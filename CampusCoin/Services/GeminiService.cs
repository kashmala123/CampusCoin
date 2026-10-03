using System.Text;
using System.Text.Json;

namespace CampusCoin.Services
{
    public class GeminiService
    {
        private readonly HttpClient _http;
        private readonly string _apiKey;
        private readonly ILogger<GeminiService> _logger;

        // Current models (older 1.5 / 2.0 names return 404 for new keys)
        private static readonly string[] Models = new[]
        {
            "gemini-3.8-flash",
            "gemini-flash-latest",
            "gemini-3.7-flash",
            "gemini-3.6-flash",
            "gemini-3.5-flash",
            "gemini-pro-latest",
            "gemini-2.5-flash"
        };

        public GeminiService(HttpClient http, IConfiguration config, ILogger<GeminiService> logger)
        {
            _http = http;
            _apiKey = (config["Gemini:ApiKey"] ?? "").Trim();
            _logger = logger;
            if (_http.Timeout < TimeSpan.FromSeconds(30))
                _http.Timeout = TimeSpan.FromSeconds(60);
        }

        public async Task<string?> AskAsync(string systemPrompt, string userMessage)
        {
            if (string.IsNullOrWhiteSpace(_apiKey))
            {
                _logger.LogWarning("Gemini API key is missing");
                return null;
            }

            foreach (var model in Models)
            {
                try
                {
                    var reply = await CallModelAsync(model, systemPrompt, userMessage);
                    if (!string.IsNullOrWhiteSpace(reply))
                    {
                        _logger.LogInformation("Gemini replied via {Model}", model);
                        return reply;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Gemini model {Model} failed", model);
                }
            }

            _logger.LogWarning("All Gemini models failed; AI Coach will use local fallback.");
            return null;
        }

        private async Task<string?> CallModelAsync(string model, string systemPrompt, string userMessage)
        {
            var requestBody = new
            {
                system_instruction = new
                {
                    parts = new[] { new { text = systemPrompt ?? "" } }
                },
                contents = new[]
                {
                    new
                    {
                        role = "user",
                        parts = new[] { new { text = userMessage ?? "" } }
                    }
                },
                generationConfig = new
                {
                    temperature = 0.7,
                    topK = 40,
                    topP = 0.95,
                    maxOutputTokens = 1024
                }
            };

            var json = JsonSerializer.Serialize(requestBody);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{model}:generateContent?key={Uri.EscapeDataString(_apiKey)}";
            using var response = await _http.PostAsync(url, content);
            var responseJson = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Gemini {Model} error: {Status} - {Err}", model, response.StatusCode,
                    responseJson.Length > 400 ? responseJson[..400] : responseJson);
                return null;
            }

            using var doc = JsonDocument.Parse(responseJson);
            if (!doc.RootElement.TryGetProperty("candidates", out var candidates) || candidates.GetArrayLength() == 0)
            {
                _logger.LogWarning("Gemini {Model}: no candidates in response", model);
                return null;
            }

            var first = candidates[0];
            if (!first.TryGetProperty("content", out var contentEl))
                return null;
            if (!contentEl.TryGetProperty("parts", out var parts) || parts.GetArrayLength() == 0)
                return null;

            return parts[0].TryGetProperty("text", out var textEl) ? textEl.GetString() : null;
        }
    }
}
