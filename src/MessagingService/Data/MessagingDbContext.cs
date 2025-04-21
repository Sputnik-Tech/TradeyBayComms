using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MessagingService.Data
{
    // --- DbContext ---
    public class MessagingDbContext : DbContext
    {
        public MessagingDbContext(DbContextOptions<MessagingDbContext> options) : base(options) { }

        public DbSet<Chat> Chats { get; set; }
        public DbSet<ChatMessage> Messages { get; set; }
        public DbSet<ChatParticipant> ChatParticipants { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // --- Keys ---
            modelBuilder.Entity<Chat>().HasKey(c => c.ChatId);
            modelBuilder.Entity<ChatMessage>().HasKey(m => m.MessageId);
            // Composite Key using the new INT UserId type
            modelBuilder.Entity<ChatParticipant>().HasKey(cp => new { cp.ChatId, cp.UserId });

            // --- Relationships ---
            modelBuilder.Entity<ChatParticipant>()
                .HasOne(cp => cp.Chat)
                .WithMany(c => c.Participants)
                .HasForeignKey(cp => cp.ChatId);

            modelBuilder.Entity<ChatMessage>()
                .HasOne(m => m.Chat)
                .WithMany(c => c.Messages)
                .HasForeignKey(m => m.ChatId);

            // --- Indexes ---
            modelBuilder.Entity<Chat>()
                .HasIndex(c => c.ListingId); // Index for finding chats by listing
            modelBuilder.Entity<ChatMessage>()
                .HasIndex(m => new { m.ChatId, m.Timestamp }) // Index for fetching history
                .IsDescending(false, true); // ChatId ascending, Timestamp descending
            // Index on the new INT UserId type
            modelBuilder.Entity<ChatParticipant>()
                 .HasIndex(cp => cp.UserId); // Index for finding chats for a user

            // --- Property Mappings ---
            modelBuilder
                .Entity<ChatMessage>()
                .Property(e => e.MessageType)
                .HasConversion<string>() // Store enum as string (VARCHAR)
                .HasMaxLength(50); // Set appropriate length

            // No MaxLength needed for INT SenderUserId or UserId

            // Still assuming SenderActingAsBusinessId is a string representation
            // of the Business ID from Auth Service. Adjust MaxLength if needed.
            modelBuilder
                .Entity<ChatMessage>()
                .Property(m => m.SenderActingAsBusinessId)
                .HasMaxLength(100); // Keep or adjust based on Business ID format

             modelBuilder.Entity<Chat>()
                .Property(c => c.ListingId)
                .HasMaxLength(100) // Keep MaxLength for ListingId string
                .IsRequired();

            // --- Table Names (Optional: Define explicitly) ---
            modelBuilder.Entity<Chat>().ToTable("Chats");
            modelBuilder.Entity<ChatMessage>().ToTable("Messages");
            modelBuilder.Entity<ChatParticipant>().ToTable("ChatParticipants");

            base.OnModelCreating(modelBuilder);
        }
    }

    // --- Models ---

    public enum DbMessageType
    {
        TEXT,
        MEDIA,
        SYSTEM,
        CALL_LOG
    }

    [Table("Chats")]
    public class Chat
    {
        [Key]
        public Guid ChatId { get; set; } = Guid.NewGuid();

        [Required]
        [MaxLength(100)] // A chat must be about a listing
        public string ListingId { get; set; } = null!;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation Properties
        public virtual ICollection<ChatParticipant> Participants { get; set; } = new List<ChatParticipant>();
        public virtual ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
    }

    [Table("Messages")]
    public class ChatMessage
    {
        [Key]
        public Guid MessageId { get; set; } = Guid.NewGuid();

        [Required]
        public Guid ChatId { get; set; }

        // Sender Info
        [Required]
        public int SenderUserId { get; set; } // <<< CHANGED TO INT (Internal Auth User ID or 0 for System)

        [MaxLength(100)] // Keep or adjust based on Business ID format from Auth Service
        public string? SenderActingAsBusinessId { get; set; } // Business ID if sent on behalf of business

        // Content
        public string? Content { get; set; }

        [Required]
        public DbMessageType MessageType { get; set; }

        [Required]
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        // Media Info (if MessageType == MEDIA)
        [MaxLength(1024)]
        public string? MediaUrl { get; set; }
        [MaxLength(255)]
        public string? MediaFileName { get; set; }
        [MaxLength(100)]
        public string? MediaMimeType { get; set; }
        public long? MediaSizeBytes { get; set; }

        // Navigation Property
        [ForeignKey("ChatId")]
        public virtual Chat Chat { get; set; } = null!;
    }

    [Table("ChatParticipants")]
    public class ChatParticipant
    {
        // Composite Key uses INT UserId now (defined in OnModelCreating)
        [Required]
        public Guid ChatId { get; set; }

        [Required]
        public int UserId { get; set; } // <<< CHANGED TO INT (Internal Auth User ID)

        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

        // Navigation Properties
        [ForeignKey("ChatId")]
        public virtual Chat Chat { get; set; } = null!;

        // public DateTime? LastReadTimestamp { get; set; } // Optional for read receipts
    }
}