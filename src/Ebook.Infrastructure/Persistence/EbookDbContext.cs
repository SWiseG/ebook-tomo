using Ebook.Domain.Knowledge;
using Ebook.Domain.Niches;
using Ebook.Domain.Products;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Ebook.Infrastructure.Persistence;

public sealed class EbookDbContext(DbContextOptions<EbookDbContext> options) : DbContext(options)
{
    public DbSet<Niche> Niches => Set<Niche>();
    public DbSet<TrendSnapshot> TrendSnapshots => Set<TrendSnapshot>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<KnowledgeAsset> KnowledgeAssets => Set<KnowledgeAsset>();
    public DbSet<Artifact> Artifacts => Set<Artifact>();
    public DbSet<OutboxEventRecord> OutboxEvents => Set<OutboxEventRecord>();
    public DbSet<ProcessedEventRecord> ProcessedEvents => Set<ProcessedEventRecord>();
    public DbSet<JobRecord> Jobs => Set<JobRecord>();
    public DbSet<AiUsageRecord> AiUsages => Set<AiUsageRecord>();
    public DbSet<AiCacheRecord> AiCache => Set<AiCacheRecord>();
    public DbSet<SettingRecord> Settings => Set<SettingRecord>();
    public DbSet<JobRunLogRecord> JobRunLogs => Set<JobRunLogRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Niche>(e =>
        {
            e.ToTable("Niche");
            e.HasKey(x => x.Id);
            e.Property(x => x.Slug).HasMaxLength(120);
            e.HasIndex(x => x.Slug).IsUnique();
            e.Property(x => x.Name).HasMaxLength(200);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(x => x.Status);
            e.Ignore(x => x.DomainEvents);
        });

        modelBuilder.Entity<Product>(e =>
        {
            e.ToTable("Product");
            e.HasKey(x => x.Id);
            e.Property(x => x.Slug).HasMaxLength(120);
            e.HasIndex(x => x.Slug).IsUnique();
            e.Property(x => x.Title).HasMaxLength(300);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Stage).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.QualityTier).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Currency).HasMaxLength(3);
            e.Property(x => x.Price).HasConversion<double>();
            e.HasIndex(x => x.Status);
            e.Ignore(x => x.DomainEvents);
        });

        modelBuilder.Entity<TrendSnapshot>(e =>
        {
            e.ToTable("TrendSnapshot");
            e.HasKey(x => x.Id);
            e.Property(x => x.Source).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.PayloadPath).HasMaxLength(400);
            e.HasIndex(x => new { x.NicheId, x.CollectedAtUtc });
        });

        modelBuilder.Entity<KnowledgeAsset>(e =>
        {
            e.ToTable("KnowledgeAsset");
            e.HasKey(x => x.Id);
            e.Property(x => x.Type).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Topic).HasMaxLength(200);
            e.Property(x => x.KeywordsCsv).HasMaxLength(1000);
            e.Property(x => x.Path).HasMaxLength(400);
            e.Property(x => x.Hash).HasMaxLength(64);
            e.HasIndex(x => new { x.NicheId, x.Type });
        });

        modelBuilder.Entity<Artifact>(e =>
        {
            e.ToTable("Artifact");
            e.HasKey(x => x.Id);
            e.Property(x => x.Type).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Path).HasMaxLength(400);
            e.Property(x => x.Hash).HasMaxLength(64);
            e.HasIndex(x => new { x.ProductId, x.Type, x.Version });
        });

        modelBuilder.Entity<OutboxEventRecord>(e =>
        {
            e.ToTable("OutboxEvent");
            e.HasKey(x => x.Id);
            e.Property(x => x.Type).HasMaxLength(200);
            e.HasIndex(x => x.ProcessedAtUtc);
        });

        modelBuilder.Entity<ProcessedEventRecord>(e =>
        {
            e.ToTable("ProcessedEvent");
            e.HasKey(x => new { x.EventId, x.HandlerName });
            e.Property(x => x.HandlerName).HasMaxLength(300);
        });

        modelBuilder.Entity<JobRecord>(e =>
        {
            e.ToTable("Job");
            e.HasKey(x => x.Id);
            e.Property(x => x.Type).HasMaxLength(100);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.IdempotencyKey).HasMaxLength(300);
            e.HasIndex(x => x.IdempotencyKey).IsUnique();
            e.HasIndex(x => new { x.Status, x.ScheduledAtUtc });
        });

        modelBuilder.Entity<AiUsageRecord>(e =>
        {
            e.ToTable("AiUsage");
            e.HasKey(x => x.Id);
            e.Property(x => x.Purpose).HasMaxLength(100);
            e.Property(x => x.Provider).HasMaxLength(20);
            e.HasIndex(x => x.CreatedAtUtc);
        });

        modelBuilder.Entity<AiCacheRecord>(e =>
        {
            e.ToTable("AiCache");
            e.HasKey(x => x.Hash);
            e.Property(x => x.Hash).HasMaxLength(64);
            e.Property(x => x.Purpose).HasMaxLength(100);
        });

        modelBuilder.Entity<SettingRecord>(e =>
        {
            e.ToTable("Setting");
            e.HasKey(x => x.Key);
            e.Property(x => x.Key).HasMaxLength(100);
        });

        modelBuilder.Entity<JobRunLogRecord>(e =>
        {
            e.ToTable("JobRunLog");
            e.HasKey(x => x.JobName);
            e.Property(x => x.JobName).HasMaxLength(100);
        });
    }
}

/// <summary>Usado apenas pelo dotnet-ef para gerar migrations (não sobe o host).</summary>
public sealed class EbookDbContextFactory : IDesignTimeDbContextFactory<EbookDbContext>
{
    public EbookDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<EbookDbContext>()
            .UseSqlite("Data Source=design-time.db")
            .Options;
        return new EbookDbContext(options);
    }
}
