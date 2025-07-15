using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using blog.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

namespace blog.Models
{
    public enum PostType
    {
        [Display(Name = "Blog Post")]
        Post,
        [Display(Name = "Content Page")]
        Page
    }

    [DebuggerDisplay("{Title} | {Slug}")]
    public class Post
    {
        public static readonly string PlaceholderImage = "data:image/gif;base64,R0lGODlhAQABAIAAAP///wAAACH5BAEAAAAALAAAAAABAAEAAAICRAEAOw==";

        private readonly IBlogService service;

        public Post()
        {
        }

        public Post(IBlogService service)
        {
            this.service = service;
        }

        [Required]
        public string ID { get; set; } = DateTime.UtcNow.Ticks.ToString();

        [Required]
        public string Title { get; set; }

        public string Slug { get; set; }

        [Required]
        public string Excerpt { get; set; }

        [Required]
        public string Content { get; set; }

        public string Draft { get; set; }

        public PostType Type { get; set; }

        [BindProperty, DisplayFormat(DataFormatString = "{0:yyyy-MM-ddTHH:mm}", ApplyFormatInEditMode = true)]
        public DateTime PubDate { get; set; } = DateTime.UtcNow;

        public DateTime LastModified { get; set; } = DateTime.UtcNow;

        public string PrimaryMediaUrl { get; set; }

        public string HeroBackgroundImageUrl { get; set; }

        public bool IsPublished { get; set; } = true;

        public IList<string> Categories { get; set; } = new List<string>();

        public IList<Comment> Comments { get; } = new List<Comment>();

        public Author Author { get; set; }

        public bool IncludeAuthor { get; set; }

        public bool IsIndexed { get; set; }

        public int CurrentPage { get; set; }

        public Post AsDraft()
        {
            return new Post(this.service)
            {
                Author = this.Author,
                IncludeAuthor = this.IncludeAuthor,
                Categories = this.Categories,
                Content = this.Draft,
                Draft = this.Draft,
                CurrentPage = this.CurrentPage,
                Excerpt = this.Excerpt,
                HeroBackgroundImageUrl = this.HeroBackgroundImageUrl,
                ID = this.ID,
                PubDate = this.PubDate,
                PrimaryMediaUrl = this.PrimaryMediaUrl,
                IsIndexed = this.IsIndexed,
                Slug = this.Slug,
                IsPublished = this.IsPublished,
                LastModified = this.LastModified,
                Title = this.Title,
                Type = this.Type
            };
        }

        public string GetLink()
        {
            if (Slug.StartsWith('/'))
            {
                return Slug;
            }

            return "/" + Slug;
        }

        public string GeneratePermalink(HttpRequest request)
        {
            var uriBuilder = new UriBuilder
            {
                Scheme = "https",
                Host = request.Host.Host,
                Path = GetLink(),
                Query = request.QueryString.ToString()
            };

            return uriBuilder.Uri.OriginalString;
        }

        public bool AreCommentsOpen(int commentsCloseAfterDays)
        {
            return PubDate.AddDays(commentsCloseAfterDays) >= DateTime.UtcNow;
        }

        public static string CreateSlug(string title)
        {
            title = title.ToLowerInvariant().Replace(" ", "-");
            title = RemoveDiacritics(title);
            title = RemoveReservedUrlCharacters(title);

            return title.ToLowerInvariant();
        }

        static string RemoveReservedUrlCharacters(string text)
        {
            var reservedCharacters = new List<string>()
            {
                "!", "#", "$", "&", "'", "(", ")", "*", ",", "/", ":", ";", "=", "?", "@", "[",
                "]", "\"", "%", ".", "<", ">", "\\", "^", "_", "'", "{", "}", "|", "~", "`", "+"
            };

            foreach (var chr in reservedCharacters)
            {
                text = text.Replace(chr, "");
            }

            return text;
        }

        static string RemoveDiacritics(string text)
        {
            var normalizedString = text.Normalize(NormalizationForm.FormD);
            var stringBuilder = new StringBuilder();

            foreach (var c in normalizedString)
            {
                var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory != UnicodeCategory.NonSpacingMark)
                {
                    stringBuilder.Append(c);
                }
            }

            return stringBuilder.ToString().Normalize(NormalizationForm.FormC);
        }

        public string GetMedia(BlogMediaType type)
        {
            var mediaUrl = type switch
            {
                BlogMediaType.PostBackground => HeroBackgroundImageUrl,
                _ => PrimaryMediaUrl,
            };

            if (string.IsNullOrWhiteSpace(mediaUrl))
            {
                return null;
            }

            foreach (var rep in this.service.Settings.UrlReplacements)
            {
                mediaUrl = mediaUrl.Replace(rep.Find, rep.Replace);
            }

            return mediaUrl;
        }

