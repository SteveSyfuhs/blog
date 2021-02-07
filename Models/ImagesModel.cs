using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Http;

namespace blog.Models
{
    public class ImagesFolder
    {
        public string Path { get; set; }

        private readonly List<ImageFile> images = new List<ImageFile>();

        public IEnumerable<ImageFile> Images => images;

        public IDictionary<string, ImagesFolder> Folders { get; set; } = new Dictionary<string, ImagesFolder>();

        public bool Active { get; set; }

        public void AddImage(ImageFile file)
        {
            var path = new Uri(ExtractFolder(file.Url)).AbsolutePath;

            char[] charSeparators = new char[] { '/' };

            var parts = path.Split(charSeparators, StringSplitOptions.RemoveEmptyEntries);

            ImagesFolder current = this;

            Queue<string> pathSoFar = new Queue<string>();

            foreach (string part in parts)
            {
                pathSoFar.Enqueue(part);

                if (!current.Folders.TryGetValue(part, out ImagesFolder child))
                {
                    child = new ImagesFolder { Path = string.Join('/', pathSoFar) };

                    current.Folders[part] = child;
                }

                current = child;
            }

            current.images.Add(file);
        }

        private static string ExtractFolder(string url)
        {
            var lastIndex = url.LastIndexOf('/');

            return url.Substring(0, lastIndex);
        }
    }

    public class ImagesModel
    {
        public ICollection<ImageFile> Images { get; } = new List<ImageFile>();

        [Display(Name = "Files")]
        public ICollection<IFormFile> FormFiles { get; set; }

        [Display(Name = "Upload Folder")]
        public string UploadFolder { get; set; }
    }
}
