# Sprints do Salto de Produto — índice e como usar

> Planos de execução detalhados das ondas definidas em [../19-salto-produto-pdf-lp-capa.md](../19-salto-produto-pdf-lp-capa.md).
> Escritos no formato **"ensinar o Claude do futuro a fazer"**: cada iniciativa traz objetivo, base de
> pesquisa, biblioteca/abordagem concreta, encaixe arquitetural (arquivos, interfaces, jobs, `ArtifactType`,
> migration), passos, testes e **Definition of Done (o que teremos ao final)**. Nenhum código aqui — só plano.

## Como retomar o trabalho (de qualquer máquina)

1. Leia o `CLAUDE.md` da raiz (regras inegociáveis) e o [docs/19](../19-salto-produto-pdf-lp-capa.md) (tese).
2. Abra a sprint que vai executar. Cada item tem um checklist; siga na ordem.
3. **Antes de implementar qualquer item, confirme o estado no código** — estes docs têm um carimbo de data;
   o código mistura coisas "feitas" e "a fazer" (ver a auditoria abaixo). Confie no código, não na memória.
4. Build/test sempre: `dotnet build Ebook.slnx -warnaserror` e `dotnet test Ebook.slnx`. Nenhum teste chama IA/rede.

## Estado real do pipeline (auditado no código em 2026-06-28)

Isto recalibra o docs/19, que herdou imprecisões do docs/11. **Já implementado e funcionando:**

- PDF rico (`QuestPdfRenderer`): drop cap, abertura de capítulo, sumário visual, callouts, pull quotes, timeline, comparação, stat, divisores SVG, modo seguro.
- **Imagens no corpo / Frente D** (`PdfJobHandler.InjectIllustrationsAsync` + `prompts/ebook/visual-plan.md` + Media Gateway).
- **Infográficos** (`PdfJobHandler.ComposeInfographics` → `IImageComposer.RenderInfographic`).
- Capa: diretor IA (`CoverDirector`), fundo via Media Gateway, full-AI gated (`cover.aiFullCover`), **QA de visão** (`ClaudeVisionCoverQa`), mockup 3D, banner de marketplace.
- LP: 3 templates (`LandingPageBuilder`), JSON-LD, OG/Twitter, hero v2, prova social, depoimentos, passos, bônus, garantia, contador, sticky CTA.
- **Auditoria de conversão** (`ConversionAudit`, endpoint `/products/{id}/audit`, UI Angular) — **como query manual**.
- `LpLab` (gera variante de LP sob demanda, async) — **ferramenta de teste interna, não A/B em produção**.
- Ciclo de otimização (`OptimizationService`: Iterate/Kill/Scale) — "Iterate" sugere preço/refresh, **sem promoção de variante vencedora**.
- Vídeo/Reels (`GenerateVideoJobHandler`: Piper TTS + FFmpeg).

**Gaps reais (o que estas sprints atacam):**

| Gap | Sprint |
|---|---|
| Sem EPUB / DOCX (só PDF) | A, B |
| Sem audiobook (apesar de TTS pronto) | D |
| Sem leitor web / flipbook | D |
| Capítulos gerados isoladamente, sem passe de continuidade | A |
| Capa única (não "melhor de N" com score comparativo) | A |
| Auditoria não é gate automático antes de publicar | B |
| LP A/B só em laboratório; produção serve 1 LP; sem Smart Traffic; sem promoção automática | C |
| Sem personalização por origem de tráfego | C |
| Doc drift (docs/06 e docs/11 desatualizados) + dívidas a limpar | E |

## Arquivos

| Sprint | Tema | Esforço dominante | Arquivo |
|---|---|---|---|
| A | Multi-superfície: quick wins de visibilidade | low/medium | [sprint-a.md](sprint-a.md) |
| B | Profundidade + gate de qualidade | medium | [sprint-b.md](sprint-b.md) |
| C | Motor de conversão autônomo | high | [sprint-c.md](sprint-c.md) |
| D | Superfícies premium (web reader, audiobook) | medium/high | [sprint-d.md](sprint-d.md) |
| E | Limpeza & otimização (deletar/consertar) | low/medium | [sprint-e.md](sprint-e.md) |

## Convenções de implementação (resumo do CLAUDE.md — valem para todas as sprints)

- IA só via `IAiGateway`; mídia só via `IMediaGateway`. Prompts em `/prompts/{area}/{nome}.md`, nunca hardcoded.
- Trabalho longo = job: `IJobQueue.EnqueueAsync` com `IdempotencyKey` natural; `IJobHandler` (registro por scan no `AddApplication`).
- Caso de uso = `ICommand<T>`/`IQuery<T>` + handler num arquivo, via `IDispatcher`; retornar `Result<T>`.
- Conteúdo no `IFileStore`/`IArtifactStore`; índice+hash no SQLite. Novo `ArtifactType` → **migration** (`dotnet dotnet-ef migrations add ...`) e **rebuild antes de rodar**.
- Eventos de domínio → Outbox (mesma transação) → handlers idempotentes. Datas UTC via `IClock`. Enums como string.
- Testes sem mock framework (fakes manuais); infra com `TestHost.Build()`. Jamais chamar Claude CLI/rede em teste.
- Todo entregável segue o padrão editorial (docs/11) e a identidade visual por nicho (docs/15).
