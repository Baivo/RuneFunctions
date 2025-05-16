namespace RuneFunctions
{
    public static class EnvironmentVariables
    {
        public static string? TableConnectionString = Environment.GetEnvironmentVariable("TableConnectionString") ?? null;
        public static string? BlobConnectionString = Environment.GetEnvironmentVariable("BlobConnectionString") ?? null;
        public static string? OpenAiApiKey = Environment.GetEnvironmentVariable("OpenAiApiKey") ?? null;
        public static string? FunctionsBaseUrl = Environment.GetEnvironmentVariable("FunctionsBaseUrl") ?? null;
    }
}
