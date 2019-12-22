using blog.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Azure.Storage;
using Microsoft.Azure.Storage.Blob;
using Microsoft.Extensions.Options;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace blog
{
    public class AzureStorageBlogService : FileBlogService
    {
        private const string PostContainerName = "posts";
        private const string FilesContainerName = "files";

        private readonly BlogSettings settings;

        public AzureStorageBlogService(
            IWebHostEnvironment env,
            IHttpContextAccessor contextAccessor,
            BlogSettings settings
        ) : base(env, contextAccessor, true)
        {
            this.settings = settings;

            Initialize();
        }

        protected override async Task PersistPost(Post post, XDocument doc)
        {
            string filePath = $"{post.ID}.xml";

            var container = LoadBlobContainer(PostContainerName);

            CloudBlockBlob blob = container.GetBlockBlobReference(filePath);

            await blob.UploadTextAsync(doc.ToString());
        }

        private static readonly HashSet<string> TrustedImageExtensions = new HashSet<string>
        {
            "png",
            "jpg",
            "jpeg"
        };

        protected override async Task<string> PersistDataFile(byte[] bytes, string fileName, string suffix)
        {
            string ext = Path.GetExtension(fileName).Substring(1);
            string name = Path.GetFileNameWithoutExtension(fileName);

            string relative = $"{name}_{suffix}.{ext}";

            var container = LoadBlobContainer(FilesContainerName);

            CloudBlockBlob blob = container.GetBlockBlobReference(relative);

            if (TrustedImageExtensions.Contains(ext))
            {
                blob.Properties.ContentType = $"image/{ext}";
            }

            await blob.UploadFromByteArrayAsync(bytes, 0, bytes.Length);

            return blob.Uri.OriginalString;
        }

        protected override void LoadPosts()
        {
            CloudBlobContainer container = LoadBlobContainer(PostContainerName);

            BlobContinuationToken continuationToken = null;

            do
            {
                var resultSegment = container.ListBlobsSegmented(string.Empty, true, BlobListingDetails.Metadata, null, continuationToken, null, null);

                foreach (var blobItem in resultSegment.Results)
                {
                    if (blobItem is CloudBlob blob && blob.Name.EndsWith(".xml"))
                    {
                        using (var stream = new MemoryStream())
                        {
                            blob.DownloadToStream(stream);

                            LoadPost(blob.Name, stream);
                        }
                    }
                }

                continuationToken = resultSegment.ContinuationToken;

            }
            while (continuationToken != null);
        }

        private CloudBlobContainer LoadBlobContainer(string containerName)
        {
            CloudStorageAccount account = CloudStorageAccount.Parse(settings.ConnectionString);

            CloudBlobClient client = account.CreateCloudBlobClient();

            var container = client.GetContainerReference(containerName);
            container.CreateIfNotExists();

            return container;
        }
    }
}
