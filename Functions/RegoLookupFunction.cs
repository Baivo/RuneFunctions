using Microsoft.AspNetCore.Http;
using Microsoft.OpenApi.Models;
using RuneFunctions.Services;
using PuppeteerSharp;

namespace RuneFunctions.Functions
{
    public interface IRegoFunction
    {
        Task<HttpResponseData> Run(HttpRequestData req, FunctionContext executionContext);
    }

    public class RegoFunction : IRegoFunction
    {
        private readonly ILogger _logger;
        private readonly WebDriverPool _webDriverPool;

        public RegoFunction(ILoggerFactory loggerFactory, WebDriverPool webDriverPool)
        {
            _logger = loggerFactory.CreateLogger<RegoFunction>();
            _webDriverPool = webDriverPool;
        }

        [Function("RegoLookup")]
        [OpenApiOperation(operationId: "RegoLookup", tags: new[] { "RegoLookup" })]
        [OpenApiParameter(name: "plate", In = ParameterLocation.Query, Required = true, Type = typeof(string), Description = "Plate number for lookup")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(object), Description = "The rego response")]
        [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.Unauthorized, Description = "Unauthorized request")]
        [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Invalid request")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "RegoLookup")] HttpRequestData req,
            FunctionContext executionContext)
        {
            try
            {
                _logger.LogInformation("Rego request received via HTTP trigger.");

                var queryParams = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
                string plate = queryParams["plate"];

                if (string.IsNullOrEmpty(plate))
                {
                    var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badRequestResponse.WriteStringAsync("Invalid request: 'plate' is required.");
                    return badRequestResponse;
                }

                var regoData = await RegoLookup(plate);

                if (regoData.Count == 0)
                {
                    var notFoundResponse = req.CreateResponse(HttpStatusCode.NotFound);
                    await notFoundResponse.WriteStringAsync("Vehicle information not found.");
                    return notFoundResponse;
                }

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(regoData);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogInformation(ex.Message);
                _logger.LogInformation(ex.InnerException?.ToString());
                _logger.LogInformation(ex.StackTrace);
                var notFoundResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await notFoundResponse.WriteStringAsync("An error occured.");
                return notFoundResponse;
            }
        }

        public async Task<Dictionary<string, string>> RegoLookup(string plate)
        {
            IBrowser browser = await _webDriverPool.AcquireAsync();
            var data = new Dictionary<string, string>();

            try
            {
                var page = await browser.NewPageAsync();
                await page.GoToAsync("https://www.service.transport.qld.gov.au/checkrego/application/VehicleSearch.xhtml");

                // Check for TOS page and accept if present
                if (await IsElementPresent(page, "#tAndCForm\\:confirmButton"))
                {
                    await page.ClickAsync("#tAndCForm\\:confirmButton");
                }

                // Wait for the plate input field to be visible
                await page.WaitForSelectorAsync("#vehicleSearchForm\\:plateNumber");

                // Input plate number
                await page.TypeAsync("#vehicleSearchForm\\:plateNumber", plate);

                // Click search button
                await page.ClickAsync("#vehicleSearchForm\\:confirmButton");

                // Wait for results
                await page.WaitForSelectorAsync("#j_id_61", new WaitForSelectorOptions { Timeout = 10000 });

                // Extract data
                var elements = await page.QuerySelectorAllAsync("dl.data");
                foreach (var element in elements)
                {
                    var dtElements = await element.QuerySelectorAllAsync("dt");
                    var ddElements = await element.QuerySelectorAllAsync("dd");

                    for (int i = 0; i < dtElements.Length; i++)
                    {
                        var key = await (await dtElements[i].GetPropertyAsync("innerText")).JsonValueAsync<string>();
                        var value = await (await ddElements[i].GetPropertyAsync("innerText")).JsonValueAsync<string>();
                        data[key.Trim()] = value.Trim();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error: {ex.Message}");
            }
            finally
            {
                _webDriverPool.Release(browser);
            }

            return data;
        }

        private static async Task<bool> IsElementPresent(IPage page, string selector)
        {
            try
            {
                var element = await page.QuerySelectorAsync(selector);
                return element != null;
            }
            catch
            {
                return false;
            }
        }


    }
    


}
