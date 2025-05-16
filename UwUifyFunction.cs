using Microsoft.OpenApi.Models;
using System.Text.RegularExpressions;

namespace RuneFunctions
{
    public interface IUwUifyFunction
    {
        Task<HttpResponseData> Run(HttpRequestData req, FunctionContext executionContext);
    }

    public class UwUifyFunction : IUwUifyFunction
    {
        private readonly ILogger _logger;
        private readonly WebDriverPool _webDriverPool;

        public UwUifyFunction(ILoggerFactory loggerFactory, WebDriverPool webDriverPool)
        {
            _logger = loggerFactory.CreateLogger<RegoFunction>();
            _webDriverPool = webDriverPool;
        }

        [Function("UwUify")]
        [OpenApiOperation(operationId: "UwUify", tags: new[] { "UwUify" })]
        [OpenApiParameter(name: "text", In = ParameterLocation.Query, Required = true, Type = typeof(string), Description = "Text to UwUify")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(object), Description = "The UwUify response")]
        [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.Unauthorized, Description = "Unauthorized request")]
        [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Invalid request")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "UwUify")] HttpRequestData req,
            FunctionContext executionContext)
        {
            try
            {
                _logger.LogInformation("Rego request received via HTTP trigger.");
                var queryParams = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
                string text = queryParams["text"];

                if (string.IsNullOrEmpty(text)) 
                {
                    var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                    await badRequestResponse.WriteStringAsync("Invalid request: 'text' is required.");
                    return badRequestResponse;
                }
                else
                {
                    var response = req.CreateResponse(HttpStatusCode.OK);
                    await response.WriteAsJsonAsync(UwUify(text));
                    return response;
                }
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
        public static string UwUify(string input)
        {
            StringBuilder result = new StringBuilder();
            string[] words = input.Split(new char[] { ' ', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries);
            string[] faces = { "(・`ω´・)", ";;w;;", "OwO", "UwU", ">w<", "^w^", "ÚwÚ", "^-^", ":3", "x3" };
            string[] actions = { "*blushes*", "*whispers to self*", "*cries*", "*screams*", "*sweats*", "*twerks*", "*runs away*", "*screeches*", "*walks away*", "*sees bulge*", "*looks at you*", "*notices buldge*", "*starts twerking*", "*huggles tightly*", "*boops your nose*" };
            string[] exclamations = { "!?", "?!!", "?!?1", "!!11", "?!?!" };

            Regex[] uwuMap = {
            new Regex("(?:r|l)", RegexOptions.IgnoreCase),
            new Regex("n([aeiou])", RegexOptions.IgnoreCase),
            new Regex("ove", RegexOptions.IgnoreCase),
            };

            string[] uwuReplace = { "w", "ny$1", "uv" };

            Random rand = new Random();

            foreach (var word in words)
            {
                string newWord = word;
                // Apply transformations to the word
                for (int i = 0; i < uwuMap.Length; i++)
                {
                    if (rand.NextDouble() > 0.7) continue;
                    newWord = uwuMap[i].Replace(newWord, uwuReplace[i]);
                }

                // Add stuttering to the start of the word
                if (rand.NextDouble() < 0.1)
                {
                    string firstLetter = newWord.Substring(0, 1);
                    string stutter = firstLetter + "-";
                    newWord = stutter + newWord;
                }

                result.Append(newWord + " ");

                // Add a face or action
                double r = rand.NextDouble();
                if (r < 0.05)
                {
                    result.Append(actions[rand.Next(actions.Length)] + " ");
                }
                else if (r < 0.1)
                {

                    result.Append(faces[rand.Next(faces.Length)] + " ");
                }

                // Handle exclamations
                if (new Regex("[?!]+$").IsMatch(newWord) && rand.NextDouble() < 1)
                {
                    string exclaimReplace = exclamations[rand.Next(exclamations.Length)];
                    result.Remove(result.Length - newWord.Length, newWord.Length);
                    newWord = new Regex("[?!]+$").Replace(newWord, exclaimReplace);
                    result.Append(newWord + " ");
                }
            }
            return result.ToString().TrimEnd();
        }
    }
}
