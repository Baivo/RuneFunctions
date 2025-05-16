using Microsoft.OpenApi.Models;
using OpenAI.Managers;
using OpenAI.ObjectModels.RequestModels;
using OpenAI.ObjectModels;
using OpenAI;

namespace RuneFunctions
{
    public class WebSummaryFunction
    {
        private static OpenAIService _aiService = new OpenAIService(new OpenAiOptions()
        {
            ApiKey = EnvironmentVariables.OpenAiApiKey,
        });

        private readonly ILogger _logger;

        public WebSummaryFunction(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<WebSummaryFunction>();
        }

        [Function("WebSummary")]
        [OpenApiOperation(operationId: "WebSummary", tags: new[] { "websummary" })]
        [OpenApiParameter(name: "url", In = ParameterLocation.Query, Required = true, Type = typeof(string), Description = "URL to summarize")]
        [OpenApiParameter(name: "code", In = ParameterLocation.Query, Required = true, Type = typeof(string), Description = "Authentication code")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "string", bodyType: typeof(string), Description = "The summary of the content")]
        [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Invalid request")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "WebSummary")] HttpRequestData req,
            FunctionContext executionContext)
        {
            _logger.LogInformation("WebSummary function request received.");

            var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
            var url = query["url"];
            var code = query["code"];
            Uri funcUrl = req.Url;

            if (string.IsNullOrEmpty(url))
            {
                var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequestResponse.WriteStringAsync("Invalid request: 'url' and 'code' are required.");
                return badRequestResponse;
            }

            try
            {
                string? mainContent = await GetScrapedContent(url, funcUrl, code);
                if (string.IsNullOrEmpty(mainContent))
                {
                    var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                    await errorResponse.WriteStringAsync("Failed to scrape content.");
                    return errorResponse;
                }

                var summary = await GetWebSummary(mainContent);
                if (summary == null)
                {
                    var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                    await errorResponse.WriteStringAsync("Failed to generate summary.");
                    return errorResponse;
                }

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteStringAsync(summary);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error: {ex.Message}");
                var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                await errorResponse.WriteStringAsync($"Error: {ex.Message}");
                return errorResponse;
            }
        }

        private static async Task<string> GetScrapedContent(string url, Uri funcUrl, string? code)
        {
            var baseUrl = $"{funcUrl.Scheme}://{funcUrl.Host}";

            // Include the port if it's not the default port (80 for HTTP or 443 for HTTPS)
            if (!((funcUrl.Scheme == "http" && funcUrl.Port == 80) || (funcUrl.Scheme == "https" && funcUrl.Port == 443)))
            {
                baseUrl += $":{funcUrl.Port}";
            }

            using var client = new HttpClient();
            string requestUrl;
            if (code == null)
                requestUrl = $"{baseUrl}/api/ScrapeWebContent?url={url}";
            else
                requestUrl = $"{baseUrl}/api/ScrapeWebContent?url={url}&code={code}";

            var response = await client.GetAsync(requestUrl);
            response.EnsureSuccessStatusCode();
            return await response.Content.ReadAsStringAsync();
        }

        private static async Task<string> GetWebSummary(string content)
        {
            try
            {
                var summaryMessages = GetSummaryMessages(content);
                var summaryResult = await _aiService.CreateCompletion(new ChatCompletionCreateRequest()
                {
                    Messages = summaryMessages,
                    Model = Models.Gpt_4o,
                });

                if (summaryResult.Choices.First().Message.Content?.ToLower() == "invalid")
                {
                    return "Invalid content";
                }

                return summaryResult.Choices.First().Message.Content;
            }
            catch (JsonReaderException ex)
            {
                Console.WriteLine($"JSON Reader Exception: {ex.Message}");
                Console.WriteLine($"Path: {ex.Path}, Line: {ex.LineNumber}, Position: {ex.LinePosition}");
                return null;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"General Exception: {ex.Message}");
                return null;
            }
        }

        private static List<ChatMessage> GetSummaryMessages(string message)
        {
            List<ChatMessage> messages = new List<ChatMessage>
            {
                ChatMessage.FromUser("Provide a summary of this article, aiming for around two standard twitter tweet in length (don't style it as a tweet, it's a TL;DR summary of the article). Do not provide anything other than the summary e.g. don't preface the response with 'this article blah blah' etc., aim to impartially represent the article's content as a summary, including all crucial detail and substance. If you believe it appears something has gone wrong (maybe you got bunk, blank or paywall related text instead, simply respond with the word INVALID in all caps, and end it there). You may be provided with content other than the information that requires summary, such as information on the site itself that is consistent across pages, privacy policy/naviation elements or other such content. You should ignore this as it's an indication that the webscraper and the cleaning up function thereafter has missed it's job a bit, focus on providing a summary of the content that you identify as the target for summary."),
                ChatMessage.FromUser("Article: " + message)
            };
            return messages;
        }
    }
}
