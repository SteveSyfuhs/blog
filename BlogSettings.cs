using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;

namespace blog
{
    public class BlogSettings
    {
        [DisplayName("Blog Name")]
        public string Name { get; set; }

        [DisplayName("Blog Description")]
        public string Description { get; set; }

        [DisplayName("Blog Owner")]
        public string Owner { get; set; }

        [DisplayName("Author Page")]
        public string AuthorPage { get; set; }

        [DisplayName("Twitter Handle")]
        public string Twitter { get; set; }

        [DisplayName("Email Contact")]
        public string Email { get; set; }

        [DisplayName("Blog Engine Name")]
        public string BlogEngineName { get; set; }

        [DisplayName("Site Root URL")]
        public string RootUrl { get; set; }

        [DisplayName("Posts Per Page")]
        public int PostsPerPage { get; set; } = 3;

        [DisplayName("Days After Posting to Close Comments")]
        public int CommentsCloseAfterDays { get; set; } = 10;

        [DisplayName("Enable Built-in Comments")]
        public bool BuiltinCommentsEnabled { get; set; }

        [DisplayName("Enable Commento Comments")]
        public bool CommentoCommentsEnabled { get; set; }

        [DisplayName("Embed Twitter Widgets")]
        public bool EmbedTwitter { get; set; }

        [DisplayName("Application Insights Key")]
        public string ApplicationInsightsKey { get; set; }

        [DisplayName("Google Tag Id")]
        public string GoogleTagId { get; set; }

        [DisplayName("URad User ID")]
        public string URadUserId { get; set; }

        [DisplayName("URad User Key")]
        public string URadUserKey { get; set; }

        public List<UrlReplacement> UrlReplacements { get; set; }

        public List<UrlReplacement> Redirects { get; set; }
    }

    [DebuggerDisplay("{Find} => {Replace}")]
    public class UrlReplacement
    {
        public string Find { get; set; }

        public string Replace { get; set; }
    }
}
