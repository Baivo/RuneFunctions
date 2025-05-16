using System.Net;
using Newtonsoft.Json.Linq;

namespace RuneFunctions.Services;

public sealed class UrbanDictionaryService
{
    private readonly HttpClient _http;

    public UrbanDictionaryService(HttpClient http) => _http = http;

    // DTO for downstream use
    public record UrbanEntry(
        string Word,
        string Definition,
        string Example,
        string Permalink,
        int ThumbsUp,
        int ThumbsDown);

    public async Task<UrbanEntry?> GetDefinitionAsync(string term)
    {
        var endpoint = string.IsNullOrWhiteSpace(term)
            ? "https://api.urbandictionary.com/v0/random"
            : string.Format(
                  "https://api.urbandictionary.com/v0/define?term={0}",
                  WebUtility.UrlEncode(term.Trim()));

        var root = JObject.Parse(await _http.GetStringAsync(endpoint));

        if ((string?)root["result_type"] == "no_results")
            return null;

        var entry = ((JArray)root["list"]!)
            .OrderByDescending(t => (int)t["thumbs_up"]!)
            .First();

        return new UrbanEntry(
            Word: (string)entry["word"]!,
            Definition: (string)entry["definition"]!,
            Example: (string?)entry["example"] ?? string.Empty,
            Permalink: (string)entry["permalink"]!,
            ThumbsUp: (int)entry["thumbs_up"]!,
            ThumbsDown: (int)entry["thumbs_down"]!);
    }
}
