using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.OpenApi.Models;
using RuneFunctions.Services;
using System.Net;
using Azure.Storage.Sas;
using Microsoft.Extensions.Logging;
using System.Web;
using System.IO;
using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using System.Security.Policy;

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
        [OpenApiParameter(name: "prompt", In = ParameterLocation.Query, Required = true, Type = typeof(string), Description = "Prompt to use for image generation")]
        [OpenApiParameter(name: "style", In = ParameterLocation.Query, Required = false, Type = typeof(string), Description = "(Optional) Style of the image")]
        [OpenApiParameter(name: "hosted", In = ParameterLocation.Query, Required = false, Type = typeof(bool), Description = "(Optional) Use blob storage if true, otherwise return base64 string (default: false)")]
        [OpenApiParameter(name: "redirect", In = ParameterLocation.Query, Required = false, Type = typeof(bool), Description = "(Optional - Requires hosted = true) Redirect to image URL if true, otherwise return URL in response body (default: false)")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "text/plain", bodyType: typeof(string), Description = "The image URL or base64 response")]
        [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Invalid request")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "ImageGen")] HttpRequestData req,
            FunctionContext executionContext)
        {
            _logger.LogInformation("ImageGen function request received.");

            var query = HttpUtility.ParseQueryString(req.Url.Query);
            var prompt = query["prompt"];
            var style = query["style"];
            bool redirect = bool.TryParse(query["redirect"], out var redirectValue) ? redirectValue : false;
            bool hosted = bool.TryParse(query["hosted"], out var hostedValue) ? hostedValue : false;

            if (string.IsNullOrEmpty(prompt))
            {
                var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequestResponse.WriteStringAsync("Invalid request: 'prompt' is required.");
                return badRequestResponse;
            }

            if (hosted)
            {
                string? imageUrl = await GenerateImageUrl(prompt, style);

                if (imageUrl == null)
                {
                    var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                    await errorResponse.WriteStringAsync("Failed to generate image.");
                    return errorResponse;
                }

                if (redirect)
                {
                    var redirectResponse = req.CreateResponse(HttpStatusCode.Redirect);
                    redirectResponse.Headers.Add("Location", imageUrl);
                    return redirectResponse;
                }
                else
                {
                    var response = req.CreateResponse(HttpStatusCode.OK);
                    await response.WriteAsJsonAsync(imageUrl);
                    return response;
                }
            }
            else
            {
                string? imageBase64 = await GenerateImageBase64(prompt, style);

                if (imageBase64 == null)
                {
                    var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                    await errorResponse.WriteStringAsync("Failed to generate image.");
                    return errorResponse;
                }

                string htmlContent = $"<html><body><img src=\"data:image/png;base64,{imageBase64}\" /></body></html>";
                var response = req.CreateResponse(HttpStatusCode.OK);
                response.Headers.Add("Content-Type", "text/html");
                await response.WriteStringAsync(htmlContent);
                return response;
            }
        }
        private async Task<string?> GenerateImageUrl(string prompt, string? style)
        {
            var imageService = new AiImageService();
            var blobService = new BlobServiceClient(EnvironmentVariables.BlobConnectionString);

            if (style != null)
                prompt = prompt + " " + style;

            Tuple<string, string> imageResult = await imageService.GenerateImageAsync(prompt);
            string imageUrl = imageResult.Item1;

            var imageData = await MediaService.UrlToByteArrayAsync(imageUrl);
            var containerClient = blobService.GetBlobContainerClient("genimages");
            var blobClient = containerClient.GetBlobClient($"{Guid.NewGuid()}.png");

            await blobClient.UploadAsync(imageData, new BlobHttpHeaders { ContentType = "image/png" });
            Uri uri = blobClient.GenerateSasUri(BlobSasPermissions.Read, new DateTimeOffset(DateTime.Now).AddYears(10));
            return uri.ToString();
        }

        private async Task<string?> GenerateImageBase64(string prompt, string? style)
        {
            var imageService = new AiImageService();
            if (style != null)
                prompt = prompt + " " + style;
            Tuple<string, string> imageResult = await imageService.GenerateImageAsync(prompt);
            string imageUrl = imageResult.Item1;

            var imageData = await MediaService.UrlToByteArrayAsync(imageUrl);
            return Convert.ToBase64String(imageData.ToArray());
        }
    }
}
