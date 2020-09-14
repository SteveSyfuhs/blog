using System.Collections.Generic;

namespace blog.Models
{
    public class Meta
    {
        public string Attribute { get; set; }

        public string Value { get; set; }
    }

    public class MetaModel
    {
        public Dictionary<string, Meta> MetaTags { get; } = new Dictionary<string, Meta>();
    }
}
