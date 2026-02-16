using BackendApp.Data;
using BackendApp.Models;
using Microsoft.EntityFrameworkCore;

namespace BackendApp.Services
{
    public interface IConversationService
    {
        Task AddMessageAsync(Guid userId, string role, string content);
        Task<List<ChatMessage>> GetHistoryAsync(Guid userId);
        Task<string> GetHistoryFormattedAsync(Guid userId, int limit = 10);
        Task ClearHistoryAsync(Guid userId);
    }

    public class ConversationService : IConversationService
    {
        private readonly AppDbContext _db;

        public ConversationService(AppDbContext db)
        {
            _db = db;
        }

        public async Task AddMessageAsync(Guid userId, string role, string content)
        {
            var message = new ChatMessage
            {
                UserId = userId,
                Role = role,
                Content = content,
                Timestamp = DateTime.UtcNow
            };

            _db.ChatMessages.Add(message);
            await _db.SaveChangesAsync();
        }

        public async Task<List<ChatMessage>> GetHistoryAsync(Guid userId)
        {
            return await _db.ChatMessages
                .Where(m => m.UserId == userId)
                .OrderBy(m => m.Timestamp)
                .ToListAsync();
        }

        public async Task<string> GetHistoryFormattedAsync(Guid userId, int limit = 10)
        {
            var history = await _db.ChatMessages
                .Where(m => m.UserId == userId)
                .OrderByDescending(m => m.Timestamp)
                .Take(limit)
                .ToListAsync();

            var formatted = string.Join("\n", history.Select(m => $"{m.Role}: {m.Content}"));
            return formatted;
        }

        public async Task ClearHistoryAsync(Guid userId)
        {
            var messages = await _db.ChatMessages
                .Where(m => m.UserId == userId)
                .ToListAsync();

            _db.ChatMessages.RemoveRange(messages);
            await _db.SaveChangesAsync();
        }
    }
}
