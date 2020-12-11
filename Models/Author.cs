using System.Diagnostics;

namespace blog.Models
{
    [DebuggerDisplay("{Name}")]
    public class Author
    {
        public string Name { get; set; }

        public string Description { get; set; }

        public string ImageUrl {get;set;}
    }
}
