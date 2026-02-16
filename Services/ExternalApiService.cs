using System.Threading.Tasks;
using System.Text.Json;
using System.Text;
using System.Net.Http.Headers;
using DotNetEnv;

namespace BackendApp.Services
{
    public interface IExternalApiService
    {
        Task<string> GetExternalMessageAsync(string inputMessage);
    }

    public class ExternalApiService : IExternalApiService
    {
        private readonly HttpClient _httpClient;

        public ExternalApiService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

       public async Task<string> GetExternalMessageAsync(string inputMessage)
        {
            try { Env.Load(); } catch { }
            var apiKey = Env.GetString("API_KEY", fallback: null) ?? Environment.GetEnvironmentVariable("API_KEY") ?? "";
            var payload = new
            {
                model = "gpt-4.1-mini",
                input = inputMessage,
                store = true
            };

            var json = JsonSerializer.Serialize(payload);

            using var client = new HttpClient();
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await client.PostAsync(
                "https://api.openai.com/v1/responses",
                content
            );

            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"OpenAI API error: {response.StatusCode}\n{responseBody}");
            }

            return responseBody;
        }

    }
}
