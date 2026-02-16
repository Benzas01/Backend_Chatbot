using BackendApp.Models;
using Microsoft.EntityFrameworkCore;

namespace BackendApp.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

        public DbSet<User> Users { get; set; }
        public DbSet<ChatMessage> ChatMessages { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<User>(entity =>
            {
                entity.HasKey(u => u.Id);
                entity.Property(u => u.CreatedAt).IsRequired();
            });

            modelBuilder.Entity<ChatMessage>(entity =>
            {
                entity.HasKey(m => m.Id);
                entity.Property(m => m.Id).ValueGeneratedOnAdd();
                entity.Property(m => m.Role).IsRequired().HasMaxLength(50);
                entity.Property(m => m.Content).IsRequired().HasColumnType("LONGTEXT");
                entity.Property(m => m.Timestamp).IsRequired();

                entity.HasOne(m => m.User)
                      .WithMany(u => u.ChatMessages)
                      .HasForeignKey(m => m.UserId)
                      .OnDelete(DeleteBehavior.Cascade);
            });
        }
    }
}
