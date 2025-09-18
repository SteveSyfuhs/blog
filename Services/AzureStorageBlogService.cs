using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using blog.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;

namespace blog
{
    public class AzureStorageBlogService : FileBlogService
    {
        private const string PostContainerName = "posts";
        private const string FilesContainerName = "files";
        private const string ImagesContainerName = "images";
        private const string SettingsContainerName = "settings";

        public AzureStorageBlogService(
            IWebHostEnvironment env,
            IHttpContextAccessor contextAccessor,
            IConfiguration config
        ) : base(env, contextAccessor, config)
        {
        }

        protected override async Task<BlogSettings> LoadSettings()
        {
            var container = LoadBlobContainer(SettingsContainerName);

            var blogClient = container.GetBlobClient("blog.json");

            if (!await blogClient.ExistsAsync())
            {
                var localSettings = await base.LoadSettings();

                await UpdateSettings(localSettings);
            }

            var settings = await blogClient.DownloadAsync();

            using JsonTextReader reader = new JsonTextReader(new StreamReader(settings.Value.Content));

            var serializer = new JsonSerializer();
            return serializer.Deserialize<BlogSettings>(reader);
        }

        public override async Task UpdateSettings(BlogSettings localSettings)
        {
            var container = LoadBlobContainer(SettingsContainerName);

            await container.CreateIfNotExistsAsync(PublicAccessType.None);

            var blogClient = container.GetBlobClient("blog.json");
            
            await blogClient.DeleteIfExistsAsync();

            using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(localSettings))))
            {
                await blogClient.UploadAsync(stream);
            }

            await Initialize();
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

        protected override async Task<string> PersistDataFile(byte[] bytes, string fileName, string suffix)
        {
            string ext = Path.GetExtension(fileName)[1..].ToLowerInvariant();

            try
            {
                return await Upload(bytes, ext, WebUtility.UrlDecode(fileName));
            }
            catch (FileOverwriteException)
            {
                string name = Path.GetFileNameWithoutExtension(fileName);

                string relative = WebUtility.UrlDecode($"{name}-{suffix}.{ext}");

                return await Upload(bytes, ext, relative);
            }
        }

        protected override async Task<IEnumerable<Post>> LoadPosts()
        {
            var posts = new List<Post>();

            await IterateBlobItems(async (client, item) =>
            {
                var post = await LoadPost(client, item);

                if (post != null)
                {
                    posts.Add(post);
                }

            }, PostContainerName);

            return posts;
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
                        Length = item.Properties.ContentLength ?? 0
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

            var containerName = substr[..substr.IndexOf('/')];

            var container = LoadBlobContainer(containerName);

            var path = substr[(containerName.Length + 1)..];

            var blob = container.GetBlobClient(path);

            await blob.DeleteIfExistsAsync();
        }

        private async Task<string> Upload(byte[] bytes, string ext, string relative)
        {
            BlobContainerClient container;

            if (TrustedImageExtensions.TryGetValue(ext, out string contentType))
            {
                container = LoadBlobContainer(ImagesContainerName);
            }
            else
            {
                container = LoadBlobContainer(FilesContainerName);
            }

            if (relative.StartsWith(container.Name + "/"))
            {
                relative = relative[(container.Name.Length + 1)..];
            }

            var blob = container.GetBlobClient(relative);

            if (await blob.ExistsAsync())
            {
                throw new FileOverwriteException();
            }

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

        private async Task IterateBlobItems(Func<BlobClient, BlobItem, Task> loader, string containerName)
        {
            var container = LoadBlobContainer(containerName);

            var blobs = container.GetBlobsAsync();

            await foreach (var blobItem in blobs)
            {
                await loader(container.GetBlobClient(blobItem.Name), blobItem);
            }
        }

        private async Task<Post> LoadPost(BlobClient blob, BlobItem item)
        {
            if (blob.Name.EndsWith(".xml"))
            {
                using (var stream = new MemoryStream())
                {
                    var downloaded = await blob.DownloadAsync();

                    return LoadPost(blob.Name, downloaded.Value.Content);
                }
            }

            return null;
        }

        private static readonly Dictionary<string, string> TrustedImageExtensions = new()
        {
            { "png", "image/png" },
            { "jpg", "image/jpeg" },
            { "jpe", "image/jpeg" },
            { "jpeg", "image/jpeg" },
            { "gif", "image/gif" },
            { "svg", "image/svg+xml" },
            { "jfif", "image/jpeg" }
        };

        private BlobContainerClient LoadBlobContainer(string containerName)
        {
            return new BlobContainerClient(Site.ConnectionString, containerName);
        }
    }
}
