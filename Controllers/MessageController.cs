using BackendApp.Data;
using BackendApp.Models;
using BackendApp.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace BackendApp.Controllers
{
    public class ApiResponse
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("object")]
        public string Object { get; set; }

        [JsonPropertyName("created_at")]
        public long CreatedAt { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; }

        [JsonPropertyName("background")]
        public bool Background { get; set; }

        [JsonPropertyName("billing")]
        public Billing Billing { get; set; }

        [JsonPropertyName("completed_at")]
        public long? CompletedAt { get; set; }

        [JsonPropertyName("error")]
        public object Error { get; set; }

        [JsonPropertyName("frequency_penalty")]
        public double FrequencyPenalty { get; set; }

        [JsonPropertyName("incomplete_details")]
        public object IncompleteDetails { get; set; }

        [JsonPropertyName("instructions")]
        public object Instructions { get; set; }

        [JsonPropertyName("max_output_tokens")]
        public int? MaxOutputTokens { get; set; }

        [JsonPropertyName("max_tool_calls")]
        public int? MaxToolCalls { get; set; }

        [JsonPropertyName("model")]
        public string Model { get; set; }

        [JsonPropertyName("output")]
        public List<OutputItem> Output { get; set; }

        [JsonPropertyName("parallel_tool_calls")]
        public bool ParallelToolCalls { get; set; }

        [JsonPropertyName("presence_penalty")]
        public double PresencePenalty { get; set; }

        [JsonPropertyName("previous_response_id")]
        public object PreviousResponseId { get; set; }

        [JsonPropertyName("prompt_cache_key")]
        public object PromptCacheKey { get; set; }

        [JsonPropertyName("prompt_cache_retention")]
        public object PromptCacheRetention { get; set; }

        [JsonPropertyName("reasoning")]
        public Reasoning Reasoning { get; set; }

        [JsonPropertyName("safety_identifier")]
        public object SafetyIdentifier { get; set; }

        [JsonPropertyName("service_tier")]
        public string ServiceTier { get; set; }

        [JsonPropertyName("store")]
        public bool Store { get; set; }

        [JsonPropertyName("temperature")]
        public double Temperature { get; set; }

        [JsonPropertyName("text")]
        public Text Text { get; set; }

        [JsonPropertyName("tool_choice")]
        public string ToolChoice { get; set; }

        [JsonPropertyName("tools")]
        public List<object> Tools { get; set; }

        [JsonPropertyName("top_logprobs")]
        public int TopLogprobs { get; set; }

        [JsonPropertyName("top_p")]
        public double TopP { get; set; }

        [JsonPropertyName("truncation")]
        public string Truncation { get; set; }

        [JsonPropertyName("usage")]
        public Usage Usage { get; set; }

        [JsonPropertyName("user")]
        public object User { get; set; }

        [JsonPropertyName("metadata")]
        public Dictionary<string, object> Metadata { get; set; }
    }

    public class Billing
    {
        [JsonPropertyName("payer")]
        public string Payer { get; set; }
    }

    public class OutputItem
    {
        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("status")]
        public string Status { get; set; }

        [JsonPropertyName("content")]
        public List<ContentItem> Content { get; set; }

        [JsonPropertyName("role")]
        public string Role { get; set; }
    }

    public class ContentItem
    {
        [JsonPropertyName("type")]
        public string Type { get; set; }

        [JsonPropertyName("annotations")]
        public List<object> Annotations { get; set; }

        [JsonPropertyName("logprobs")]
        public List<object> Logprobs { get; set; }

        [JsonPropertyName("text")]
        public string Text { get; set; }
    }

    public class Reasoning
    {
        [JsonPropertyName("effort")]
        public object Effort { get; set; }

        [JsonPropertyName("summary")]
        public object Summary { get; set; }
    }

    public class Text
    {
        [JsonPropertyName("format")]
        public Format Format { get; set; }

        [JsonPropertyName("verbosity")]
        public string Verbosity { get; set; }
    }

    public class Format
    {
        [JsonPropertyName("type")]
        public string Type { get; set; }
    }

    public class Usage
    {
        [JsonPropertyName("input_tokens")]
        public int InputTokens { get; set; }

        [JsonPropertyName("input_tokens_details")]
        public InputTokensDetails InputTokensDetails { get; set; }

        [JsonPropertyName("output_tokens")]
        public int OutputTokens { get; set; }

        [JsonPropertyName("output_tokens_details")]
        public OutputTokensDetails OutputTokensDetails { get; set; }

        [JsonPropertyName("total_tokens")]
        public int TotalTokens { get; set; }
    }

    public class InputTokensDetails
    {
        [JsonPropertyName("cached_tokens")]
        public int CachedTokens { get; set; }
    }

    public class OutputTokensDetails
    {
        [JsonPropertyName("reasoning_tokens")]
        public int ReasoningTokens { get; set; }
    }
    [ApiController]
    [Route("api/[controller]")]
    public class MessageController : ControllerBase
    {
        private readonly IExternalApiService _externalApiService;
        private readonly IConversationService _conversationService;
        private readonly AppDbContext _db;
        private const string UserCookieName = "UserId";

        public MessageController(IExternalApiService externalApiService, IConversationService conversationService, AppDbContext db)
        {
            _externalApiService = externalApiService;
            _conversationService = conversationService;
            _db = db;
        }

        public class MessageRequest
        {
            public string Content { get; set; }
        }

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] MessageRequest request)
        {
            // 0. Resolve User from cookie
            var userId = await ResolveUserIdAsync();

            // 1. Get History
            string history = await _conversationService.GetHistoryFormattedAsync(userId);

            // 2. Inject Prompt with History and User Content
            var processedRequest = injectprompt(request, history);
            
            // 3. Call API
            var jsonResult = await _externalApiService.GetExternalMessageAsync(processedRequest.Content);

            // Make sure jsonResult is the full JSON string (starts with '{' and ends with '}')
            if (string.IsNullOrWhiteSpace(jsonResult) || !jsonResult.TrimStart().StartsWith("{"))
            {
                Console.WriteLine("API did not return valid JSON.");
                return BadRequest();
            }

            // Deserialize into the ApiResponse class
            var response = JsonSerializer.Deserialize<ApiResponse>(jsonResult);

            // Get the latest assistant message text
            var latestText = response?.Output?
                .LastOrDefault()?
                .Content?
                .Where(c => c.Type == "output_text")
                .Select(c => c.Text)
                .FirstOrDefault();

            Console.WriteLine(latestText);

            // 4. Save History (User message and AI response)
            if (!string.IsNullOrEmpty(latestText))
            {
                await _conversationService.AddMessageAsync(userId, "User", request.Content);
                await _conversationService.AddMessageAsync(userId, "AI", latestText);
            }

            return Ok(new { response = latestText });
        }

        public MessageRequest injectprompt(MessageRequest request, string history)
        {
            string prompt = System.IO.File.ReadAllText("Prompt.txt");
            prompt = prompt.Replace("{history}", history);
            prompt = prompt.Replace("{user.text}", request.Content);
            MessageRequest r = new MessageRequest { Content = prompt };
            return r;
        }

        private async Task<Guid> ResolveUserIdAsync()
        {
            var cookieValue = Request.Cookies[UserCookieName];

            if (!string.IsNullOrEmpty(cookieValue) && Guid.TryParse(cookieValue, out var existingId))
            {
                var exists = await _db.Users.AnyAsync(u => u.Id == existingId);
                if (exists)
                    return existingId;
            }

            // Create a new user
            var newUser = new User
            {
                Id = Guid.NewGuid(),
                CreatedAt = DateTime.UtcNow
            };

            _db.Users.Add(newUser);
            await _db.SaveChangesAsync();

            Response.Cookies.Append(UserCookieName, newUser.Id.ToString(), new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.None,
                Expires = DateTimeOffset.UtcNow.AddYears(1),
                Path = "/"
            });

            return newUser.Id;
        }
    }
}
