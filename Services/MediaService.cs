

namespace RuneFunctions.Services
{
    public class MediaService
    {
        public static async Task<MemoryStream> UrlToMemoryStreamAsync(string url)
        {
            using (var client = new HttpClient())
            {
                var response = await client.GetAsync(url);
                response.EnsureSuccessStatusCode();
                var byteArray = await response.Content.ReadAsByteArrayAsync();
                var stream = new MemoryStream(byteArray);
                return stream;
            }
        }
        public static async Task<MemoryStream> UrlToByteArrayAsync(string url)
        {
            using (var client = new HttpClient())
            {
                var response = await client.GetAsync(url);
                response.EnsureSuccessStatusCode();
                var byteArray = await response.Content.ReadAsByteArrayAsync();
                var stream = new MemoryStream(byteArray);
                return stream;
            }
        }
        
    }
}
