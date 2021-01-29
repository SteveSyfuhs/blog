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
        public List<(string Key, Meta Value)> MetaTags { get; } = new List<(string Key, Meta Value)>();
    }
}