        private static readonly List<KeyValuePair<string, string>> EmbeddedReplaces = new()
        {
            KeyValuePair.Create(
                "youtube",
                "<div class=\"video\">" +
                "<iframe width=\"560\" height=\"315\" title=\"YouTube embed\" src=\"about:blank\" " +
                "data-src=\"https://www.youtube-nocookie.com/embed/{0}?modestbranding=1&amp;hd=1&amp;rel=0&amp;theme=light\" allowfullscreen></iframe>" +
                "</div>"
            ),
            KeyValuePair.Create(
                "gist",
                "<script src=\"https://gist.github.com/{0}.js\"></script>" //name/c3c3bd7d8d2da771d10b9d453d68b5eb
            ),
            KeyValuePair.Create(
                "iframe",
                "<iframe src={0}></iframe>"
            ),
            KeyValuePair.Create(
                "gallery",
                ""
            )
        };

        public async Task<string> RenderAsPage(HttpContext context = null)
        {
            var rendered = await this.RenderContent(context: context);

            var pages = rendered.Split("[page-break]", StringSplitOptions.RemoveEmptyEntries);

            var pageLink = this.GetLink();

            if (pageLink.EndsWith('/'))
            {
                pageLink = pageLink[0..^1];
            }

            for (var i = 0; i < pages.Length; i++)
            {
                if (pages.Length > 1)
                {
                    var pageList = $"<div class=\"pages\">Page {i + 1} / {pages.Length}</div>";

                    pages[i] = pageList + pages[i] + pageList;
                }

                pages[i] += "<nav class=\"pagination\" aria-label=\"Pagination\">";

                if (i == 1)
                {
                    pages[i] += $"<a href=\"{pageLink}\">&laquo; Previous Page</a>";
                }
                else if (i > 0)
                {
                    pages[i] += $"<a href=\"{pageLink}/{i - 2}\">&laquo; Previous Page</a>";
                }

                if (i < pages.Length - 1)
                {
                    pages[i] += $"<a href=\"{pageLink}/{i + 2}\">Continue Reading on Next Page &raquo;</a>";
                }

                pages[i] += "</nav>";
            }

            if (this.CurrentPage > 1 && this.CurrentPage <= pages.Length)
            {
                return pages[this.CurrentPage - 1];
            }

            return pages[0];
        }

        public async Task<string> RenderContent(bool lazyLoad = true, HttpContext context = null)
        {
            var result = this.Content;

            if (lazyLoad)
            {
                result = result.Replace(" src=\"", $" src=\"{PlaceholderImage}\" data-src=\"");
            }

            var components = new List<(string Key, string Value)>();

            foreach (var embed in EmbeddedReplaces)
            {
                //\[(({embed.Key}:[a-z]+)\s*([^>]*))\]
                //"\[{embed.Key}:(.*?)\]"

                result = Regex.Replace(result.ToString(), $@"\[{embed.Key}:(.*?)\]", (Match m) =>
                {
                    if (string.IsNullOrWhiteSpace(embed.Value) && m.Groups.Count > 1)
                    {
                        components.Add((embed.Key, m.Groups[1].Value));

                        return $"[component{components.Count - 1}]";
                    }

                    return string.Format(embed.Value, m.Groups[1].Value, m.Groups.Count > 2 ? m.Groups[2].Value : "");
                });
            }

            foreach (var rep in this.service.Settings.UrlReplacements)
            {
                result = result.Replace(rep.Find, rep.Replace);
            }

            for (var i = 0; i < components.Count; i++)
            {
                var component = components[i];

                var renderedValue = await RenderComponent(component.Key, component.Value, context);

                result = result.Replace($"[component{i}]", renderedValue);
            }

            return result.ToString();
        }

        private async Task<string> RenderComponent(string key, string value, HttpContext context)
        {
            return key.Trim().ToLowerInvariant() switch
            {
                "gallery" => await RenderGallery(value, context),
                _ => "",
            };
        }

        private async Task<string> RenderGallery(string value, HttpContext httpContext)
        {
            var viewName = "~/views/shared/_GalleryComponent.cshtml";

            var model = await this.service.ListImages();

            string pattern = Regex.Escape(value).Replace("\\*", ".*?");

            var filteredImages = model.Images.Where(i => Regex.IsMatch(i.Title, pattern) && !i.Title.Contains("-sm")).ToList();

            model.Images.Clear();

            foreach (var img in filteredImages)
            {
                model.Images.Add(img);
            }

            return await ViewRenderer.RenderView(httpContext, viewName, model);
        }
    }
}
