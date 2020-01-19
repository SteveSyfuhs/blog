using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace blog.Models
{
    public enum PostType
    {
        Post,
        Page
    }

    [DebuggerDisplay("{Title} | {Slug}")]
    public class Post
    {
        private string domain;

        [Required]
        public string ID { get; set; } = DateTime.UtcNow.Ticks.ToString();

        [Required]
        public string Title { get; set; }

        public string Slug { get; set; }

        [Required]
        public string Excerpt { get; set; }

        [Required]
        public string Content { get; set; }

        public PostType Type { get; set; }

        public DateTime PubDate { get; set; } = DateTime.UtcNow;

        public DateTime LastModified { get; set; } = DateTime.UtcNow;

        public string MediaUrl { get; set; }

        public bool IsPublished { get; set; } = true;

        public IList<string> Categories { get; set; } = new List<string>();

        public IList<Comment> Comments { get; } = new List<Comment>();

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
                Host = domain ?? request.Host.Host,
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
            var reservedCharacters = new List<string>() { "!", "#", "$", "&", "'", "(", ")", "*", ",", "/", ":", ";", "=", "?", "@", "[", "]", "\"", "%", ".", "<", ">", "\\", "^", "_", "'", "{", "}", "|", "~", "`", "+" };

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

        public string GetMedia()
        {
            if (string.IsNullOrWhiteSpace(MediaUrl))
            {
                return null;
            }

            var mediaUrl = MediaUrl;

            foreach (var rep in HardReplace)
            {
                mediaUrl = mediaUrl.Replace(rep.Key, rep.Value);
            }

            return mediaUrl;
        }

        private static readonly List<KeyValuePair<string, string>> HardReplace = new List<KeyValuePair<string, string>>
        {
            KeyValuePair.Create("http://", "https://"),
            KeyValuePair.Create("https://syfuhs.blob.core.windows.net", "https://syfuhsblog.blob.core.windows.net"),
            KeyValuePair.Create("https://syfuhs.net/wp-content/", "https://syfuhsblog.blob.core.windows.net/"),
            KeyValuePair.Create("https://www.syfuhs.net/", "https://syfuhs.net/"),
            KeyValuePair.Create("https://syfuhs.net/IMAGES/", "https://syfuhsblog.blob.core.windows.net/images/")
        };

        private static readonly List<KeyValuePair<string, string>> EmbeddedReplaces = new List<KeyValuePair<string, string>>
        {
            KeyValuePair.Create(
                "youtube",
                "<div class=\"video\">" +
                "<iframe width=\"560\" height=\"315\" title=\"YouTube embed\" src=\"about:blank\" data-src=\"https://www.youtube-nocookie.com/embed/{0}?modestbranding=1&amp;hd=1&amp;rel=0&amp;theme=light\" allowfullscreen></iframe>" +
                "</div>"
            ),
            KeyValuePair.Create(
                "gist",
                "<script src=\"https://gist.github.com/{0}.js\"></script>" //SteveSyfuhs/c3c3bd7d8d2da771d10b9d453d68b5eb
            ),
            KeyValuePair.Create(
                "iframe",
                "<iframe src={0}></iframe>"
            ),
        };

        public string RenderContent()
        {
            var result = Content;

            result = result.Replace(" src=\"", " src=\"data:image/gif;base64,R0lGODlhAQABAIAAAP///wAAACH5BAEAAAAALAAAAAABAAEAAAICRAEAOw==\" data-src=\"");

            foreach (var embed in EmbeddedReplaces)
            {
                //\[(({embed.Key}:[a-z]+)\s*([^>]*))\]
                //"\[{embed.Key}:(.*?)\]"

                result = Regex.Replace(result.ToString(), $@"\[{embed.Key}:(.*?)\]", (Match m) =>
                {
                    var str = string.Format(embed.Value, m.Groups[1].Value, m.Groups.Count > 2 ? m.Groups[2].Value : "");

                    return str;
                });
            }

            foreach (var rep in HardReplace)
            {
                result = result.Replace(rep.Key, rep.Value);
            }

            return result.ToString();
        }

        internal void SetDomain(string baseDomain)
        {
            domain = baseDomain;
        }
    }
}
