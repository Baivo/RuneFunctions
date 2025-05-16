using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.OpenApi.Models;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Newtonsoft.Json;
using RuneFunctions.Services;

namespace RuneFunctions.Functions
{
    public class UrbanDictionaryFunction
    {
        private readonly UrbanDictionaryService _svc;

        public UrbanDictionaryFunction(UrbanDictionaryService svc) => _svc = svc;

        [Function("UrbanDictionaryDefine")]
        [OpenApiOperation("UrbanDictionaryDefine", "UrbanDictionary",
            Summary = "Get Urban Dictionary entry for a given input")]
        [OpenApiParameter("term", In = ParameterLocation.Query, Required = false, Type = typeof(string),
            Summary = "Word or phrase. Empty → random entry.")]
        [OpenApiResponseWithBody(HttpStatusCode.OK, "application/json",
            typeof(UrbanDictionaryService.UrbanEntry),
            Summary = "Urban Dictionary entry")]
        [OpenApiResponseWithoutBody(HttpStatusCode.NotFound, Summary = "No results")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "UrbanDictionaryDefine")]
            HttpRequestData req)
        {
            var term = System.Web.HttpUtility
                        .ParseQueryString(req.Url.Query)["term"] ?? string.Empty;

            var entry = await _svc.GetDefinitionAsync(term);   // UrbanEntry?

            if (entry is null)
            {
                var notFound = req.CreateResponse(HttpStatusCode.NotFound);
                await notFound.WriteStringAsync("No results.");
                return notFound;
            }

            var ok = req.CreateResponse(HttpStatusCode.OK);
            ok.Headers.Add("Content-Type", "application/json");
            await ok.WriteStringAsync(
                JsonConvert.SerializeObject(entry, Formatting.Indented));
            return ok;
        }
    }
}
