using System.Net;
using System.Text;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Utilities.Encoders;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;

namespace RuneFunctions
{
    public interface IInteractionFunction
    {
        Task<HttpResponseData> Run(HttpRequestData req, FunctionContext executionContext);
    }

    public class InteractionFunction : IInteractionFunction
    {
        private readonly ILogger _logger;
        private static readonly string PublicKey = Environment.GetEnvironmentVariable("DISCORD_PUBLIC_KEY");

        public InteractionFunction(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<InteractionFunction>();
        }

        [Function("Interactions")]
        [OpenApiOperation(operationId: "Interactions", tags: new[] { "discord" })]
        [OpenApiRequestBody("application/json", typeof(object), Description = "JSON request body")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(object), Description = "The interaction response")]
        [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.Unauthorized, Description = "Unauthorized request")]
        [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Invalid request")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "interactions")] HttpRequestData req,
            FunctionContext executionContext)
        {
            _logger.LogInformation("C# HTTP trigger function processed a request.");

            // Read request body
            string requestBody = await new StreamReader(req.Body).ReadToEndAsync();

            // Validate signature
            if (!req.Headers.TryGetValues("X-Signature-Ed25519", out var signatureValues) ||
                !req.Headers.TryGetValues("X-Signature-Timestamp", out var timestampValues) ||
                !IsValidRequest(signatureValues.First(), timestampValues.First(), requestBody))
            {
                _logger.LogWarning("Invalid request signature.");
                var unauthorizedResponse = req.CreateResponse(HttpStatusCode.Unauthorized);
                return unauthorizedResponse;
            }

            var interaction = JsonConvert.DeserializeObject<DiscordInteraction>(requestBody);
            var interactionResponse = HandleInteraction(interaction);

            var response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(interactionResponse);
            return response;
        }

        private static bool IsValidRequest(string signature, string timestamp, string body)
        {
            try
            {
                byte[] publicKey = Hex.Decode(PublicKey);
                byte[] signatureBytes = Hex.Decode(signature);
                byte[] bodyBytes = Encoding.UTF8.GetBytes(timestamp + body);

                var publicKeyParams = new Ed25519PublicKeyParameters(publicKey, 0);
                var verifier = new Org.BouncyCastle.Crypto.Signers.Ed25519Signer();
                verifier.Init(false, publicKeyParams);
                verifier.BlockUpdate(bodyBytes, 0, bodyBytes.Length);
                return verifier.VerifySignature(signatureBytes);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception during request validation: {ex.Message}");
                return false;
            }
        }

        private static object HandleInteraction(DiscordInteraction interaction)
        {
            
            switch (interaction.Type)
            {
                case InteractionType.Ping:
                    return new { type = InteractionResponseType.Pong };

                case InteractionType.ApplicationCommand:
                    return new
                    {
                        type = InteractionResponseType.ChannelMessageWithSource,
                        data = new
                        {
                            content = "Received an application command!"
                        }
                    };

                case InteractionType.MessageComponent:
                    return new
                    {
                        type = InteractionResponseType.ChannelMessageWithSource,
                        data = new
                        {
                            content = "Received a message component interaction!"
                        }
                    };

                //case InteractionType.ApplicationCommandAutocomplete:
                //    return new
                //    {
                //        type = InteractionResponseType.ApplicationCommandAutocompleteResult,
                //        data = new
                //        {
                //            choices = new[]
                //            {
                //                new { name = "Choice 1", value = "choice_1" },
                //                new { name = "Choice 2", value = "choice_2" }
                //            }
                //        }
                //    };

                case InteractionType.ModalSubmit:
                    return new
                    {
                        type = InteractionResponseType.ChannelMessageWithSource,
                        data = new
                        {
                            content = "Received a modal submit interaction!"
                        }
                    };

                default:
                    return new
                    {
                        type = InteractionResponseType.ChannelMessageWithSource,
                        data = new
                        {
                            content = "Unknown interaction type!"
                        }
                    };
            }
        }
    }

    public class DiscordInteraction
    {
        [JsonProperty("type")]
        public InteractionType Type { get; set; }

        [JsonProperty("data")]
        public InteractionData Data { get; set; }
    }

    public class InteractionData
    {
        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("name")]
        public string Name { get; set; }

        // Add other relevant properties
    }

    public enum InteractionType
    {
        Ping = 1,
        ApplicationCommand = 2,
        MessageComponent = 3,
        ApplicationCommandAutocomplete = 4,
        ModalSubmit = 5
    }

    public enum InteractionResponseType
    {
        Pong = 1,
        ChannelMessageWithSource = 4,
        DeferredChannelMessageWithSource = 5,
        DeferredUpdateMessage = 6,
        UpdateMessage = 7,
        ApplicationCommandAutocompleteResult = 8,
        Modal = 9
    }
}
