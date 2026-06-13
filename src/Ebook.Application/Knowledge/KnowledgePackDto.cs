namespace Ebook.Application.Knowledge;

/// <summary>Recorte do KnowledgePack usado apenas para indexar (tópico + vocabulário do público).</summary>
public sealed record KnowledgePackDto(
    string? Topic,
    KnowledgePackAudienceDto? Audience);

public sealed record KnowledgePackAudienceDto(IReadOnlyList<string>? Vocabulary);
