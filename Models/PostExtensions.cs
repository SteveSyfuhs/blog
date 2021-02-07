using System;
using System.Collections.Generic;
using System.Linq;

namespace blog.Models
{
    public static class PostExtensions
    {
        public static Post FindHighlight(this IEnumerable<Post> posts)
        {
            var post = posts.FirstOrDefault(
                p => !string.IsNullOrWhiteSpace(p.GetMedia(BlogMediaType.PostPrimary)) &&
                     p.Categories.Any(c => "featured".Equals(c, StringComparison.OrdinalIgnoreCase))
            );

            if (post == null)
            {
                post = posts.FirstOrDefault(p => !string.IsNullOrWhiteSpace(p.GetMedia(BlogMediaType.PostPrimary)));
            }

            return post;
        }

        public static IEnumerable<Post> FindSnapshots(this IEnumerable<Post> posts, Post highlight)
        {
            return posts.Where(p => p != highlight);
        }

        public static string ToFileSize(this long size)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };

            int order = 0;

            var sizeDec = (decimal)size;

            while (sizeDec >= 1024 && order < sizes.Length - 1)
            {
                order++;
                sizeDec /= 1024;
            }

            return string.Format("{0:0.##} {1}", sizeDec, sizes[order]);
        }
    }
}
