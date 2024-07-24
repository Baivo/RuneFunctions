using System.Text.RegularExpressions;
using HtmlAgilityPack;
using Microsoft.OpenApi.Models;
using PuppeteerSharp;

namespace RuneFunctions
{
    public class WebScraperFunction
    {
        private static readonly HttpClient httpClient = new HttpClient();

        static WebScraperFunction()
        {
            httpClient.Timeout = TimeSpan.FromMinutes(1);
        }

        private readonly ILogger _logger;

        public WebScraperFunction(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory.CreateLogger<WebScraperFunction>();
        }

        [Function("ScrapeWebContent")]
        [OpenApiOperation(operationId: "ScrapeWebContent", tags: new[] { "webscraper" })]
        [OpenApiParameter(name: "url", In = ParameterLocation.Query, Required = true, Type = typeof(string), Description = "URL to scrape")]
        [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "text/plain", bodyType: typeof(string), Description = "The scraped main content response")]
        [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.BadRequest, Description = "Invalid request")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "ScrapeWebContent")] HttpRequestData req,
            FunctionContext executionContext)
        {
            _logger.LogInformation("ScrapeWebContent function request received.");

            var query = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
            var url = query["url"];

            if (string.IsNullOrEmpty(url))
            {
                var badRequestResponse = req.CreateResponse(HttpStatusCode.BadRequest);
                await badRequestResponse.WriteStringAsync("Invalid request: 'url' is required.");
                return badRequestResponse;
            }

            try
            {
                string mainContent = await GetMainContentAsync(url);
                if (mainContent == null)
                {
                    var errorResponse = req.CreateResponse(HttpStatusCode.InternalServerError);
                    await errorResponse.WriteStringAsync("Failed to scrape content.");
                    return errorResponse;
                }

                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteStringAsync(mainContent);
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

        public static async Task<string> GetMainContentAsync(string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                throw new ArgumentException("URL cannot be null or empty", nameof(url));
            }

            try
            {
                // Using Puppeteer to handle dynamic content
                string htmlContent = await GetHtmlContentWithPuppeteer(url);
                if (string.IsNullOrEmpty(htmlContent))
                {
                    HttpResponseMessage response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseContentRead);
                    response.EnsureSuccessStatusCode();
                    htmlContent = await response.Content.ReadAsStringAsync();
                }

                string mainContent = ExtractMainContent(htmlContent);
                return mainContent;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
                return null;
            }
        }

        private static async Task<string> GetHtmlContentWithPuppeteer(string url)
        {
            try
            {
                var options = new BrowserFetcherOptions
                {
                    Path = Path.GetTempPath(),
                };
                var browserFetcher = new BrowserFetcher(options);
                var revision = await browserFetcher.DownloadAsync();

                await using var browser = await Puppeteer.LaunchAsync(new LaunchOptions { Headless = true });
                var page = await browser.NewPageAsync();
                await page.SetUserAgentAsync("Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/58.0.3029.110 Safari/537.3");
                await page.SetViewportAsync(new ViewPortOptions { Width = 1280, Height = 800 });
                await page.GoToAsync(url, WaitUntilNavigation.Networkidle2);
                await Task.Delay(500); // Increase delay to ensure dynamic content loads
                string content = await page.GetContentAsync();
                await browser.CloseAsync();
                return content;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Puppeteer Error: {ex.Message}");
                return null;
            }
        }

        private static string ExtractMainContent(string html)
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            // Remove unnecessary nodes
            RemoveUnnecessaryNodes(doc);

            // Attempt to find all relevant content nodes
            var mainContentNodes = FindMainContentNodes(doc);
            if (mainContentNodes == null || mainContentNodes.Count == 0)
            {
                return string.Empty;
            }

            var combinedText = string.Join(" ", mainContentNodes.Select(node => CleanText(node.InnerHtml)));
            return combinedText;
        }

        private static void RemoveUnnecessaryNodes(HtmlDocument doc)
        {
            var unnecessaryTags = new[] { "script", "style", "header", "footer", "nav", "aside" };
            foreach (var tag in unnecessaryTags)
            {
                var nodes = doc.DocumentNode.Descendants(tag).ToArray();
                foreach (var node in nodes)
                {
                    node.Remove();
                }
            }

            // Remove comments
            var comments = doc.DocumentNode.SelectNodes("//comment()");
            if (comments != null)
            {
                foreach (var comment in comments)
                {
                    comment.Remove();
                }
            }
        }

        private static HtmlNodeCollection FindMainContentNodes(HtmlDocument doc)
        {
            var contentNodes = doc.DocumentNode.SelectNodes("//main | //article | //div[contains(@class,'content')] | //div[contains(@class,'post')] | //div[contains(@id,'main')] | //div[contains(@class,'entry')]");

            if (contentNodes == null || contentNodes.Count == 0)
            {
                // Heuristic approach to find content nodes
                var potentialNodes = doc.DocumentNode.SelectNodes("//div | //section");
                if (potentialNodes != null)
                {
                    contentNodes = new HtmlNodeCollection(null);
                    foreach (var node in potentialNodes
                        .Where(node => node.InnerText.Length > 200) // Filter nodes with sufficient text length
                        .OrderByDescending(node => TextDensity(node))
                        .Take(10)) // Take the top 10 dense nodes to avoid picking too many irrelevant sections
                    {
                        contentNodes.Add(node);
                    }
                }
            }

            return contentNodes;
        }

        private static double TextDensity(HtmlNode node)
        {
            int textLength = node.InnerText.Length;
            int childCount = node.Descendants().Count();
            return childCount > 0 ? (double)textLength / childCount : 0;
        }

        private static string CleanText(string html)
        {
            var doc = new HtmlDocument();
            doc.LoadHtml(html);

            foreach (var script in doc.DocumentNode.Descendants("script").ToArray())
                script.Remove();
            foreach (var style in doc.DocumentNode.Descendants("style").ToArray())
                style.Remove();

            var nodesWithAttributes = doc.DocumentNode.SelectNodes("//*[@onclick or @onmouseover or @style]");
            if (nodesWithAttributes != null)
            {
                foreach (var node in nodesWithAttributes)
                    node.Attributes.Remove("onclick");
                foreach (var node in nodesWithAttributes)
                    node.Attributes.Remove("style");
            }

            string cleanedText = System.Net.WebUtility.HtmlDecode(doc.DocumentNode.InnerText);
            cleanedText = Regex.Replace(cleanedText, @"\s+", " ").Trim();

            return cleanedText;
        }
    }

    public class WebScraperResponse
    {
        public string MainContent { get; set; }
    }

    public static class HtmlNodeCollectionExtensions
    {
        public static HtmlNodeCollection ToHtmlNodeCollection(this IEnumerable<HtmlNode> nodes)
        {
            var collection = new HtmlNodeCollection(null);
            foreach (var node in nodes)
            {
                collection.Add(node);
            }
            return collection;
        }
    }
}
