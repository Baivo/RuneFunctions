using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using System.Net;
using System.Text;
using System.Text.RegularExpressions;

namespace RuneFunctions.Functions
{
    public interface IUwUifyFunction
    {
        Task<HttpResponseData> Run(HttpRequestData req, FunctionContext ctx);
    }

    public class UwUifyFunction : IUwUifyFunction
    {
        // ─────────── static resources ───────────
        private static readonly Random _rng = new();

        private static readonly Regex[] _uwuMap =
        {
            new Regex("(?:r|l)", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex("n([aeiou])", RegexOptions.Compiled | RegexOptions.IgnoreCase),
            new Regex("ove",            RegexOptions.Compiled | RegexOptions.IgnoreCase)
        };
        private static readonly string[] _uwuReplace = { "w", "ny$1", "uv" };

        private static readonly Regex _punctuationRegex =
            new Regex("[?!]+$", RegexOptions.Compiled);

        private static readonly string[] _faces =
        {
            "(・`ω´・)", ";;w;;", "OwO", "UwU", ">w<", "^w^", "ÚwÚ", "^-^", ":3", "x3"
        };
        private static readonly string[] _actions =
        {
            "*blushes*", "*whispers to self*", "*cries*", "*screams*", "*sweats*", "*twerks*",
            "*runs away*", "*screeches*", "*walks away*", "*sees bulge*", "*looks at you*",
            "*notices buldge*", "*starts twerking*", "*huggles tightly*", "*boops your nose*"
        };
        private static readonly string[] _exclamations = { "!?", "?!!", "?!?1", "!!11", "?!?!" };

        // ─────────── HTTP trigger ───────────
        [Function("UwUify")]
        [OpenApiOperation("UwUify", tags: new[] { "UwUify" })]
        [OpenApiParameter("text", In = ParameterLocation.Query, Required = true, Type = typeof(string), Description = "Text to UwU-ify")]
        [OpenApiParameter("letterSwap", In = ParameterLocation.Query, Required = false, Type = typeof(double), Description = "Chance 0–1 to swap letters   (default 0.7)")]
        [OpenApiParameter("stutter", In = ParameterLocation.Query, Required = false, Type = typeof(double), Description = "Chance 0–1 to add stutter    (default 0.1)")]
        [OpenApiParameter("action", In = ParameterLocation.Query, Required = false, Type = typeof(double), Description = "Chance 0–1 to insert action  (default 0.05)")]
        [OpenApiParameter("face", In = ParameterLocation.Query, Required = false, Type = typeof(double), Description = "Chance 0–1 to insert face    (default 0.05)")]
        [OpenApiParameter("exclaim", In = ParameterLocation.Query, Required = false, Type = typeof(double), Description = "Chance 0–1 to remix punctuation (default 1.0)")]
        [OpenApiResponseWithBody(HttpStatusCode.OK, "application/json", typeof(string), Description = "UwU-ified text")]
        [OpenApiResponseWithoutBody(HttpStatusCode.BadRequest, Description = "Missing or invalid parameters")]
        public async Task<HttpResponseData> Run(
            [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "UwUify")]
            HttpRequestData req,
            FunctionContext ctx)
        {
            var log = ctx.GetLogger(nameof(UwUifyFunction));
            var qp = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
            string text = qp["text"];

            if (string.IsNullOrWhiteSpace(text))
                return await Bad("Invalid request: 'text' is required.");

            // parse & clamp
            double letterSwap = ParseChance(qp["letterSwap"], 0.7);
            double stutter = ParseChance(qp["stutter"], 0.1);
            double action = ParseChance(qp["action"], 0.05);
            double face = ParseChance(qp["face"], 0.05);
            double exclaim = ParseChance(qp["exclaim"], 1.0);

            log.LogInformation("UwUify called swap={s} st={st} ac={a} fc={f} ex={e}",
                               letterSwap, stutter, action, face, exclaim);

            var ok = req.CreateResponse(HttpStatusCode.OK);
            await ok.WriteAsJsonAsync(UwUify(text, letterSwap, stutter, action, face, exclaim));
            return ok;

            async Task<HttpResponseData> Bad(string msg)
            {
                var res = req.CreateResponse(HttpStatusCode.BadRequest);
                await res.WriteStringAsync(msg);
                return res;
            }
        }

        private static double ParseChance(string? raw, double fallback)
        {
            if (double.TryParse(raw, out double v) && v >= 0 && v <= 1) return v;
            return fallback;
        }

        // ─────────── UwU engine ───────────
        public static string UwUify(
            string input,
            double letterSwapChance = 0.7,
            double stutterChance = 0.1,
            double actionChance = 0.05,
            double faceChance = 0.05,
            double exclaimChance = 1.0)
        {
            if (string.IsNullOrWhiteSpace(input)) return input;

            var sb = new StringBuilder(input.Length * 2);
            var tokens = Regex.Split(input, @"(\s+)"); // keep original whitespace

            foreach (var tok in tokens)
            {
                if (string.IsNullOrWhiteSpace(tok))
                {
                    sb.Append(tok);
                    continue;
                }

                string word = tok;

                // letter swaps
                for (int i = 0; i < _uwuMap.Length; i++)
                    if (_rng.NextDouble() <= letterSwapChance)
                        word = _uwuMap[i].Replace(word, _uwuReplace[i]);

                // stutter
                if (_rng.NextDouble() < stutterChance)
                    word = $"{word[0]}-{word}";

                // punctuation remix (probability-gated)
                if (_punctuationRegex.IsMatch(word) && _rng.NextDouble() < exclaimChance)
                    word = _punctuationRegex.Replace(
                        word, _exclamations[_rng.Next(_exclamations.Length)]);

                sb.Append(word);

                // faces / actions
                double roll = _rng.NextDouble();
                if (roll < actionChance)
                    sb.Append(' ').Append(_actions[_rng.Next(_actions.Length)]);
                else if (roll < actionChance + faceChance)
                    sb.Append(' ').Append(_faces[_rng.Next(_faces.Length)]);
            }

            return sb.ToString();
        }
    }
}
