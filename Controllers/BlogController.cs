using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using blog.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using WebEssentials.AspNetCore.OutputCaching;

namespace blog.Controllers
{
    public class BlogController : Controller
    {
        private readonly IBlogService _blog;
        private readonly BlogSettings _settings;
        private readonly IOutputCachingService _cache;

        public BlogController(
            IBlogService blog,
            IOutputCachingService cache
        )
        {
            _blog = blog;
            _settings = _blog.Settings;
            _cache = cache;
        }

        [Route("/{page:int?}")]
        [OutputCache(Profile = "default")]
        public async Task<IActionResult> Index([FromRoute] int page = 0)
        {
            var postsPerPage = _settings.PostsPerPage;

            var skip = postsPerPage * page;

            var posts = await _blog.GetPosts(postsPerPage, skip);

            if (!posts.Any() && page > 0)
            {
                return Redirect("~/");
            }

            if (page > 0)
            {
                ViewData["Title"] = $"Page {page} - " + _settings.Name + " | " + _settings.Description;
            }
            else
            {
                ViewData["Title"] = _settings.Name + " | " + _settings.Description;
            }

            ViewData["Description"] = _settings.Name + " | " + _settings.Description;
            ViewData["Highlight"] = "Featured Posts";
            //ViewData["IncludeTitle"] = true;

            var postCount = posts.Count();
            var allPostsCount = await _blog.GetPostCount(includePages: false);

            if (skip + postCount < allPostsCount)
            {
                ViewData["prev"] = $"/{page + 1}/";
            }

            ViewData["next"] = $"/{(page <= 1 ? null : page - 1 + "/")}";

            return View("Views/Blog/Index.cshtml", posts);
        }

        [Route("/error/{statusCode:int?}")]
        public async Task<IActionResult> Error([FromRoute] int statusCode = 0)
        {
            var feature = HttpContext.Features.Get<IStatusCodeReExecuteFeature>();

            ViewData["ErrorUrl"] = feature?.OriginalPath;

            var status = (HttpStatusCode)statusCode;

            switch (status)
            {
                case HttpStatusCode.NotFound:
                    return await TryNotFound(feature);
            }

            if (statusCode >= 400 && statusCode < 599)
            {
                ViewData["ErrorCode"] = statusCode;
                ViewData["ErrorCodeName"] = status.ToString();
            }

            return View("Error");
        }

        private async Task<IActionResult> TryNotFound(IStatusCodeReExecuteFeature feature)
        {
            IEnumerable<Post> searchResults = null;

            if (!string.IsNullOrWhiteSpace(feature?.OriginalPath))
            {
                var path = feature.OriginalPath;

                var knownRedirect = _settings.Redirects.FirstOrDefault(r => path.Equals(r.Find, StringComparison.OrdinalIgnoreCase));

                if (knownRedirect != null)
                {
                    return Redirect(knownRedirect.Replace);
                }

                if (path.EndsWith('/'))
                {
                    path = path[0..^1];
                }

                var index = path.LastIndexOf('/') + 1;

                if (index >= 0)
                {
                    var file = Path.GetFileNameWithoutExtension(path[index..]);

                    if (!string.IsNullOrWhiteSpace(file))
                    {
                        var postResults = await _blog.Search(file, 0, 1);

                        var firstPost = postResults.Posts.FirstOrDefault();

                        if (firstPost.Key > 10000)
                        {
                            return Redirect(firstPost.Value.GetLink());
                        }

                        if (postResults.Results > 0)
                        {
                            searchResults = postResults.Posts.Select(p => p.Value);
                        }
                    }
                }
            }

            if (searchResults == null || !searchResults.Any())
            {
                searchResults = await _blog.GetPostsByCategory("Featured");
            }

            return View("NotFound", searchResults);
        }

        [Route("/tag/{category}/{page:int?}")]
        [Route("/category/{category}/{page:int?}")]
        [OutputCache(Profile = "default")]
        public async Task<IActionResult> Category(string category, int page = 0)
        {
            var allPosts = await _blog.GetPostsByCategory(category);
            var skip = _settings.PostsPerPage * page;

            var posts = allPosts.Skip(skip).Take(_settings.PostsPerPage);

            if (!posts.Any())
            {
                return Redirect("/");
            }

            ViewData["Title"] = $"{category} | {_settings.Name}";
            ViewData["Description"] = $"Articles posted in the {category} category";
            ViewData["Category"] = category;

            var postCount = posts.Count();
            var allPostsCount = allPosts.Count();

            if (skip + postCount < allPostsCount)
            {
                ViewData["prev"] = $"/category/{category}/{page + 1}/";
            }

            ViewData["next"] = $"/category/{category}/{(page <= 1 ? null : page - 1 + "/")}";

            return View("Views/Blog/Index.cshtml", posts);
        }

