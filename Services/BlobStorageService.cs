using Azure.Storage.Blobs;
using System.Text;
using Newtonsoft.Json;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Azure.Storage;

namespace RuneFunctions.Services
{

    public class Result<T>
    {
        public T? Value { get; set; } = default(T);
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }

        public Result(T? value, bool success, string? errorMessage = null)
        {
            Value = value;
            Success = success;
            ErrorMessage = errorMessage;
        }
    }


    public interface IBlobStorageService
    {
        Task UploadObjectAsync<T>(string containerName, string blobName, T obj);

        Task UploadBytesAsync(string containerName, string blobName, byte[] data, string contentType);
        Task<Result<T>> DownloadObjectAsync<T>(string containerName, string blobName);
        Task DeleteBlobAsync(string containerName, string blobName);
        Task CreateContainerAsync(string containerName);
        Task<IEnumerable<BlobItem>> QueryBlobsAsync(string containerName, string query);
        Task<IEnumerable<BlobItem>> GetBlobsAsync(string containerName);

    }
    public class BlobStorageService : IBlobStorageService
    {

        private readonly BlobServiceClient _blobServiceClient;
        //fix this
        private readonly string AccountKey = "QxP9Eph7SPI/Z4+4qnDRIx7heRmWKnvBniTRHigkHXFER4um5nnheUYlvnDsb/EofajfPGn4sU2k+AStCs2lng==";
        private readonly string AccountName = "b1vcorune";
        private readonly string? ConnectionString = EnvironmentVariables.BlobConnectionString;
        public BlobStorageService()
        {
            _blobServiceClient = new BlobServiceClient(ConnectionString);
        }

        public async Task UploadBytesAsync(string containerName, string blobName, byte[] data, string contentType)
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
            var blobClient = containerClient.GetBlobClient(blobName);

            using (var memoryStream = new MemoryStream(data))
            {
                await blobClient.UploadAsync(memoryStream, new BlobUploadOptions()
                {
                    HttpHeaders = new BlobHttpHeaders()
                    {
                        ContentType = contentType
                    }
                });
            }
        }

        public async Task UploadObjectAsync<T>(string containerName, string blobName, T obj)
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
            await containerClient.CreateIfNotExistsAsync();
            var blobClient = containerClient.GetBlobClient(blobName);

            var json = JsonConvert.SerializeObject(obj);
            var bytes = Encoding.UTF8.GetBytes(json);
            using (var memoryStream = new MemoryStream(bytes))
            {
                await blobClient.UploadAsync(memoryStream, overwrite: true);
            }
        }


        public async Task<Result<T>> DownloadObjectAsync<T>(string containerName, string blobName)
        {
            try
            {
                var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
                var blobClient = containerClient.GetBlobClient(blobName);
                var response = await blobClient.DownloadAsync();

                if (response != null)
                {
                    using (var streamReader = new StreamReader(response.Value.Content))
                    {
                        var json = await streamReader.ReadToEndAsync();
                        var value = JsonConvert.DeserializeObject<T>(json);
                        return new Result<T>(value, true);
                    }
                }
                else
                {
                    throw new Exception("Blob response is null.");
                }
            }
            catch (Exception ex)
            {
                return new Result<T>(default, false, ex.Message);
            }
        }

        public async Task<IEnumerable<BlobItem>> QueryBlobsAsync(string containerName, string? query = null)
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
            var list = new List<BlobItem>();
            await foreach (var blobItem in containerClient.GetBlobsAsync(prefix: query))
            {
                list.Add(blobItem);
            }
            return list;
        }

        public async Task<IEnumerable<BlobItem>> GetBlobsAsync(string containerName)

        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
            var list = new List<BlobItem>();
            await foreach (var blobItem in containerClient.GetBlobsAsync())
            {
                list.Add(blobItem);
            }
            return list;
        }

        public string GetBlobSasUri(string containerName, string blobName)
        {
            BlobSasBuilder sasBuilder = new BlobSasBuilder()
            {
                BlobContainerName = containerName,
                BlobName = blobName,
                Resource = "b",
                ExpiresOn = DateTimeOffset.UtcNow.AddDays(7) // expiry time of the link, can be adjusted, might want it as a param, not sure
            };

            // Allow read access
            sasBuilder.SetPermissions(BlobSasPermissions.Read);

            var sasToken = sasBuilder.ToSasQueryParameters(new StorageSharedKeyCredential(AccountName, AccountKey)).ToString();

            BlobClient blobClient = _blobServiceClient.GetBlobContainerClient(containerName).GetBlobClient(blobName);

            var sasUri = blobClient.GenerateSasUri(sasBuilder);
            return sasUri.ToString();
        }


        public async Task DeleteBlobAsync(string containerName, string blobName)
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
            var blobClient = containerClient.GetBlobClient(blobName);
            await blobClient.DeleteIfExistsAsync();
        }

        public async Task CreateContainerAsync(string containerName)
        {
            await _blobServiceClient.CreateBlobContainerAsync(containerName);
        }
        public async Task UploadMemoryStreamAsync(string containerName, string blobName, MemoryStream memoryStream, string? mimeType = null, bool setContentDisposition = false)
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
            await containerClient.CreateIfNotExistsAsync();
            var blockBlob = containerClient.GetBlobClient(blobName);

            var blobHttpHeader = new BlobHttpHeaders();
            if (mimeType != null)
            {
                blobHttpHeader.ContentType = mimeType;
            }
            if (setContentDisposition)
            {
                blobHttpHeader.ContentDisposition = $"inline; filename=\"{Uri.EscapeDataString(blobName)}\"";
            }

            memoryStream.Position = 0;
            if (await blockBlob.ExistsAsync())
            {
                await blockBlob.SetHttpHeadersAsync(blobHttpHeader);
                await blockBlob.UploadAsync(memoryStream, overwrite: true);
            }
            else
            {
                await blockBlob.UploadAsync(memoryStream, new BlobUploadOptions { HttpHeaders = blobHttpHeader });
            }
        }


        public async Task<MemoryStream> DownloadMemoryStreamAsync(string containerName, string blobName)
        {
            var containerClient = _blobServiceClient.GetBlobContainerClient(containerName);
            var blobClient = containerClient.GetBlobClient(blobName);

            BlobDownloadInfo download = await blobClient.DownloadAsync();

            MemoryStream memoryStream = new MemoryStream();
            await download.Content.CopyToAsync(memoryStream);
            memoryStream.Position = 0; // Reset the position after writing to the stream.

            return memoryStream;
        }

    }
}
