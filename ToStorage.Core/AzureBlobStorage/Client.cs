using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Auth;
using Microsoft.WindowsAzure.Storage.Blob;

namespace Knapcode.ToStorage.Core.AzureBlobStorage
{
    public class Client
    {
        public async Task<UploadResult> UploadAsync(string account, string key, UploadRequest request)
        {
            var context = new CloudContext(account, key, request.Container);
            return await UploadAsync(context, request).ConfigureAwait(false);
        }

        public async Task<UploadResult> UploadAsync(string connectionString, UploadRequest request)
        {
            var context = new CloudContext(connectionString, request.Container);
            return await UploadAsync(context, request).ConfigureAwait(false);
        }

        public async Task<Stream> GetLatestStreamAsync(string account, string key, GetLatestRequest request)
        {
            var context = new CloudContext(account, key, request.Container);
            return await GetLatestStreamAsync(request, context);
        }

        public async Task<Stream> GetLatestStreamAsync(string connectionString, GetLatestRequest request)
        {
            var context = new CloudContext(connectionString, request.Container);
            return await GetLatestStreamAsync(request, context);
        }

        private static async Task<Stream> GetLatestStreamAsync(GetLatestRequest request, CloudContext context)
        {
            if (!await context.BlobContainer.ExistsAsync())
            {
                request.Trace.WriteLine($"Blob container '{context.BlobContainer.Name}' in account '{context.StorageAccount.Credentials.AccountName}' does not exist.");
                return null;
            }

            var latestPath = GetLatestPath(request.PathFormat);
            var latestBlob = context.BlobContainer.GetBlockBlobReference(latestPath);

            if (!await latestBlob.ExistsAsync())
            {
                request.Trace.WriteLine($"No blob exists at '{latestBlob.Uri}'.");
            }

            return await latestBlob.OpenWriteAsync();
        }

        private async Task<UploadResult> UploadAsync(CloudContext context, UploadRequest request)
        {
            // initialize
            request.Trace.Write("Initializing...");

            await context.BlobContainer.CreateIfNotExistsAsync(
                BlobContainerPublicAccessType.Blob,
                new BlobRequestOptions(),
                null).ConfigureAwait(false);
            request.Trace.WriteLine(" done.");

            var directPath = string.Format(request.PathFormat, DateTimeOffset.UtcNow.ToString("yyyy.MM.dd.HH.mm.ss"));
            request.Trace.Write($"Uploading the blob to '{directPath}'...");
            var directBlob = context.BlobContainer.GetBlockBlobReference(directPath);

            // upload the blob
            using (var blobStream = await directBlob.OpenWriteAsync().ConfigureAwait(false))
            {
                await request.Stream.CopyToAsync(blobStream).ConfigureAwait(false);
            }
            request.Trace.WriteLine(" done.");

            // set the content type
            if (!string.IsNullOrWhiteSpace(request.ContentType))
            {
                request.Trace.Write("Setting the content type...");
                directBlob.Properties.ContentType = request.ContentType;
                await directBlob.SetPropertiesAsync().ConfigureAwait(false);
                request.Trace.WriteLine(" done.");
            }

            // set the latest
            CloudBlockBlob latestBlob = null;
            if (request.UpdateLatest)
            {
                var latestPath = GetLatestPath(request.PathFormat);
                request.Trace.Write($"Updating '{latestPath}' to the latest blob...");
                latestBlob = context.BlobContainer.GetBlockBlobReference(latestPath);
                await latestBlob.StartCopyAsync(directBlob).ConfigureAwait(false);
                while (latestBlob.CopyState.Status == CopyStatus.Pending)
                {
                    await Task.Delay(100).ConfigureAwait(false);
                }
                request.Trace.WriteLine(" done.");
            }

            var result = new UploadResult {DirectUrl = directBlob.Uri};
            request.Trace.WriteLine();
            request.Trace.WriteLine($"Direct: {directBlob.Uri}");
            if (latestBlob != null)
            {
                result.LatestUrl = latestBlob.Uri;
                request.Trace.WriteLine($"Latest: {latestBlob.Uri}");
            }

            return result;
        }

        private static string GetLatestPath(string pathFormat)
        {
            var latestPath = string.Format(pathFormat, "latest");
            return latestPath;
        }

        private class CloudContext
        {
            private CloudContext(string container)
            {
                BlobClient = StorageAccount.CreateCloudBlobClient();
                BlobContainer = BlobClient.GetContainerReference(container);
            }

            public CloudContext(string connectionString, string container) : this(container)
            {
                StorageAccount = CloudStorageAccount.Parse(connectionString);
            }

            public CloudContext(string account, string key, string container) : this(container)
            {
                var storageCredentials = new StorageCredentials(account, key);
                StorageAccount = new CloudStorageAccount(storageCredentials, true);
            }

            public CloudStorageAccount StorageAccount { get; }
            public CloudBlobClient BlobClient { get; }
            public CloudBlobContainer BlobContainer { get; }
        }
    }
}