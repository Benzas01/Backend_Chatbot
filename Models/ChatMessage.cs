namespace BackendApp.Models
{
    public class ChatMessage
    {
        public int Id { get; set; }
        public Guid UserId { get; set; }
        public string Role { get; set; } = string.Empty;
        public string Content { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        // Navigation property
        public User User { get; set; } = null!;
    }
}
