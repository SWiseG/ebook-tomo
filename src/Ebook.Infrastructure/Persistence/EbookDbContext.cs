using Ebook.Domain.Analytics;
using Ebook.Domain.Knowledge;
using Ebook.Domain.Niches;
using Ebook.Domain.Optimization;
using Ebook.Domain.Products;
using Ebook.Domain.Sales;
using Ebook.Domain.Social;
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
    public DbSet<SaleEvent> SaleEvents => Set<SaleEvent>();
    public DbSet<SocialPost> SocialPosts => Set<SocialPost>();
    public DbSet<Channel> Channels => Set<Channel>();
    public DbSet<AnalyticsEvent> AnalyticsEvents => Set<AnalyticsEvent>();
    public DbSet<MetricDaily> MetricDailies => Set<MetricDaily>();
    public DbSet<OptimizationRun> OptimizationRuns => Set<OptimizationRun>();
    public DbSet<OptimizationDecision> OptimizationDecisions => Set<OptimizationDecision>();
    public DbSet<OutboxEventRecord> OutboxEvents => Set<OutboxEventRecord>();
    public DbSet<ProcessedEventRecord> ProcessedEvents => Set<ProcessedEventRecord>();
    public DbSet<JobRecord> Jobs => Set<JobRecord>();
    public DbSet<AiUsageRecord> AiUsages => Set<AiUsageRecord>();
    public DbSet<AiCacheRecord> AiCache => Set<AiCacheRecord>();
    public DbSet<MediaUsageRecord> MediaUsages => Set<MediaUsageRecord>();
    public DbSet<MediaCacheRecord> MediaCache => Set<MediaCacheRecord>();
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
            e.Property(x => x.KiwifyProductId).HasMaxLength(120);
            e.Property(x => x.CheckoutUrl).HasMaxLength(500);
            e.Property(x => x.LpUrl).HasMaxLength(500);
            e.Property(x => x.Description).HasMaxLength(4000);
            e.Property(x => x.EmailLanguage).HasMaxLength(20);
            e.Property(x => x.Category).HasMaxLength(120);
            e.Property(x => x.PublicationPlatform).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(x => x.Status);
            e.HasIndex(x => x.KiwifyProductId);
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

        modelBuilder.Entity<SaleEvent>(e =>
        {
            e.ToTable("SaleEvent");
            e.HasKey(x => x.Id);
            e.Property(x => x.KiwifyOrderId).HasMaxLength(120);
            // chave natural composta: a Kiwify reusa o order_id na venda e no estorno/chargeback
            e.HasIndex(x => new { x.KiwifyOrderId, x.Type }).IsUnique();
            e.Property(x => x.Type).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Currency).HasMaxLength(3);
            e.Property(x => x.GrossAmount).HasConversion<double>();
            e.Property(x => x.NetAmount).HasConversion<double>();
            e.Property(x => x.UtmSource).HasMaxLength(120);
            e.Property(x => x.UtmCampaign).HasMaxLength(120);
            e.Property(x => x.RawPayloadPath).HasMaxLength(400);
            e.HasIndex(x => new { x.ProductId, x.OccurredAtUtc });
        });

        modelBuilder.Entity<SocialPost>(e =>
        {
            e.ToTable("SocialPost");
            e.HasKey(x => x.Id);
            e.Property(x => x.Network).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.PostType).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Caption).HasMaxLength(3000);
            e.Property(x => x.Hashtags).HasMaxLength(600);
            e.Property(x => x.ContentPath).HasMaxLength(400);
            e.Property(x => x.MediaPath).HasMaxLength(400);
            e.Property(x => x.CarouselPaths).HasMaxLength(2000);
            e.Property(x => x.Utm).HasMaxLength(300);
            e.Property(x => x.ExternalId).HasMaxLength(120);
            e.HasIndex(x => x.ProductId);
            e.HasIndex(x => new { x.Status, x.ScheduledAtUtc });
        });

        modelBuilder.Entity<Channel>(e =>
        {
            e.ToTable("Channel");
            e.HasKey(x => x.Id);
            e.Property(x => x.Name).HasMaxLength(120);
            e.Property(x => x.Platform).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.PageId).HasMaxLength(120);
            e.Property(x => x.IgUserId).HasMaxLength(120);
            e.Property(x => x.AccessToken).HasMaxLength(800);
            e.Property(x => x.PublicMediaBaseUrl).HasMaxLength(300);
            e.HasIndex(x => x.NicheId).IsUnique();
            e.Ignore(x => x.DomainEvents);
        });

        modelBuilder.Entity<AnalyticsEvent>(e =>
        {
            e.ToTable("AnalyticsEvent");
            e.HasKey(x => x.Id);
            e.Property(x => x.Channel).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Type).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.UtmSource).HasMaxLength(120);
            e.Property(x => x.UtmCampaign).HasMaxLength(120);
            e.Property(x => x.UtmContent).HasMaxLength(120);
            e.HasIndex(x => x.OccurredAtUtc);
            e.HasIndex(x => new { x.ProductId, x.OccurredAtUtc });
        });

        modelBuilder.Entity<MetricDaily>(e =>
        {
            e.ToTable("MetricDaily");
            e.HasKey(x => x.Id);
            e.Property(x => x.Channel).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Revenue).HasConversion<double>();
            e.HasIndex(x => new { x.ProductId, x.DateUtc, x.Channel }).IsUnique();
            e.HasIndex(x => x.DateUtc);
        });

        modelBuilder.Entity<OptimizationRun>(e =>
        {
            e.ToTable("OptimizationRun");
            e.HasKey(x => x.Id);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.ReportPath).HasMaxLength(400);
            e.HasIndex(x => x.CycleNumber).IsUnique();
        });

        modelBuilder.Entity<OptimizationDecision>(e =>
        {
            e.ToTable("OptimizationDecision");
            e.HasKey(x => x.Id);
            e.Property(x => x.Decision).HasConversion<string>().HasMaxLength(20);
            e.Property(x => x.Status).HasConversion<string>().HasMaxLength(20);
            e.HasIndex(x => x.RunId);
            e.HasIndex(x => x.ProductId);
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

        modelBuilder.Entity<MediaUsageRecord>(e =>
        {
            e.ToTable("MediaUsage");
            e.HasKey(x => x.Id);
            e.Property(x => x.Purpose).HasMaxLength(100);
            e.Property(x => x.Provider).HasMaxLength(20);
            e.HasIndex(x => new { x.Provider, x.CreatedAtUtc }); // cota diária por provedor
            e.HasIndex(x => x.ProductId);                        // proveniência por produto (Fase 3B)
        });

        modelBuilder.Entity<MediaCacheRecord>(e =>
        {
            e.ToTable("MediaCache");
            e.HasKey(x => x.Hash);
            e.Property(x => x.Hash).HasMaxLength(64);
            e.Property(x => x.Purpose).HasMaxLength(100);
            e.Property(x => x.Provider).HasMaxLength(20);
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
