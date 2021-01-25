using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Xml.Linq;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using blog.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;

namespace blog
{
    public class AzureStorageBlogService : FileBlogService
    {
        private const string PostContainerName = "posts";
        private const string FilesContainerName = "files";
        private const string ImagesContainerName = "images";

        public AzureStorageBlogService(
            IWebHostEnvironment env,
            IHttpContextAccessor contextAccessor,
            BlogSettings settings
        ) : base(env, contextAccessor, true, settings)
        {
            InitializeSync();
        }

        protected override async Task PersistPost(Post post, XDocument doc)
        {
            string filePath = $"{post.ID}.xml";

            var container = LoadBlobContainer(PostContainerName);

            var blob = container.GetBlobClient(filePath);

            using (var stream = new MemoryStream())
            using (var writer = new StreamWriter(stream))
            {
                writer.Write(doc.ToString());
                writer.Flush();
                stream.Position = 0;

                await blob.UploadAsync(stream, overwrite: true);
            }
        }

        private static readonly Dictionary<string, string> TrustedImageExtensions = new Dictionary<string, string>
        {
            { "png", "image/png" },
            { "jpg", "image/jpeg" },
            { "jpe", "image/jpeg" },
            { "jpeg", "image/jpeg" },
            { "gif", "image/gif" },
            { "svg", "image/svg+xml" },
            { "jfif", "image/jpeg" }
        };

        protected override async Task<string> PersistDataFile(byte[] bytes, string fileName, string suffix)
        {
            string ext = Path.GetExtension(fileName).Substring(1).ToLowerInvariant();
            string name = Path.GetFileNameWithoutExtension(fileName);

            string relative = WebUtility.UrlDecode($"{name}_{suffix}.{ext}");

            BlobContainerClient container;

            if (TrustedImageExtensions.TryGetValue(ext, out string contentType))
            {
                container = LoadBlobContainer(ImagesContainerName);
            }
            else
            {
                container = LoadBlobContainer(FilesContainerName);
            }

            var blob = container.GetBlobClient(relative);

            var options = new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders
                {
                    CacheControl = "max-age=31536000"
                }
            };

            if (!string.IsNullOrWhiteSpace(contentType))
            {
                options.HttpHeaders.ContentType = contentType;
            }

            using (var stream = new MemoryStream(bytes))
            {
                await blob.UploadAsync(stream, options);
            }

            return blob.Uri.OriginalString;
        }

        protected override async Task LoadPosts()
        {
            await IterateBlobItems(LoadPost, PostContainerName);
        }

        public override async Task<ImagesModel> ListImages()
        {
            var images = new List<ImageFile>();

            await IterateBlobItems(
                (blob, item) =>
                {
                    images.Add(new ImageFile
                    {
                        Title = blob.Name,
                        Url = blob.Uri.OriginalString.Replace("%2F", "/"),
                        Created = item.Properties.CreatedOn ?? DateTimeOffset.MinValue,
                        Size = item.Properties.ContentLength ?? 0
                    });

                    return Task.CompletedTask;
                },
                ImagesContainerName
            );

            var model = new ImagesModel();

            foreach (var image in images.OrderBy(i => i.Url))
            {
                model.Images.Add(image);
            }

            return model;
        }

        public override async Task DeletePost(Post post)
        {
            string filePath = $"/{PostContainerName}/{post.ID}.xml";

            await DeleteFile(filePath);

            await base.DeletePost(post);
        }

        public override async Task DeleteFile(string file)
        {
            string substr = file;

            if (Uri.TryCreate(file, UriKind.Absolute, out Uri uri))
            {
                substr = uri.AbsolutePath;
            }

            if (substr.StartsWith('/'))
            {
                substr = substr[1..];
            }

            var containerName = substr.Substring(0, substr.IndexOf('/'));

            var container = LoadBlobContainer(containerName);

            var path = substr[(containerName.Length + 1)..];

            var blob = container.GetBlobClient(path);

            await blob.DeleteIfExistsAsync();
        }

        private async Task IterateBlobItems(Func<BlobClient, BlobItem, Task> loader, string containerName)
        {
            var container = LoadBlobContainer(containerName);

            var blobs = container.GetBlobsAsync();

            await foreach (var blobItem in blobs)
            {
                await loader(container.GetBlobClient(blobItem.Name), blobItem);
            }
        }

        private async Task LoadPost(BlobClient blob, BlobItem item)
        {
            if (blob.Name.EndsWith(".xml"))
            {
                using (var stream = new MemoryStream())
                {
                    var downloaded = await blob.DownloadAsync();

                    LoadPost(blob.Name, downloaded.Value.Content);
                }
            }
        }

        private BlobContainerClient LoadBlobContainer(string containerName)
        {
            return new BlobContainerClient(Settings.ConnectionString, containerName);
        }
    }
}
