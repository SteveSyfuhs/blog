using System;
using System.Diagnostics;
using System.IO;

namespace blog.Models
{
    [DebuggerDisplay("{Title}")]
    public class ImageFile
    {
        public string Url { get; set; }

        public string Title { get; set; }

        public long Size { get; set; }

        public DateTimeOffset Created { get; set; }

        public string Id => Path.GetFileNameWithoutExtension(this.Url);
    }
}
