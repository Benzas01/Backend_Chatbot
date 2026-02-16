namespace BackendApp.Models
{
    public class User
    {
        public Guid Id { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation property
        public List<ChatMessage> ChatMessages { get; set; } = new();
    }
}
