using Ebook.Domain.Knowledge;

namespace Ebook.Domain.Tests.Knowledge;

public class KnowledgeAssetTests
{
    private static readonly DateTime Now = new(2026, 6, 12, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_inicia_sem_reusos()
    {
        var asset = KnowledgeAsset.Create(
            Guid.NewGuid(), KnowledgeAssetType.KnowledgePack, "Finanças", "juros,renda", "p/x.json", "abc", Now);

        Assert.Equal(KnowledgeAssetType.KnowledgePack, asset.Type);
        Assert.Equal(0, asset.ReuseCount);
        Assert.Equal("Finanças", asset.Topic);
    }

    [Fact]
    public void MarkReused_incrementa_contador()
    {
        var asset = KnowledgeAsset.Create(
            Guid.NewGuid(), KnowledgeAssetType.KnowledgePack, "t", "k", "p", "h", Now);

        asset.MarkReused();
        asset.MarkReused();

        Assert.Equal(2, asset.ReuseCount);
    }
}
