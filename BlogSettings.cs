namespace blog
{
    public class BlogSettings
    {
        public string Name { get; set; } = "Steve on Security";
        
        public string Description { get; set; } = "Theoretical Headbanging";
        
        public string Owner { get; set; } = "Steve Syfuhs";
        
        public int PostsPerPage { get; set; } = 10;
        
        public int CommentsCloseAfterDays { get; set; } = 10;

        public string BaseDomain { get; set; }

        public bool BuiltinCommentsEnabled { get; set; }

        public string ConnectionString { get; set; }
    }
}
