using System.Security.Cryptography;
using System.Text;
using Ebook.Application.Common.Messaging;
using Ebook.Domain.Abstractions;
using Ebook.Domain.Common;
using Ebook.Domain.Products;
using Ebook.Domain.Sales;

namespace Ebook.Application.Publishing;

/// <summary>
/// Registra uma venda/estorno vinda do webhook da Kiwify (E07-02). Idempotente por
/// <c>KiwifyOrderId</c>; resolve o produto pelo id externo; guarda o payload bruto no FileStore.
/// </summary>
public sealed record RecordSaleCommand(
    string KiwifyOrderId,
    SaleType Type,
    decimal GrossAmount,
    decimal NetAmount,
    string Currency,
    string? KiwifyProductRef,
    string? UtmSource,
    string? UtmCampaign,
    DateTime OccurredAtUtc,
    string RawPayloadJson) : ICommand<bool>;

public sealed class RecordSaleCommandHandler(
    ISaleRepository sales,
    IProductRepository products,
    IFileStore fileStore,
    IUnitOfWork unitOfWork) : ICommandHandler<RecordSaleCommand, bool>
{
    public async Task<Result<bool>> HandleAsync(RecordSaleCommand command, CancellationToken ct)
    {
        if (await sales.ExistsAsync(command.KiwifyOrderId, command.Type, ct))
        {
            return Result.Success(true); // reentrega do webhook: idempotente por (order_id, tipo)
        }

        Guid? productId = null;
        if (!string.IsNullOrWhiteSpace(command.KiwifyProductRef))
        {
            var product = await products.GetByKiwifyProductIdAsync(command.KiwifyProductRef, ct);
            productId = product?.Id;
        }

        var stored = await fileStore.WriteTextAsync(
            SalePaths.Raw(command.KiwifyOrderId, command.Type), command.RawPayloadJson, ct);

        sales.Add(SaleEvent.Create(
            productId,
            command.KiwifyOrderId,
            command.Type,
            command.GrossAmount,
            command.NetAmount,
            command.Currency,
            command.UtmSource,
            command.UtmCampaign,
            command.OccurredAtUtc,
            stored.RelativePath));

        await unitOfWork.SaveChangesAsync(ct);
        return Result.Success(true);
    }
}

public static class SalePaths
{
    // tipo no caminho: venda e estorno do mesmo pedido não sobrescrevem o payload um do outro
    public static string Raw(string orderId, SaleType type) =>
        $"sales/{Sanitize(orderId)}-{type.ToString().ToLowerInvariant()}.json";

    private static string Sanitize(string value)
    {
        var chars = value.Select(c => char.IsLetterOrDigit(c) || c is '-' or '_' ? c : '-');
        return new string(chars.ToArray());
    }
}

/// <summary>Validação do token do webhook (comparação em tempo fixo).</summary>
public static class KiwifyWebhook
{
    public static bool IsValidToken(string? provided, string? configured)
    {
        if (string.IsNullOrEmpty(configured) || string.IsNullOrEmpty(provided))
        {
            return false;
        }

        var a = Encoding.UTF8.GetBytes(provided);
        var b = Encoding.UTF8.GetBytes(configured);
        return CryptographicOperations.FixedTimeEquals(a, b);
    }
}
