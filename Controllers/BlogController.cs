using blog.Controllers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.SyndicationFeed;
using Microsoft.SyndicationFeed.Atom;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using System.Web;
using System.Xml;
using WebEssentials.AspNetCore.OutputCaching;

namespace blog.Models
{
    public class ImageFile
    {
        public string Url { get; set; }

        public string Title { get; set; }

        public long Size { get; set; }

        public DateTimeOffset Created { get; set; }

        public string Id => Path.GetFileNameWithoutExtension(this.Url);
    }

    public class BlogController : Controller
    {
        private readonly IBlogService _blog;
        private readonly IOptionsSnapshot<BlogSettings> _settings;
        private readonly IOutputCachingService _cache;

        public BlogController(
            IBlogService blog,
            IOptionsSnapshot<BlogSettings> settings,
            IOutputCachingService cache
        )
        {
            _blog = blog;
            _settings = settings;
            _cache = cache;
        }

        [Route("/{page:int?}")]
        [OutputCache(Profile = "default")]
        public async Task<IActionResult> Index([FromRoute] int page = 0)
        {
            var skip = _settings.Value.PostsPerPage * page;

            var posts = await _blog.GetPosts(_settings.Value.PostsPerPage, skip);

            ViewData["Title"] = _settings.Value.Name + " | " + _settings.Value.Description;
            ViewData["Description"] = _settings.Value.Description;

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

                if (path.EndsWith('/'))
                {
                    path = path[0..^1];
                }

                var index = path.LastIndexOf('/') + 1;

                if (index >= 0)
                {
                    var file = Path.GetFileNameWithoutExtension(path.Substring(index));

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

            if (searchResults == null || searchResults.Count() <= 0)
            {
                searchResults = await _blog.GetPostsByCategory("Featured");
            }

            return View("NotFound", searchResults);
        }

        [Authorize]
        [Route("/Import")]
        public async Task<IActionResult> Import()
        {
            var posts = await _blog.GetPosts(1);

            if (!posts.Any())
            {
                await TryImport();
            }

            return await Index();
        }

        private async Task TryImport()
        {
            StreamReader reader = await ReadUrl("https://syfuhs.net/feed/atom/");

            using (var xmlReader = XmlReader.Create(reader, new XmlReaderSettings() { }))
            {
                var feedReader = new AtomFeedReader(xmlReader);

                while (await feedReader.Read())
                {
                    if (feedReader.ElementType == SyndicationElementType.Item)
                    {
                        var item = await feedReader.ReadItem() as AtomEntry;

                        var path = item.Links.FirstOrDefault(l => l.RelationshipType == "alternate");

                        var image = item.Links.FirstOrDefault(l => l.RelationshipType == "media");

                        var post = new Post
                        {
                            Categories = item.Categories.Select(c => c.Name).ToList(),
                            Content = item.Description,
                            Title = HttpUtility.HtmlDecode(item.Title),
                            Excerpt = HttpUtility.HtmlDecode(item.Summary),
                            PubDate = item.Published.DateTime,
                            LastModified = item.LastUpdated.DateTime,
                            IsPublished = true,
                            Slug = path.Uri.AbsolutePath
                        };

                        if (image != null)
                        {
                            post.MediaUrl = image.Uri.OriginalString;
                        }

                        await _blog.SavePost(post);
                    }
                }
            }

            _blog.ResetCache();
        }

        private async Task<StreamReader> ReadUrl(string uri)
        {
            var httpClient = new HttpClient();

            var feed = await httpClient.GetAsync(uri);

            if (!feed.IsSuccessStatusCode)
            {
                return null;
            }

            var stream = await feed.Content.ReadAsStreamAsync();

            return new StreamReader(stream);
        }

        [Route("/edit/images")]
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> ListImages()
        {
            var images = await _blog.ListImages();

            return View("images", images);
        }

        [Route("/edit/upload")]
        [Authorize]
        [HttpPost]
        public async Task<IActionResult> UploadEditorImage(IFormFile file)
        {
            const long maxUploadSize = 10L * 1024L * 1024L * 1024L;

            if (!ModelState.IsValid)
            {
                return View("images");
            }

            string name = null;

            var formFileContent = await FileHelpers.ProcessFormFile<ImagesModel>(
                    file,
                    ModelState, new[] { ".jpg", ".jpeg", ".png", ".gif", ".jfif" },
                    maxUploadSize
                );

            if (formFileContent.Length > 0)
            {
                name = await UploadImage(formFileContent, file.FileName);
            }

            return Json(new { location = name });
        }

        [Route("/edit/images")]
        [HttpDelete]
        [Authorize]
        public async Task<IActionResult> DeleteImage([FromQuery] string url)
        {
            await this._blog.DeleteFile(url);

            return StatusCode((int)HttpStatusCode.NoContent);
        }

        [Route("/edit/images")]
        [Authorize]
        [HttpPost]
        public async Task<IActionResult> UploadImage(ImagesModel model)
        {
            const long maxUploadSize = 10L * 1024L * 1024L * 1024L;

            if (!ModelState.IsValid)
            {
                return View("images");
            }

            string name = null;

            foreach (var file in model.FormFiles)
            {
                var formFileContent = await FileHelpers.ProcessFormFile<ImagesModel>(
                    file,
                    ModelState, new[] { ".jpg", ".jpeg", ".png", ".gif", ".jfif" },
                    maxUploadSize
                );

                if (formFileContent.Length > 0)
                {
                    name = await UploadImage(formFileContent, file.FileName);

                    name = Path.GetFileNameWithoutExtension(name);
                }
            }

            return Redirect($"/edit/images#{name}");
        }

        private async Task<string> UploadImage(byte[] formFileContent, string name)
        {
            name = name.Replace(" ", "_");

            return await _blog.SaveFile(formFileContent, name);
        }

        [Route("/tag/{category}/{page:int?}")]
        [Route("/category/{category}/{page:int?}")]
        [OutputCache(Profile = "default")]
        public async Task<IActionResult> Category(string category, int page = 0)
        {
            var allPosts = await _blog.GetPostsByCategory(category);
            var skip = _settings.Value.PostsPerPage * page;

            var posts = allPosts.Skip(skip).Take(_settings.Value.PostsPerPage);

            ViewData["Title"] = $"{category} | {_settings.Value.Name}";
            ViewData["Description"] = $"Articles posted in the {category} category";

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

        [Route("/{year:int}/{month:int}/{day:int}/{slug}")]
        [OutputCache(Profile = "default")]
        public Task<IActionResult> Post()
        {
            return Post(Request.Path.Value);
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

        [Route("/{slug?}")]
        [OutputCache(Profile = "default")]
        public async Task<IActionResult> Post(string slug)
        {
            var post = await _blog.GetPostBySlug(slug);

            if (post != null)
            {
                post.SetDomain(_settings.Value.BaseDomain);

                if (!post.Slug.Equals(slug, StringComparison.OrdinalIgnoreCase))
                {
                    return LocalRedirectPermanent(post.Slug);
                }

                var meta = new MetaModel();

                AddTwitter(post, meta);

                this.ViewData["Meta"] = meta;

                return View(post);
            }

            return NotFound();
        }

        private void AddTwitter(Post post, MetaModel meta)
        {
            meta.MetaTags["twitter:card"] = new Meta { Attribute = "content", Value = "summary_large_image" };
            meta.MetaTags["twitter:site"] = new Meta { Attribute = "content", Value = _settings.Value.Twitter };
            meta.MetaTags["twitter:creator"] = new Meta { Attribute = "content", Value = _settings.Value.Twitter };
            meta.MetaTags["twitter:title"] = new Meta { Attribute = "content", Value = post.Title };
            meta.MetaTags["twitter:description"] = new Meta { Attribute = "content", Value = post.Excerpt };
            meta.MetaTags["twitter:image"] = new Meta { Attribute = "content", Value = post.GetMedia(BlogMediaType.PostPrimary) };
        }

        [Route("/edit/{id?}")]
        [HttpGet, Authorize]
        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrEmpty(id))
            {
                return View(new Post() { IncludeAuthor = true });
            }

            var post = await _blog.GetPostById(id);

            if (post != null)
            {
                return View(post);
            }

            return NotFound();
        }

        [Route("/")]
        [HttpPost, Authorize, AutoValidateAntiforgeryToken]
        public async Task<IActionResult> UpdatePost(Post post)
        {
            if (!ModelState.IsValid)
            {
                return View("Edit", post);
            }

            var existing = await _blog.GetPostById(post.ID) ?? post;
            string categories = Request.Form["categories"];

            existing.Categories = categories.Split(",", StringSplitOptions.RemoveEmptyEntries).Select(c => c.Trim().ToLowerInvariant()).ToList();
            existing.Title = post.Title.Trim();
            existing.Slug = post.Slug.Trim();
            existing.Slug = !string.IsNullOrWhiteSpace(post.Slug) ? post.Slug.Trim() : Models.Post.CreateSlug(post.Title);
            existing.IsPublished = post.IsPublished;
            existing.Content = post.Content.Trim();
            existing.Excerpt = post.Excerpt.Trim();
            existing.Type = post.Type;
            existing.IncludeAuthor = post.IncludeAuthor;

            if (!string.IsNullOrWhiteSpace(post.MediaUrl))
            {
                existing.MediaUrl = new Uri(post.MediaUrl.Trim()).OriginalString;
            }
            else
            {
                existing.MediaUrl = null;
            }

            if (!string.IsNullOrWhiteSpace(post.HeroImageUrl))
            {
                existing.HeroImageUrl = new Uri(post.HeroImageUrl.Trim()).OriginalString;
            }
            else
            {
                existing.HeroImageUrl = null;
            }

            await _blog.SavePost(existing);

            _cache.Clear();

            return Redirect(post.GetLink());
        }

        [Route("/deletepost/{id}")]
        [HttpPost, Authorize, AutoValidateAntiforgeryToken]
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

        [Route("/comment/{postId}")]
        [HttpPost]
        public async Task<IActionResult> AddComment(string postId, Comment comment)
        {
            var post = await _blog.GetPostById(postId);

            if (!ModelState.IsValid)
            {
                return View("Post", post);
            }

            if (post == null || !post.AreCommentsOpen(_settings.Value.CommentsCloseAfterDays))
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
