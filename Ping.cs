namespace RuneFunctions
{
    public interface IPingFunction
    {
        Task<HttpResponseData> Run(HttpRequestData req, FunctionContext executionContext);
    }

    public class PingFunction : IPingFunction
    {
        private readonly ILogger _logger;

        public PingFunction(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<PingFunction>();
        }

        [Function("Ping")]
        [OpenApiOperation(operationId: "Ping", tags: new[] { "template" })]
        [OpenApiRequestBody("application/json", typeof(object), Description = "JSON request body")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(object), Description = "The Pong response")]
        [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.Unauthorized, Description = "Unauthorized request")]
        [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Invalid request")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "Ping")] HttpRequestData req,
            FunctionContext executionContext)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync("Pong");
            return response;
        }
    }
}
