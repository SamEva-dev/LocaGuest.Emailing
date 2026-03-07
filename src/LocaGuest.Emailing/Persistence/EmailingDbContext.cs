using LocaGuest.Emailing.Persistence.Entities;
using Microsoft.EntityFrameworkCore;

namespace LocaGuest.Emailing.Persistence;

public sealed class EmailingDbContext : DbContext
{
    public EmailingDbContext(DbContextOptions<EmailingDbContext> options) : base(options) { }

    public DbSet<EmailMessageEntity> EmailMessages => Set<EmailMessageEntity>();
    public DbSet<EmailEventEntity> EmailEvents => Set<EmailEventEntity>();
    public DbSet<EmailAttachmentEntity> EmailAttachments => Set<EmailAttachmentEntity>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema("emailing");

        modelBuilder.Entity<EmailMessageEntity>()
            .HasIndex(x => x.ProviderMessageId);

        modelBuilder.Entity<EmailMessageEntity>()
            .HasIndex(x => new { x.Status, x.CreatedAtUtc });

        modelBuilder.Entity<EmailEventEntity>()
            .HasIndex(x => new { x.ProviderMessageId, x.Event, x.TsEvent })
            .IsUnique();

        modelBuilder.Entity<EmailEventEntity>()
            .HasOne(x => x.EmailMessage)
            .WithMany()
            .HasForeignKey(x => x.EmailMessageId)
            .OnDelete(DeleteBehavior.SetNull);

        modelBuilder.Entity<EmailAttachmentEntity>()
            .HasOne(x => x.EmailMessage)
            .WithMany(x => x.Attachments)
            .HasForeignKey(x => x.EmailMessageId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
