namespace Ebook.Application.Knowledge;

/// <summary>Tipos/chaves dos jobs de aprendizado de conhecimento (E15 — loop de estilo).</summary>
public static class KnowledgeJobs
{
    public const string StyleLearn = "style.learn";

    /// <summary>Chave idempotente por nicho+semana: um aprendizado de estilo por nicho por semana.</summary>
    public static string StyleLearnKey(Guid nicheId, int week) => $"style:{nicheId}:{week}";
}

/// <summary>Payload do job de aprendizado de estilo: o nicho e o produto cuja capa será analisada.</summary>
public sealed record StyleLearnJobPayload(Guid NicheId, Guid ProductId);
