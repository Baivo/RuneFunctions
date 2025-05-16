using PuppeteerSharp;
using System.Collections.Concurrent;

namespace RuneFunctions.Services
{
    public class WebDriverPool
    {
        private readonly ConcurrentBag<IBrowser> _browserPool;
        private readonly int _maxSize;
        private int _currentSize;
        private readonly ILogger<WebDriverPool> _logger;

        public WebDriverPool(int maxSize, ILogger<WebDriverPool> logger)
        {
            _browserPool = new ConcurrentBag<IBrowser>();
            _maxSize = maxSize;
            _currentSize = 0;
            _logger = logger;
        }

        public async Task<IBrowser> AcquireAsync()
        {
            IBrowser browser;

            if (!_browserPool.TryTake(out browser))
            {
                if (Interlocked.Increment(ref _currentSize) <= _maxSize)
                {
                    browser = await CreateBrowserAsync();
                }
                else
                {
                    Interlocked.Decrement(ref _currentSize);

                    while (!_browserPool.TryTake(out browser))
                    {
                        await Task.Delay(TimeSpan.FromMilliseconds(100));
                    }
                }
            }

            return browser;
        }

        public void Release(IBrowser browser)
        {
            _browserPool.Add(browser);
        }

        public async Task<IBrowser> CreateBrowserAsync()
        {
            var browserFetcher = new BrowserFetcher();
            var installedBrowser = await browserFetcher.DownloadAsync();

            var launchOptions = new LaunchOptions
            {
                Headless = true,
                Args = new[] { "--no-sandbox", "--disable-setuid-sandbox" }
            };

            _logger.LogInformation("Launching Chrome browser");

            var browser = await Puppeteer.LaunchAsync(launchOptions);

            _logger.LogInformation("Chrome browser launched successfully");

            return browser;
        }
    }
}
