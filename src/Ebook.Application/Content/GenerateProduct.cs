using System.Text.Json;
using Ebook.Application.Common.Jobs;
using Ebook.Application.Common.Messaging;
using Ebook.Application.Common.Text;
using Ebook.Domain.Abstractions;
using Ebook.Domain.Common;
using Ebook.Domain.Niches;
using Ebook.Domain.Products;

namespace Ebook.Application.Content;

/// <summary>
/// Inicia o pipeline de conteúdo de um nicho: cria o Product (estágio Outline)
/// e enfileira o primeiro job (ebook.outline). As etapas seguintes encadeiam-se
/// por jobs idempotentes.
/// </summary>
public sealed record GenerateProductCommand(Guid NicheId, string? Title = null, QualityTier Tier = QualityTier.Commercial)
    : ICommand<GenerateProductResult>;

public sealed record GenerateProductResult(Guid ProductId, string Slug);

public sealed class GenerateProductCommandHandler(
    INicheRepository niches,
    IProductRepository products,
    IJobQueue jobQueue,
    IUnitOfWork unitOfWork,
    IClock clock) : ICommandHandler<GenerateProductCommand, GenerateProductResult>
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<Result<GenerateProductResult>> HandleAsync(GenerateProductCommand command, CancellationToken ct)
    {
        var niche = await niches.GetByIdAsync(command.NicheId, ct);
        if (niche is null)
        {
            return Result.Failure<GenerateProductResult>(ContentErrors.NicheNotFound(command.NicheId));
        }

        var title = string.IsNullOrWhiteSpace(command.Title) ? niche.Name : command.Title;
        var slug = await UniqueSlugAsync(title, niche.Slug, ct);

        var product = Product.Create(niche.Id, slug, title, command.Tier, clock.UtcNow);
        products.Add(product);
        await unitOfWork.SaveChangesAsync(ct);

        await jobQueue.EnqueueAsync(new JobRequest(
            ContentJobs.Outline,
            JsonSerializer.Serialize(new OutlineJobPayload(product.Id), JsonOptions),
            ContentJobs.OutlineKey(product.Id),
            ProductId: product.Id), ct);

        return Result.Success(new GenerateProductResult(product.Id, slug));
    }

    private async Task<string> UniqueSlugAsync(string title, string fallback, CancellationToken ct)
    {
        var baseSlug = Slug.From(title);
        if (string.IsNullOrEmpty(baseSlug))
        {
            baseSlug = string.IsNullOrEmpty(fallback) ? "ebook" : fallback;
        }

        var slug = baseSlug;
        while (await products.SlugExistsAsync(slug, ct))
        {
            slug = $"{baseSlug}-{Guid.NewGuid().ToString("N")[..6]}";
        }

        return slug;
    }
}