        // This is for redirecting potential existing URLs from the old Miniblog URL format
        [Route("/post/{slug}")]
        [HttpGet]
        public IActionResult Redirects(string slug)
        {
            return LocalRedirectPermanent($"/{slug}");
        }

        [Route("/{year:int}/{month:int}/{day:int}/{slug}/{page:int?}")]
        [OutputCache(Profile = "default")]
        public Task<IActionResult> Post(int year, int month, int day, string slug, int? page)
        {
            var url = Request.Path.Value;

            return Post(url.Substring(0, page.HasValue ? url.LastIndexOf('/') + 1 : url.Length), page);
        }

        [Route("/{year:int}/{month:int}/{day:int?}")]
        [OutputCache(Profile = "default")]
        public async Task<IActionResult> PostByMonth(int year, int month, int? day)
        {
            try
            {
                var dtMonth = new DateTime(year, month, 1, 0, 0, 0);

                ViewData["Title"] = $"Posts for {dtMonth:Y}";
                ViewData["IncludeTitle"] = true;
            }
            catch (ArgumentException)
            {
                return NotFound();
            }

            var allPosts = await _blog.GetPostsByMonth(year, month);

            if (day.HasValue)
            {
                allPosts = allPosts.Where(a => a.PubDate.Day == day.Value);
            }

            return View("Views/Blog/Index.cshtml", allPosts);
        }

        [Route("/{slug?}/{page:int?}")]
        [OutputCache(Profile = "default")]
        public async Task<IActionResult> Post(string slug, int? page)
        {
            var post = await _blog.GetPostBySlug(slug);

            if (post != null)
            {
                post.CurrentPage = page ?? 0;

                if (!post.Slug.Equals(slug, StringComparison.OrdinalIgnoreCase))
                {
                    return LocalRedirectPermanent(post.Slug);
                }

                this.ViewData["Meta"] = AddPostMeta(post);

                return View(post);
            }

            return NotFound();
        }

        private MetaModel AddPostMeta(Post post)
        {
            var meta = new MetaModel();

            meta.MetaTags.Add(("og:type", new Meta { Attribute = "content", Value = "article" }));
            meta.MetaTags.Add(("article:published_time", new Meta { Attribute = "content", Value = post.PubDate.ToString("o") }));
            meta.MetaTags.Add(("article:modified_time", new Meta { Attribute = "content", Value = post.LastModified.ToString("o") }));
            meta.MetaTags.Add(("article:author", new Meta { Attribute = "content", Value = _settings.Owner }));

            foreach (var cat in post.Categories)
            {
                meta.MetaTags.Add(("article:tag", new Meta { Attribute = "content", Value = cat }));
            }

            AddTwitter(post, meta);

            return meta;
        }

        private void AddTwitter(Post post, MetaModel meta)
        {
            meta.MetaTags.Add(("twitter:card", new Meta { Attribute = "content", Value = "summary_large_image" }));
            meta.MetaTags.Add(("twitter:site", new Meta { Attribute = "content", Value = _settings.Twitter }));
            meta.MetaTags.Add(("twitter:creator", new Meta { Attribute = "content", Value = _settings.Twitter }));
            meta.MetaTags.Add(("twitter:title", new Meta { Attribute = "content", Value = post.Title }));
            meta.MetaTags.Add(("twitter:description", new Meta { Attribute = "content", Value = post.Excerpt }));
            meta.MetaTags.Add(("twitter:image", new Meta { Attribute = "content", Value = post.GetMedia(BlogMediaType.PostPrimary) }));
        }

        [Route("/comment/{postId}")]
        [HttpPost]
        public async Task<IActionResult> AddComment(string postId, Comment comment)
        {
            var post = await _blog.GetPostById(postId);

            if (!ModelState.IsValid)
            {
                return View("Post", post);
            }

            if (post == null || !post.AreCommentsOpen(_settings.CommentsCloseAfterDays))
            {
                return NotFound();
            }

            comment.IsAdmin = User.Identity.IsAuthenticated;
            comment.Content = comment.Content.Trim();
            comment.Author = comment.Author.Trim();
            comment.Email = comment.Email.Trim();

            // the website form key should have been removed by javascript
            // unless the comment was posted by a spam robot
            if (!Request.Form.ContainsKey("website"))
            {
                post.Comments.Add(comment);
                await _blog.SavePost(post);
            }

            return Redirect(post.GetLink() + "#" + comment.ID);
        }

        [Route("/comment/{postId}/{commentId}")]
        [Authorize]
        public async Task<IActionResult> DeleteComment(string postId, string commentId)
        {
            var post = await _blog.GetPostById(postId);

            if (post == null)
            {
                return NotFound();
            }

            var comment = post.Comments.FirstOrDefault(c => c.ID.Equals(commentId, StringComparison.OrdinalIgnoreCase));

            if (comment == null)
            {
                return NotFound();
            }

            post.Comments.Remove(comment);
            await _blog.SavePost(post);

            return Redirect(post.GetLink() + "#comments");
        }
    }
}
