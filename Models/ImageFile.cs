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

        public long Length { get; set; }

        public string Size => this.Length.ToFileSize();

        public DateTimeOffset Created { get; set; }

        public string Id => Path.GetFileNameWithoutExtension(this.Url);
    }
}
