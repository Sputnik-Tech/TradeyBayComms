using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MessagingService.Data;

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
        modelBuilder.Entity<ChatParticipant>().HasKey(cp => new { cp.ChatId, cp.UserId }); // Composite Key

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
        modelBuilder.Entity<ChatParticipant>()
             .HasIndex(cp => cp.UserId); // Index for finding chats for a user

        // --- Property Mappings ---
        modelBuilder
            .Entity<ChatMessage>()
            .Property(e => e.MessageType)
            .HasConversion<string>() // Store enum as string (VARCHAR)
            .HasMaxLength(50); // Set appropriate length

         modelBuilder.Entity<Chat>() // Removed ChatType mapping
            .Property(c => c.ListingId)
            .IsRequired(); // Assuming a chat is always linked to a listing


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
    CALL_LOG // Keep if calls might inject log markers
}

// Represents a 1:1 conversation context, usually linked to an Ad
[Table("Chats")]
public class Chat
{
    [Key]
    public Guid ChatId { get; set; } = Guid.NewGuid();

    // ChatType is implicitly "OneToOne" - field removed
    // GroupName removed

    // Context (Avito specific)
    [Required]
    [MaxLength(100)]
    public string ListingId { get; set; } = null!; // A chat must be about a listing

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // Navigation Properties
    public virtual ICollection<ChatParticipant> Participants { get; set; } = new List<ChatParticipant>();
    public virtual ICollection<ChatMessage> Messages { get; set; } = new List<ChatMessage>();
}

// Represents a single message within a chat
[Table("Messages")]
public class ChatMessage
{
    [Key]
    public Guid MessageId { get; set; } = Guid.NewGuid();

    [Required]
    public Guid ChatId { get; set; }

    // Sender Info
    [Required]
    [MaxLength(100)]
    public string SenderUserId { get; set; } = null!; // B2C User Object ID

    [MaxLength(100)]
    public string? SenderActingAsBusinessId { get; set; } // Business ID if sent on behalf of business

    // Content
    public string? Content { get; set; } // Nullable for Media/System messages

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

// Links a user to a specific chat
[Table("ChatParticipants")]
public class ChatParticipant
{
    // Composite Key defined in OnModelCreating
    [Required]
    public Guid ChatId { get; set; }

    [Required]
    [MaxLength(100)]
    public string UserId { get; set; } = null!; // B2C User Object ID

    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    // Navigation Properties
    [ForeignKey("ChatId")]
    public virtual Chat Chat { get; set; } = null!;

    // Add LastReadTimestamp etc. if needed for read receipts
    // public DateTime? LastReadTimestamp { get; set; }
}