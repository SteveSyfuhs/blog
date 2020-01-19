using System.Collections.Generic;

namespace blog.Models
{

    public class SearchResults
    {
        public string Query { get; set; }

        public int Results { get; set; }

        public int Page { get; set; }

        public IEnumerable<string> Words { get; set; }

        public IEnumerable<KeyValuePair<int, Post>> Posts { get; set; }
    }
}
