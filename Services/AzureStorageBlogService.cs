using blog.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Storage;
using Microsoft.Azure.Storage.Blob;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Xml.Linq;

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

            var container = await LoadBlobContainer(PostContainerName);

            CloudBlockBlob blob = container.GetBlockBlobReference(filePath);

            await blob.UploadTextAsync(doc.ToString());
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

            string relative = $"{name}_{suffix}.{ext}";

            CloudBlobContainer container;

            if (TrustedImageExtensions.TryGetValue(ext, out string contentType))
            {
                container = await LoadBlobContainer(ImagesContainerName);
            }
            else
            {
                container = await LoadBlobContainer(FilesContainerName);
            }

            var blob = container.GetBlockBlobReference(relative);

            if (!string.IsNullOrWhiteSpace(contentType))
            {
                blob.Properties.ContentType = contentType;
                blob.Properties.CacheControl = "max-age=31536000";
            }

            await blob.UploadFromByteArrayAsync(bytes, 0, bytes.Length);

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
                blob =>
                {
                    images.Add(new ImageFile
                    {
                        Title = blob.Name,
                        Url = blob.Uri.OriginalString,
                        Created = blob.Properties.Created ?? DateTimeOffset.MinValue,
                        Size = blob.Properties.Length
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

        public override async Task DeleteFile(string file)
        {
            var blobUri = new Uri(file).AbsolutePath;

            var substr = blobUri.Substring(1);

            var containerName = substr.Substring(0, substr.IndexOf('/'));

            CloudBlobContainer container = await LoadBlobContainer(containerName);

            var path = substr.Substring(containerName.Length + 1);

            var blob = await container.GetBlobReferenceFromServerAsync(path);

            await blob.DeleteIfExistsAsync();

        }

        private async Task IterateBlobItems(Func<CloudBlob, Task> loader, string containerName)
        {
            CloudBlobContainer container = await LoadBlobContainer(containerName);

            BlobContinuationToken continuationToken = null;

            do
            {
                var resultSegment = await container.ListBlobsSegmentedAsync(
                    string.Empty,
                    true,
                    BlobListingDetails.Metadata,
                    null,
                    continuationToken,
                    null,
                    null
                );

                foreach (CloudBlob blobItem in resultSegment.Results)
                {
                    await loader(blobItem);
                }

                continuationToken = resultSegment.ContinuationToken;

            }
            while (continuationToken != null);
        }

        private async Task LoadPost(CloudBlob blob)
        {
            if (blob.Name.EndsWith(".xml"))
            {
                using (var stream = new MemoryStream())
                {
                    await blob.DownloadToStreamAsync(stream);

                    LoadPost(blob.Name, stream);
                }
            }
        }

        private async Task<CloudBlobContainer> LoadBlobContainer(string containerName)
        {
            CloudStorageAccount account = CloudStorageAccount.Parse(Settings.ConnectionString);

            CloudBlobClient client = account.CreateCloudBlobClient();

            var container = client.GetContainerReference(containerName);

            await container.CreateIfNotExistsAsync();

            return container;
        }
    }
}
