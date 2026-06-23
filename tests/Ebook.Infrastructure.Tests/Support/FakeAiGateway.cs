using System.Collections.Concurrent;
using Ebook.Application.Ai;
using Ebook.Domain.Common;

namespace Ebook.Infrastructure.Tests.Support;

/// <summary>
/// Gateway de IA determinístico para testes: respostas canned por purpose e
/// contagem de invocações (para validar reuso e economia de tokens).
/// Jamais chama Claude CLI ou rede.
/// </summary>
public sealed class FakeAiGateway : IAiGateway
{
    private readonly ConcurrentDictionary<string, int> _calls = new(StringComparer.Ordinal);

    public int CallsFor(string purpose) => _calls.GetValueOrDefault(purpose);

    public Task<Result<AiResponse>> CompleteAsync(AiRequest request, CancellationToken ct = default)
    {
        _calls.AddOrUpdate(request.Purpose, 1, (_, n) => n + 1);
        var content = Canned(request.Purpose);
        return Task.FromResult(Result.Success(new AiResponse(content, AiProviderKind.ClaudeCli, CacheHit: false, 1)));
    }

    private static string Canned(string purpose) => purpose switch
    {
        "knowledge.pack" => """
            {
              "niche": "Finanças para Autônomos",
              "topic": "Finanças para Autônomos",
              "language": "pt-BR",
              "audience": {
                "who": "Autônomos brasileiros",
                "pains": ["renda instável"],
                "desires": ["previsibilidade"],
                "objections": ["falta de tempo"],
                "vocabulary": ["fluxo de caixa", "reserva de emergência"]
              },
              "facts": [{ "claim": "60% dos autônomos não separam contas", "source": "exemplo" }],
              "competitors": [],
              "angles": ["organização em 30 dias"],
              "sources": ["https://exemplo"]
            }
            """,
        "ebook.outline" => """
            {
              "title": "Dinheiro Sob Controle",
              "subtitle": "O guia financeiro do autônomo",
              "promise": "Organize suas finanças em 30 dias",
              "tone": "prático e direto",
              "chapters": [
                { "n": 1, "title": "Mapeie seu dinheiro", "goal": "Diagnóstico", "keyPoints": ["receitas", "despesas"], "targetWords": 1000 },
                { "n": 2, "title": "Crie sua reserva", "goal": "Segurança", "keyPoints": ["meta", "automação"], "targetWords": 1000 }
              ]
            }
            """,
        "ebook.chapter" => "Conteúdo do capítulo gerado pela IA fake.\n\nSegundo parágrafo com exemplo prático.",
        "ebook.palette" => """
            { "background": "#0E2A47", "accent": "#E0B978", "onDark": "#F5F8FC",
              "headingFont": "Manrope", "bodyFont": "Merriweather", "displayFont": "Archivo Black" }
            """,
        "cover.plan" => """
            {
              "eyebrow": "Guia Completo",
              "subtitle": "Do caos ao controle em 30 dias",
              "features": [
                { "text": "Elimine dívidas e organize seu orçamento", "icon": "money" },
                { "text": "Crie sua reserva de emergência", "icon": "shield" },
                { "text": "Invista com confiança", "icon": "chart" }
              ],
              "seal": "MÉTODO VALIDADO",
              "scene": "a person reviewing finances at a tidy desk, warm light, editorial photo, no text",
              "layout": "classic"
            }
            """,
        "ebook.visual-plan" => """
            {
              "chapters": [
                { "title": "Mapeie seu dinheiro", "mode": "photo", "query": "person budgeting notebook", "prompt": "person reviewing finances at a desk, editorial photo, no text" },
                { "title": "Crie sua reserva", "mode": "illustration", "query": "financial safety net", "prompt": "conceptual safety net catching coins, editorial illustration, no text" }
              ]
            }
            """,
        "ebook.review" => """
            { "introduction": "Introdução envolvente sobre dinheiro.", "conclusion": "Conclusão com CTA: comece hoje." }
            """,
        "ebook.audit" => """
            {
              "verdict": "warn",
              "score": 72,
              "summary": "Boa estrutura; falta prova social em mais capítulos.",
              "items": [
                { "item": "Promessa clara (4 U's)", "pass": true, "note": "Headline específica e urgente." },
                { "item": "Hook por capítulo", "pass": true, "note": "Aberturas nomeiam a dor." },
                { "item": "Prova social/autoridade", "pass": false, "note": "Só 1 capítulo traz caso real." },
                { "item": "CTA final forte", "pass": true, "note": "CTA único e específico no fim." }
              ]
            }
            """,
        "ebook.sales-copy" => """
            {
              "headline": "Assuma o controle do seu dinheiro",
              "subheadline": "Mesmo com renda variável",
              "bullets": ["Método de 30 dias", "Planilhas prontas"],
              "painSection": "Você nunca sabe quanto sobra.",
              "solutionSection": "Um sistema simples resolve.",
              "faq": [{ "q": "Funciona para MEI?", "a": "Sim." }],
              "price": { "anchor": 47, "current": 27 },
              "bonuses": ["Checklist mensal"],
              "variants": [{ "id": "A", "headline": "Assuma o controle", "active": true }]
            }
            """,
        "social.calendar" => """
            {
              "posts": [
                { "day": 1, "network": "Instagram", "postType": "Launch",
                  "headline": "Saiu o guia!", "copy": "Lançamento do nosso e-book.",
                  "hashtags": ["financas", "autonomos"], "timeSlot": "19:00" },
                { "day": 3, "network": "Facebook", "postType": "Value",
                  "headline": "Dica rápida", "copy": "Separe contas PF e PJ hoje.",
                  "hashtags": ["dinheiro"], "timeSlot": "12:00" }
              ]
            }
            """,
        "video.script" => """
            {
              "hook": "Você ainda não controla seu dinheiro?",
              "scenes": [
                { "narration": "Toda semana o dinheiro some.", "onScreen": "Para de sumir", "seconds": 7 },
                { "narration": "Existe um método simples.", "onScreen": "Método de 30 dias", "seconds": 8 },
                { "narration": "Garanta o seu guia no link da bio.", "onScreen": "Link na bio", "seconds": 6 }
              ],
              "caption": "Assuma o controle do seu dinheiro. Link na bio.",
              "hashtags": ["financas", "autonomos"]
            }
            """,
        _ => "{}"
    };
}
