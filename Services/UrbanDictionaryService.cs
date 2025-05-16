using Newtonsoft.Json.Linq;

namespace RuneFunctions.Services
{
    /// <summary>
    /// Queries Urban Dictionary for definitions
    /// </summary>
    public class UrbanDictionaryService
    {
        private readonly HttpClient _http;
        private readonly Random _rng = new();

        public UrbanDictionaryService(HttpClient http) => _http = http;

        public async Task<string?> GetDefinitionAsync(string term)
        {
            string endpoint = string.IsNullOrWhiteSpace(term)
                ? "https://api.urbandictionary.com/v0/random"
                : $"https://api.urbandictionary.com/v0/define?term={WebUtility.UrlEncode(term.Trim())}";

            string json = await _http.GetStringAsync(endpoint).ConfigureAwait(false);
            JObject root = JObject.Parse(json);

            if ((string)root["result_type"] == "no_results") return null;

            JArray list = (JArray)root["list"]!;
            JObject entry = (JObject)list[_rng.Next(list.Count)];

            string definition = entry.Value<string>("definition")!;
            if (definition.Length > 500) definition = definition[..500] + "...";

            string example = entry.Value<string>("example") ?? "*No example provided*";

            var payload = new
            {
                title = entry.Value<string>("word"),
                description = $"*{example}*",
                url = entry.Value<string>("permalink"),
                fields = new[]
                {
                    new { name = "Definition", value = definition }
                }
            };

            return JsonConvert.SerializeObject(payload, Formatting.Indented);
        }
    }
}
