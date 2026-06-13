using Ebook.Domain.Products;

namespace Ebook.Domain.Tests.Products;

public class ArtifactTests
{
    private static readonly DateTime Now = new(2026, 6, 12, 9, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Create_preserva_metadados()
    {
        var productId = Guid.NewGuid();

        var artifact = Artifact.Create(
            productId, ArtifactType.Manuscript, "products/x/manuscript/manuscript.v1.md", "deadbeef", 1, "{}", Now);

        Assert.Equal(productId, artifact.ProductId);
        Assert.Equal(ArtifactType.Manuscript, artifact.Type);
        Assert.Equal(1, artifact.Version);
        Assert.Equal("deadbeef", artifact.Hash);
        Assert.NotEqual(Guid.Empty, artifact.Id);
    }
}
