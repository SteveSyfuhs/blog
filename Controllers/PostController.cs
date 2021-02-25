using System;
using System.Linq;
using System.Threading.Tasks;
using blog.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using WebEssentials.AspNetCore.OutputCaching;

namespace blog.Controllers
{
    [Authorize, AutoValidateAntiforgeryToken]
    public class PostController : Controller
    {
        private readonly IBlogService _blog;
        private readonly BlogSettings _settings;
        private readonly IOutputCachingService _cache;

        public PostController(
            IBlogService blog,
            IOutputCachingService cache
        )
        {
            _blog = blog;
            _settings = _blog.Settings;
            _cache = cache;
        }

        [Route("/edit/{id?}")]
        [HttpGet]
        public async Task<IActionResult> Edit(string id, [FromQuery] bool draft)
        {
            if (string.IsNullOrEmpty(id))
            {
                return View(new Post() { IncludeAuthor = true, IsIndexed = true });
            }

            var post = await _blog.GetPostById(id);

            if (post != null)
            {
                if (draft && !string.IsNullOrWhiteSpace(post.Draft))
                {
                    post = post.AsDraft();

                    post.Content = post.Draft;
                }

                return View(post);
            }

            return NotFound();
        }

        [Route("/manage/{page:int?}")]
        [HttpGet]
        public async Task<IActionResult> EditPosts([FromRoute] int page)
        {
            var pageSize = _settings.PostsPerPage;

            var skip = pageSize * page;

            var posts = await _blog.GetAllContent(pageSize, skip);

            ViewData["Title"] = _settings.Name + " | " + _settings.Description;
            ViewData["Description"] = _settings.Description;

            var postCount = posts.Count();
            var allPostsCount = await _blog.GetPostCount(includePages: false);

            if (skip + postCount < allPostsCount)
            {
                ViewData["prev"] = $"/manage/{page + 1}/";
            }

            ViewData["next"] = $"/manage/{(page <= 1 ? null : page - 1 + "/")}";

            return View("ManagePosts", posts);
        }

        [Route("/")]
        [HttpPost]
        public async Task<IActionResult> UpdatePost(Post post)
        {
            if (!ModelState.IsValid)
            {
                return View("Edit", post);
            }

            await UpdateOrCreate(post);

            _cache.Clear();

            return Redirect(post.GetLink());
        }

        private async Task<Post> UpdateOrCreate(Post post)
        {
            var existing = await _blog.GetPostById(post.ID) ?? post;

            string categories = Request.Form["categories"];

            existing.Categories = categories.Split(",", StringSplitOptions.RemoveEmptyEntries).Select(c => c.Trim()).ToList();
            existing.Title = post.Title?.Trim();
            existing.Slug = !string.IsNullOrWhiteSpace(post.Slug) ? post.Slug.Trim() : Post.CreateSlug(post.Title);
            existing.IsPublished = post.IsPublished;
            existing.IncludeAuthor = post.IncludeAuthor;
            existing.IsIndexed = post.IsIndexed;

            existing.Content = post.Content?.Trim();
            existing.Excerpt = post.Excerpt?.Trim();
            existing.Type = post.Type;
            existing.PubDate = post.PubDate;
            existing.Draft = null;

            if (!string.IsNullOrWhiteSpace(post.PrimaryMediaUrl) && Uri.TryCreate(post.PrimaryMediaUrl.Trim(), UriKind.Absolute, out Uri mediaUrl))
            {
                existing.PrimaryMediaUrl = mediaUrl.AbsoluteUri;
            }
            else
            {
                existing.PrimaryMediaUrl = null;
            }

            if (!string.IsNullOrWhiteSpace(post.HeroBackgroundImageUrl) && Uri.TryCreate(post.HeroBackgroundImageUrl.Trim(), UriKind.Absolute, out Uri heroImageUrl))
            {
                existing.HeroBackgroundImageUrl = heroImageUrl.AbsoluteUri;
            }
            else
            {
                existing.HeroBackgroundImageUrl = null;
            }

            await _blog.SavePost(existing);

            return existing;
        }

        [Route("/deletepost/{id}")]
        [HttpPost]
        public async Task<IActionResult> DeletePost(string id)
        {
            var existing = await _blog.GetPostById(id);

            if (existing != null)
            {
                await _blog.DeletePost(existing);
                return Redirect("/");
            }

            return NotFound();
        }

        [Route("/edit/{id}/draft")]
        [HttpPost]
        public async Task<IActionResult> SaveDraft(Post post)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var currentPost = await _blog.GetPostById(post.ID);

            var isNewPost = false;

            if (currentPost == null)
            {
                post.IsPublished = false;

                currentPost = await UpdateOrCreate(post);

                isNewPost = true;
            }

            if (!isNewPost && !string.IsNullOrWhiteSpace(post.Content))
            {
                currentPost.Draft = post.Content;
            }
            else if (!isNewPost && !string.IsNullOrWhiteSpace(post.Draft))
            {
                currentPost.Draft = post.Draft;
            }

            if (string.Equals(currentPost.Content, currentPost.Draft))
            {
                currentPost.Draft = null;
            }

            await _blog.SavePost(currentPost);

            return Json(new { Id = currentPost.ID });
        }
    }
}
