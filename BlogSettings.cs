using System.Collections.Generic;

namespace blog
{
    public class BlogSettings
    {
        public string Name { get; set; }
        
        public string Description { get; set; }
        
        public string Owner { get; set; }

        public string AuthorPage { get; set; }

        public string Twitter { get; set; }

        public string Email { get; set; }

        public string BlogEngineName { get; set; }

        public string RootUrl { get; set; }
        
        public int PostsPerPage { get; set; } = 3;
        
        public int CommentsCloseAfterDays { get; set; } = 10;

        public string BaseDomain { get; set; }

        public bool BuiltinCommentsEnabled { get; set; }

        public bool CommentoCommentsEnabled { get; set; }

        public bool EmbedTwitter { get; set; }

        public string ApplicationInsightsKey { get; set; }

        public string GoogleTagId { get; set; }

        public string ConnectionString { get; set; }

        public string URadUserId { get; set; }
        
        public string URadUserKey { get; set; }

        public IEnumerable<UrlReplacement> UrlReplacements { get; set; }

        public IEnumerable<UrlReplacement> Redirects { get; set; }
    }

    public class UrlReplacement
    {
        public string Find { get; set; }

        public string Replace { get; set; }
    }
}
