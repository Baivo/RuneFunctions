using OpenAI.Managers;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using OpenAI.ObjectModels.RequestModels;
using OpenAI.ObjectModels;
using OpenAI;
namespace RuneFunctions.Services
{
    public class WebSummary
    {
        private static OpenAIService _aiService = new OpenAIService(new OpenAiOptions()
        {
            ApiKey = EnvironmentVariables.OpenAiApiKey,
        });
        public static List<string> Blacklist = new List<string> { "discordapp", "tenor",
                    "twitter", "https://x.", "youtube", "twitch", "facebook", "instagram",
                    "imgur", "pornhub", "xnxx", "xvideos", "xhamster", "chaturbate",
                    "onlyfans", "patreon", "tiktok", "youtu.be"};
        public static async Task GetWebSummaryAsync(string content)
        {
            try
            {
                var summaryMessages = GetSummaryMessages(content);
                var summaryResult = await _aiService.CreateCompletion(new ChatCompletionCreateRequest()
                {
                    Messages = summaryMessages,
                    Model = Models.Gpt_4o,
                });

                if ((summaryResult.Choices.First().Message.Content?.ToLower() ?? string.Empty) == "invalid")
                {
                    Console.WriteLine("Invalid content");
                    //continue;
                }
            }
            catch (JsonReaderException ex)
            {

                Console.WriteLine($"JSON Reader Exception: {ex.Message}");
                Console.WriteLine($"Path: {ex.Path}, Line: {ex.LineNumber}, Position: {ex.LinePosition}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"General Exception: {ex.Message}");
            }

        }
        public static bool CheckForURLs(string input, List<string> blacklist)
        {
            // Regular expression to find URLs
            string urlPattern = @"https?://[^\s]+";
            var matches = Regex.Matches(input, urlPattern);

            // If no URLs are found, return false
            if (matches.Count == 0)
                return false;

            foreach (Match match in matches)
            {
                string url = match.Value;
                Console.WriteLine("URL found: " + url);  // Diagnostic output

                bool isBlacklisted = false;

                foreach (string term in blacklist)
                {
                    if (url.Contains(term, StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine("Blacklisted term found: " + term);  // Diagnostic output
                        isBlacklisted = true;
                        break;
                    }
                }

                if (!isBlacklisted)
                    return true;
            }

            // If all URLs are blacklisted or there were no URLs, return false
            return false;
        }
        private static List<string> UrlsFromString(string input)
        {
            return input.Split("\t\n ".ToCharArray(), StringSplitOptions.RemoveEmptyEntries)
                        .Where(x => x.StartsWith("http://") || x.StartsWith("www.") || x.StartsWith("https://"))
                        .ToList();
        }
        public static List<ChatMessage> GetSummaryMessages(string message, string sourceURL = "")
        {
            List<ChatMessage> messages = new List<ChatMessage>();
            messages.Add(ChatMessage.FromUser("Provide a summary of this article, aiming for around two standard twitter tweet in length (don't style it as a tweet, it's a TL;DR summary of the article).  Do not provide anything other than the summary e.g. don't preface the response with 'this article blah blah' etc., aim to impartially represent the article's content as a summary, including all crucial detail and substance. If you believe it appears something has gone wrong (maybe you got bunk, blank or paywall related text instead, simply respond with the word INVALID in all caps, and end it there). You may be provided with content other than the information that requires summary, such as information on the site itself that is consistent across pages, privacy policy/naviation elements or other such content. You should ignore this as it's an indication that the webscraper and the cleaning up function thereafter has missed it's job a bit, focus on providing a summary of the content that you identify as the target for summary."));
            messages.Add(ChatMessage.FromUser("Article: " + message));
            messages.Add(ChatMessage.FromUser("Source URL: " + sourceURL + "\n(Don't mention this in your summary, but use it to inform your decision on how to respond to the summary request e.g. Wikipedia URLs would always be safe to summarize, even if it's a subject that might otherwise not be, whereas lack of any text content or content that appears as though the web scraper has failed or been blocked by a paywall should result with you replying only with the INVALID which will cancel the request etc. Use your best judgement, and note that this service is only available after age verification.). Summary text should be written as a direct summarization, and not contain any pre-amble or post-amble text, just the summary of the article's content."));
            return messages;
        }
        public static List<ChatMessage> GetUrls(string message)
        {
            List<ChatMessage> messages = new List<ChatMessage>();
            messages.Add(ChatMessage.FromUser("Find all the URL's in this message and reply with them in json format."));

            messages.Add(ChatMessage.FromUser(message));
            messages.Add(ChatMessage.FromUser("Answer in a json format with key value pairs of the url and a bool true or false" +
                " where true indicates it's worth scraping the website at that link and extracting the text of the content for viewing," +
                " and false if it's probably not worth it such as in the case where the url ends in a media type like .png or .mp4 and wouldn't be scrapable etc. Your response in it's entirety will be fed to a program to act on the response," +
                " so do not provide anything but the json payload please."));
            return messages;
        }
    }
}