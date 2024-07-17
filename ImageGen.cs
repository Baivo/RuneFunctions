using System.Net;
using System.Reflection;
using System.Text.Json;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Azure.Functions.Worker.Extensions.OpenApi.Extensions;
using Microsoft.Extensions.Logging;
using Rune.Services;

namespace RuneFunctions
{
    public interface IImageGenFunction
    {
        Task<HttpResponseData> Run(HttpRequestData req, FunctionContext executionContext);
    }

    public class ImageGenFunction : IImageGenFunction
    {
        private readonly ILogger _logger;

        public ImageGenFunction(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<ImageGenFunction>();
        }

        [Function("ImageGen")]
        [OpenApiOperation(operationId: "ImageGen", tags: new[] { "image" })]
        [OpenApiRequestBody("application/json", typeof(ImageGenRequest), Description = "JSON request body")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(ImageGenResponse), Description = "The image URL response")]
        [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Invalid request")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "ImageGen")] HttpRequestData req,
            FunctionContext executionContext)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");

            var requestBody = await JsonSerializer.DeserializeAsync<ImageGenRequest>(req.Body);

            if (requestBody == null || string.IsNullOrEmpty(requestBody.Prompt))
            {
                var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequestResponse.WriteStringAsync("Invalid request body");
                return badRequestResponse;
            }

            string? imageUrl = await GenerateImageUrl(requestBody.Prompt, requestBody.Style);

            var response = req.CreateResponse(HttpStatusCode.OK);
            var responseBody = new ImageGenResponse { ImageUrl = imageUrl };
            await response.WriteAsJsonAsync(responseBody);

            return response;
        }

        private async Task<string?> GenerateImageUrl(string prompt, string? style)
        {
            var sdService = new StableDiffusionService();
            var blobService = new BlobServiceClient("YourAzureStorageConnectionString");

            if (style != null)
                prompt = prompt + " " + style;

            var sdImageResult = await sdService.GenerateImageAsync(prompt);
            if (sdImageResult == "Error")
                return null;

            var sdImageData = Convert.FromBase64String(sdImageResult);
            var containerClient = blobService.GetBlobContainerClient("genimages");
            var blobClient = containerClient.GetBlobClient($"{Guid.NewGuid()}.png");

            using var stream = new MemoryStream(sdImageData);
            await blobClient.UploadAsync(stream, new BlobHttpHeaders { ContentType = "image/png" });

            return blobClient.Uri.ToString();
        }
    }

    public class ImageGenRequest
    {
        public string Prompt { get; set; }
        public string? Style { get; set; }
    }

    public class ImageGenResponse
    {
        public string ImageUrl { get; set; }
    }

    public class StableDiffusionService
    {
        public async Task<string> GenerateImageAsync(string prompt)
        {
            // Placeholder for image generation logic
            // Replace with actual call to Stable Diffusion API and return the base64 image string
            return await Task.FromResult("Base64EncodedImageString");
        }
    }
}
