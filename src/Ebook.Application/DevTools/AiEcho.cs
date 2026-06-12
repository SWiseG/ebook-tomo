using Ebook.Application.Ai;
using Ebook.Application.Common.Messaging;
using Ebook.Domain.Common;
using Ebook.Domain.Products;

namespace Ebook.Application.DevTools;

/// <summary>
/// Critério de saída da Fase 0: valida a cadeia completa do AI Gateway
/// (cache → Claude CLI via assinatura Pro) de ponta a ponta.
/// </summary>
public sealed record AiEchoCommand(string Text) : ICommand<AiResponse>;

public sealed class AiEchoCommandHandler(IAiGateway aiGateway) : ICommandHandler<AiEchoCommand, AiResponse>
{
    public Task<Result<AiResponse>> HandleAsync(AiEchoCommand command, CancellationToken ct) =>
        aiGateway.CompleteAsync(new AiRequest(
            Purpose: "dev.echo",
            PromptTemplate: "dev/echo",
            Variables: new Dictionary<string, string> { ["text"] = command.Text },
            Tier: QualityTier.Draft,
            MaxOutputTokensEst: 200), ct);
}
