using Org.BouncyCastle.Crypto.Parameters;
using Org.BouncyCastle.Utilities.Encoders;

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
            object interactionResponse;
            switch (interaction.Type)
            {
                case InteractionType.Ping:
                    interactionResponse = HandlePing(interaction);
                    break;
                case InteractionType.ApplicationCommand:
                    interactionResponse = HandleApplicationCommand(interaction);
                    break;
                case InteractionType.MessageComponent:
                    interactionResponse = HandleMessageComponent(interaction);
                    break;
                case InteractionType.ModalSubmit:
                    interactionResponse = HandleModalSubmit(interaction);
                    break;
                case InteractionType.ApplicationCommandAutocomplete:
                    interactionResponse = HandleApplicationCommandAutocomplete(interaction);
                    break;
                default:
                    interactionResponse = HandleUnknownInteraction(interaction);
                    break;
            }

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
        private static object HandleApplicationCommand(DiscordInteraction interaction)
        {
            InteractionData data = interaction.Data;


            StringBuilder sb = new();
            sb.AppendLine("Name: " + interaction.Data.Name);
            sb.AppendLine("ID: " + interaction.Data.Id);
            sb.AppendLine("Data: " + data);
            return new
            {
                type = InteractionResponseType.ChannelMessageWithSource,
                data = new
                {
                    content = sb.ToString(),
                }
            };

        }
        private static object HandlePing(DiscordInteraction interaction) 
        {
            return new { type = InteractionResponseType.Pong };
        }
        private static object HandleMessageComponent(DiscordInteraction interaction)
        {
            return new
            {
                type = InteractionResponseType.ChannelMessageWithSource,
                data = new
                {
                    content = "Received a message component interaction!"
                }
            };
        }
        private static object HandleModalSubmit(DiscordInteraction interaction)
        {
            return new
            {
                type = InteractionResponseType.ChannelMessageWithSource,
                data = new
                {
                    content = "Received a modal submit interaction!"
                }
            };
        }
        private static object HandleApplicationCommandAutocomplete(DiscordInteraction interaction)
        {
            return new
            {
                type = InteractionResponseType.ApplicationCommandAutocompleteResult,
                data = new
                {
                    choices = new[]
                    {
                            new { name = "Choice 1", value = "choice_1" },
                            new { name = "Choice 2", value = "choice_2" }
                        }
                }
            };
        }
        private static object HandleUnknownInteraction(DiscordInteraction interaction)
        {
            return new
            {
                type = InteractionResponseType.ChannelMessageWithSource,
                data = new
                {
                    content = "Received an unknown interaction!"
                }
            };
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
