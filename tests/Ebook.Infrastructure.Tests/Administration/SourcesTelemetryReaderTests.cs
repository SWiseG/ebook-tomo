using Ebook.Domain.Abstractions;
using Ebook.Infrastructure.Administration;
using Ebook.Infrastructure.Persistence;
using Ebook.Infrastructure.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace Ebook.Infrastructure.Tests.Administration;

public class SourcesTelemetryReaderTests
{
    private sealed class FixedClock(DateTime now) : IClock
    {
        public DateTime UtcNow => now;
    }

    [Fact]
    public async Task Agrega_texto_e_imagem_separando_hoje_do_mes_e_contando_cache()
    {
        await using var provider = TestHost.Build();
        var db = provider.GetRequiredService<EbookDbContext>();
        var now = new DateTime(2026, 6, 15, 12, 0, 0, DateTimeKind.Utc);
        var today = now.Date;
        var earlierThisMonth = new DateTime(2026, 6, 3, 9, 0, 0, DateTimeKind.Utc);

        // Texto (Claude): 2 gerações hoje + 1 cache hit hoje + 1 geração no início do mês
        db.AiUsages.AddRange(
            new AiUsageRecord { Id = Guid.NewGuid(), Purpose = "ebook.chapter", Provider = "ClaudeCli", CacheHit = false, InputTokensEst = 100, OutputTokensEst = 50, DurationMs = 1000, CreatedAtUtc = today.AddHours(8) },
            new AiUsageRecord { Id = Guid.NewGuid(), Purpose = "ebook.outline", Provider = "ClaudeCli", CacheHit = false, InputTokensEst = 200, OutputTokensEst = 80, DurationMs = 2000, CreatedAtUtc = today.AddHours(9) },
            new AiUsageRecord { Id = Guid.NewGuid(), Purpose = "ebook.chapter", Provider = "ClaudeCli", CacheHit = true, InputTokensEst = 0, OutputTokensEst = 0, DurationMs = 5, CreatedAtUtc = today.AddHours(10) },
            new AiUsageRecord { Id = Guid.NewGuid(), Purpose = "ebook.chapter", Provider = "ClaudeCli", CacheHit = false, InputTokensEst = 10, OutputTokensEst = 10, DurationMs = 900, CreatedAtUtc = earlierThisMonth });

        // Imagem: 1 geração Pollinations hoje + 1 cache hit hoje
        db.MediaUsages.AddRange(
            new MediaUsageRecord { Id = Guid.NewGuid(), Purpose = "chapter-illustration", Provider = "Pollinations", CacheHit = false, Bytes = 5000, DurationMs = 3000, CreatedAtUtc = today.AddHours(8) },
            new MediaUsageRecord { Id = Guid.NewGuid(), Purpose = "chapter-illustration", Provider = "Cache", CacheHit = true, Bytes = 0, DurationMs = 0, CreatedAtUtc = today.AddHours(9) });

        await db.SaveChangesAsync();

        var reader = new SourcesTelemetryReader(db, new FixedClock(now));
        var dto = await reader.GetTelemetryAsync(default);

        var text = Assert.Single(dto.Sources, s => s.Kind == "Texto" && s.Provider == "ClaudeCli");
        Assert.Equal(2, text.GeneratedToday);          // cache hit não conta
        Assert.Equal(3, text.GeneratedThisMonth);      // 2 hoje + 1 no início do mês
        Assert.Equal(430, text.TokensToday);           // 150 + 280
        Assert.Equal(0, text.BytesToday);

        var image = Assert.Single(dto.Sources, s => s.Kind == "Imagem" && s.Provider == "Pollinations");
        Assert.Equal(1, image.GeneratedToday);
        Assert.Equal(5000, image.BytesToday);
        Assert.Equal(0, image.TokensToday);

        Assert.Equal(2, dto.CacheHitsToday);           // 1 texto + 1 imagem
    }
}
