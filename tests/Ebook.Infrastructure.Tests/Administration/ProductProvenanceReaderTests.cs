using Ebook.Infrastructure.Administration;
using Ebook.Infrastructure.Persistence;
using Ebook.Infrastructure.Tests.Support;
using Microsoft.Extensions.DependencyInjection;

namespace Ebook.Infrastructure.Tests.Administration;

public class ProductProvenanceReaderTests
{
    [Fact]
    public async Task Lista_texto_e_imagem_apenas_do_produto_pedido()
    {
        await using var provider = TestHost.Build();
        var db = provider.GetRequiredService<EbookDbContext>();
        var productA = Guid.NewGuid();
        var productB = Guid.NewGuid();
        var t0 = new DateTime(2026, 6, 10, 8, 0, 0, DateTimeKind.Utc);

        db.AiUsages.AddRange(
            new AiUsageRecord { Id = Guid.NewGuid(), Purpose = "ebook.outline", ProductId = productA, Provider = "ClaudeCli", InputTokensEst = 120, OutputTokensEst = 80, DurationMs = 1500, CreatedAtUtc = t0 },
            new AiUsageRecord { Id = Guid.NewGuid(), Purpose = "ebook.chapter", ProductId = productA, Provider = "ClaudeCli", InputTokensEst = 200, OutputTokensEst = 100, DurationMs = 1800, CreatedAtUtc = t0.AddMinutes(5) },
            new AiUsageRecord { Id = Guid.NewGuid(), Purpose = "ebook.chapter", ProductId = productB, Provider = "ClaudeCli", InputTokensEst = 50, OutputTokensEst = 50, DurationMs = 1000, CreatedAtUtc = t0 });

        db.MediaUsages.AddRange(
            new MediaUsageRecord { Id = Guid.NewGuid(), Purpose = "chapter-illustration", ProductId = productA, Provider = "Pollinations", Bytes = 8000, DurationMs = 3000, CreatedAtUtc = t0.AddMinutes(10) },
            new MediaUsageRecord { Id = Guid.NewGuid(), Purpose = "background", ProductId = null, Provider = "LocalSkia", Bytes = 1000, DurationMs = 50, CreatedAtUtc = t0 },
            new MediaUsageRecord { Id = Guid.NewGuid(), Purpose = "chapter-illustration", ProductId = productB, Provider = "Pollinations", Bytes = 4000, DurationMs = 2500, CreatedAtUtc = t0 });

        await db.SaveChangesAsync();

        var dto = await new ProductProvenanceReader(db).GetAsync(productA, default);

        Assert.Equal(2, dto.TextCount);
        Assert.Equal(1, dto.ImageCount);
        Assert.Equal(500, dto.TotalTokens);   // (120+80) + (200+100)
        Assert.Equal(8000, dto.TotalBytes);
        Assert.All(dto.Text, e => Assert.Equal("ClaudeCli", e.Provider));
        Assert.Equal("Pollinations", Assert.Single(dto.Images).Provider);
        // ordem cronológica do texto: outline antes do chapter
        Assert.Equal("ebook.outline", dto.Text[0].Purpose);
    }
}
