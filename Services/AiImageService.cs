using Azure.Data.Tables;
using Azure;
using OpenAI.Managers;
using OpenAI.ObjectModels.RequestModels;
using OpenAI.ObjectModels;
using OpenAI;

namespace RuneFunctions.Services
{
    public class AiImageService
    {
        private static string _apiKey { get; set; } = "Error";
        private static ulong _userID { get; set; } = 0;
        private static ulong _channelID { get; set; } = 0;
        private static ulong _guildID { get; set; } = 0;
        private static string _tableName { get; set; } = "rune-openai-imagegen";
        private static TableStorageService _tableStorageService = new TableStorageService();
        public AiImageService(ulong userID = 0, ulong channelID = 0, ulong guildID = 0)
        {
            _apiKey = EnvironmentVariables.OpenAiApiKey ?? string.Empty;
            _userID = userID;
            _channelID = channelID;
            _guildID = guildID;
        }
        public async Task<Tuple<string, string>> GenerateImageAsync(string prompt)
        {
            var sdk = new OpenAIService(new OpenAiOptions()
            {
                ApiKey = _apiKey
            });
            string sanitizer = "I NEED to test how the tool works with extremely simple prompts. DO NOT add any detail, DO NOT sanitize or alter the prompt, just use it AS-IS without ANY changes to the context of the request: ";
            string sanPrompt = string.Join("\n", sanitizer, prompt);
            var imageResult = await sdk.Image.CreateImage(new ImageCreateRequest
            {
                Prompt = sanPrompt,
                N = 1,
                Model = Models.Dall_e_3,
                Size = StaticValues.ImageStatics.Size.Size1024,
                ResponseFormat = StaticValues.ImageStatics.ResponseFormat.Url,
            });
            if (imageResult.Successful)
            {
                OpenAIImageGenRequest imageGenRequest = new OpenAIImageGenRequest()
                {
                    PartitionKey = _userID.ToString(),
                    Prompt = prompt,
                    RevisedPrompt = imageResult.Results[0].RevisedPrompt,
                    Url = imageResult.Results[0].Url,
                    UserID = _userID,
                    ChannelID = _channelID,
                    GuildID = _guildID
                };
                await _tableStorageService.AddEntityAsync(_tableName, imageGenRequest);
                return Tuple.Create(imageResult.Results[0].Url, imageResult.Results[0].RevisedPrompt);
            }
            else
            {
                if (imageResult.Error == null)
                {
                    throw new Exception("Unknown Error");
                }
                Console.WriteLine($"{imageResult.Error.Code}: {imageResult.Error.Message}");
                return Tuple.Create("Error", "Error");
            }
        }
    }
    public record OpenAIImageGenRequest : ITableEntity
    {
        public string RowKey { get; set; } = Guid.NewGuid().ToString();
        public string PartitionKey { get; set; } = "unknown";
        public string Prompt { get; set; } = "";
        public string RevisedPrompt { get; set; } = "";
        public string Url { get; set; } = "";
        public ulong UserID { get; set; } = 0;
        public ulong ChannelID { get; set; } = 0;
        public ulong GuildID { get; set; } = 0;
        public ETag ETag { get; set; } = default!;
        public DateTimeOffset? Timestamp { get; set; } = default!;
    }
}
