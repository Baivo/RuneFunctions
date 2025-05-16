// AnimalsFunction.cs
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using Newtonsoft.Json.Linq;
using System.Net;
using System.Net.Http.Headers;

namespace RuneFunctions.Functions
{
    public static class AnimalsFunction
    {
        private static readonly HttpClient _http = new()
        {
            Timeout = TimeSpan.FromSeconds(10)
        };

        // ──────── routes ────────
        [Function("Animals_Cat")]
        [OpenApiOperation("Animals_Cat", tags: new[] { "Animals" })]
        [OpenApiResponseWithBody(HttpStatusCode.OK, "image/jpeg", typeof(byte[]),
            Description = "Random cat image")]
        public static Task<HttpResponseData> Cat(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "animals/cat")]
            HttpRequestData req,
            FunctionContext ctx)
            => HandleAnimalImageAsync(req, ctx,
                "https://api.thecatapi.com/v1/images/search",
                j => j[0]!["url"]!.ToString());

        [Function("Animals_Dog")]
        [OpenApiOperation("Animals_Dog", tags: new[] { "Animals" })]
        [OpenApiResponseWithBody(HttpStatusCode.OK, "image/jpeg", typeof(byte[]),
            Description = "Random dog image")]
        public static Task<HttpResponseData> Dog(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "animals/dog")]
            HttpRequestData req,
            FunctionContext ctx)
            => HandleAnimalImageAsync(req, ctx,
                "https://api.thedogapi.com/v1/images/search",
                j => j[0]!["url"]!.ToString());

        [Function("Animals_Lizard")]
        [OpenApiOperation("Animals_Lizard", tags: new[] { "Animals" })]
        [OpenApiResponseWithBody(HttpStatusCode.OK, "image/jpeg", typeof(byte[]),
            Description = "Random lizard image")]
        public static Task<HttpResponseData> Lizard(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "animals/lizard")]
            HttpRequestData req,
            FunctionContext ctx)
            => HandleAnimalImageAsync(req, ctx,
                "https://nekos.life/api/v2/img/lizard",
                j => j["url"]!.ToString());

        // ──────── helper ────────
        private static async Task<HttpResponseData> HandleAnimalImageAsync(
            HttpRequestData req,
            FunctionContext ctx,
            string apiUrl,
            Func<JToken, string> extractor)
        {
            var log = ctx.GetLogger("AnimalsImg");

            try
            {
                // 1️⃣  Get JSON from upstream
                using var metaReq = new HttpRequestMessage(HttpMethod.Get, apiUrl);
                metaReq.Headers.UserAgent.Add(new ProductInfoHeaderValue("RuneBot", "1.0"));
                using HttpResponseMessage metaResp = await _http.SendAsync(metaReq);

                if (!metaResp.IsSuccessStatusCode)
                {
                    log.LogWarning("Meta API failed [{Status}] {Url}", metaResp.StatusCode, apiUrl);
                    return await Fail(req);
                }

                string metaRaw = await metaResp.Content.ReadAsStringAsync();
                string imgUrl = extractor(JToken.Parse(metaRaw));

                // 2️⃣  Download the image
                using var imgReq = new HttpRequestMessage(HttpMethod.Get, imgUrl);
                using HttpResponseMessage imgResp = await _http.SendAsync(imgReq);

                if (!imgResp.IsSuccessStatusCode)
                {
                    log.LogWarning("Image download failed [{Status}] {Url}", imgResp.StatusCode, imgUrl);
                    return await Fail(req);
                }

                byte[] bytes = await imgResp.Content.ReadAsByteArrayAsync();
                string contentType = imgResp.Content.Headers.ContentType?.ToString()
                                     ?? "image/jpeg";

                // 3️⃣  Return image bytes
                var ok = req.CreateResponse(HttpStatusCode.OK);
                ok.Headers.Add("Content-Type", contentType);
                await ok.Body.WriteAsync(bytes);
                return ok;
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Animal image handler error");
                return await Fail(req);
            }

            static async Task<HttpResponseData> Fail(HttpRequestData r)
            {
                var res = r.CreateResponse(HttpStatusCode.ServiceUnavailable);
                await res.WriteStringAsync("Upstream error – please try again later.");
                return res;
            }
        }
    }
}
