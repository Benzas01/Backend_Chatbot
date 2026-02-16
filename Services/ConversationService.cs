using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace BackendApp.Services
{
    public interface IConversationService
    {
        Task AddMessageAsync(string role, string content);
        Task<List<ConversationMessage>> GetHistoryAsync();
        Task<string> GetHistoryFormattedAsync(int limit = 10);
    }

    public class ConversationMessage
    {
        public string Role { get; set; }
        public string Content { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public class ConversationService : IConversationService
    {
        private readonly string _filePath = "conversation_history.json";
        private readonly object _lock = new object();

        public async Task AddMessageAsync(string role, string content)
        {
            var message = new ConversationMessage
            {
                Role = role,
                Content = content,
                Timestamp = DateTime.UtcNow
            };

            List<ConversationMessage> history;
            
            // Simple file-based persistence with locking for thread safety on file access broadly
            // Note: For high concurrency, a database is better.
            lock (_lock)
            {
                history = LoadHistoryInternal();
                history.Add(message);
                SaveHistoryInternal(history);
            }
            
            await Task.CompletedTask;
        }

        public async Task<List<ConversationMessage>> GetHistoryAsync()
        {
            lock (_lock)
            {
                return LoadHistoryInternal();
            }
        }

        public async Task<string> GetHistoryFormattedAsync(int limit = 10)
        {
            var history = await GetHistoryAsync();
            // Order by newest to oldest as requested
            var relevantHistory = history.OrderByDescending(m => m.Timestamp).Take(limit);

            var formatted = string.Join("\n", relevantHistory.Select(m => $"{m.Role}: {m.Content}"));
            return formatted;
        }

        private List<ConversationMessage> LoadHistoryInternal()
        {
            if (!File.Exists(_filePath))
            {
                return new List<ConversationMessage>();
            }

            try
            {
                var json = File.ReadAllText(_filePath);
                return JsonSerializer.Deserialize<List<ConversationMessage>>(json) ?? new List<ConversationMessage>();
            }
            catch
            {
                return new List<ConversationMessage>();
            }
        }

        private void SaveHistoryInternal(List<ConversationMessage> history)
        {
            var json = JsonSerializer.Serialize(history, new JsonSerializerOptions { WriteIndented = true });
            File.WriteAllText(_filePath, json);
        }
    }
}
