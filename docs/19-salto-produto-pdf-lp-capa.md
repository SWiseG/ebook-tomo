# 19 — O Salto: PDF + LP + Capa de "competente" para "irresistível"

> **Documento estratégico + plano de execução.** Nasce de um diagnóstico honesto (o pipeline NÃO está estagnado — está maduro, mas chegou ao limite do polimento incremental) cruzado com pesquisa de mercado 2026. Define o salto qualitativo que dá **visibilidade** ao projeto. Liga-se a [11-padrao-editorial.md](11-padrao-editorial.md), [12-pdf-recursos-visuais.md](12-pdf-recursos-visuais.md), [14-sistema-capas-ia.md](14-sistema-capas-ia.md), [16-guia-lp-design-system.md](16-guia-lp-design-system.md) e [10-geracao-ia-midia.md](10-geracao-ia-midia.md).

---

## 0. Diagnóstico sem ilusão

A sensação de estagnação **não** vem de o produto estar ruim. Vem de termos resolvido a "primeira camada" de qualidade e o ganho marginal de cada novo ajuste ter ficado invisível. Onde estamos hoje (auditado no código):

- **PDF** (`QuestPdfRenderer`): drop cap, aberturas de capítulo decorativas, sumário visual com ícones, pull quotes, caixas Insight/Caso com SVG, timelines, tabelas antes→depois, blocos de estatística, divisores SVG, imagem no corpo, modo seguro à prova de falha. **É bom.**
- **Capa** (`CoverJobHandler` + `CoverDirector` + `ClaudeVisionCoverQa`): diretor de capa por IA (eyebrow, benefícios, selo, cena), fundo via Media Gateway free-first, caminho full-AI com QA de visão, composição Skia rica, mockup 3D, banner de marketplace. **É bom.**
- **LP** (`LandingPageBuilder`): 3 templates por categoria de nicho, JSON-LD (Product/Offer/FAQ/Rating), OG/Twitter, hero v2 com fundo borrado, prova social, depoimentos, passos, bônus empilhados, garantia, contador, sticky CTA. **É bom.**

**O problema:** "bom" é o teto da camada atual. Os concorrentes 2026 não competem mais em "o PDF é bonito" — competem em **formato, loop de conversão e profundidade editorial**. É aí que está o salto.

### O que o mercado faz que nós não fazemos (pesquisa 2026)

| Vetor competitivo | Mercado | Nós | Fonte |
|---|---|---|---|
| **Multi-formato** (EPUB/DOCX/áudio) | Padrão de tabela; "publicação-ready" é o diferencial nº 1 | Só PDF | Inkfluence, FlipHTML5 |
| **Continuidade entre capítulos** | Maior falha apontada nos concorrentes (livros desconexos) | Geramos capítulo a capítulo, sem passe de coerência | Inkfluence |
| **Loop de conversão fechado** (Smart Traffic / A/B automático) | Unbounce +20% conversão; variantes geradas por IA +15–30% | Geramos 1 LP fixa; ROI Optimizer não fecha o loop visual | Unbounce, Landingi |
| **Leitura interativa** (flipbook HTML5) | +tempo na página, +open rate mobile 25%, captura de lead | Entregamos arquivo estático | ZenFlip, Publitas |
| **Capa: torneio + sinalização de gênero** | Cor quente +34% CTR, contraste em thumbnail decide a venda | Geramos 1 capa, QA passa/reprova | AIBookArt, WriteStats |
| **Imagens no corpo** | "1 imagem a cada 2–3 páginas" eleva conclusão ~30% | ✅ JÁ FEITO (`PdfJobHandler` + visual-plan) | docs/11 |

> **Correção factual (auditoria de código 2026-06-28):** este doc foi escrito a partir do docs/11, que está
> desatualizado. O código foi além: **imagens no corpo (Frente D), infográficos (`ComposeInfographics`) e a
> auditoria de conversão (`ConversionAudit` + endpoint `/products/{id}/audit` + UI) JÁ EXISTEM.** A auditoria existe
> como **query manual**, não como **gate automático** antes de publicar — esse é o gap real. O estado verdadeiro,
> item a item, está nos planos de sprint em [`docs/sprints/`](sprints/README.md).

---

## 1. A tese do salto

Em vez de continuar polindo três artefatos, transformamos cada produto de **3 arquivos** em um **ecossistema de ativos a partir de uma fonte única**, e fechamos o **loop autônomo de conversão**. Três pilares:

```
PILAR 1 — MULTI-SUPERFÍCIE        PILAR 2 — MOTOR DE CONVERSÃO       PILAR 3 — PROFUNDIDADE
(1 conteúdo → N formatos)         (gera → mede → aprende → escolhe)   EDITORIAL
                                                                       
EPUB · DOCX · Audiobook ·         Torneio de capas (vision score) ·   Imagens no corpo (Frente D) ·
Web Reader/Flipbook               Variantes de LP + Smart Traffic ·   Passe de continuidade ·
                                  Auditoria de conversão (gate)       Infográficos de dados
```

- **Pilar 1** dá **visibilidade** imediata: o mesmo trabalho vira 4 entregáveis. É o que o cliente *vê* mudar.
- **Pilar 2** é o **diferencial defensável**: somos autônomos; o concorrente humano não roda um loop 24/7. Encaixa no `OptimizationService` e na arquitetura event-driven que já temos.
- **Pilar 3** elimina os gaps editoriais conhecidos (Frentes D e E nunca concluídas).

---

## 2. Iniciativas priorizadas (impacto × esforço)

Esforço: **low** = 1 arquivo/prompt, sem migration; **medium** = novo renderer/job, talvez migration; **high** = novo módulo/loop, migrations, frontend.

### 🥇 Onda 1 — Quick wins de visibilidade (low/medium)

| # | Iniciativa | Esforço | Impacto | Encaixe no framework |
|---|---|---|---|---|
| 1.1 | **Export EPUB** | medium | alto | Novo `IEbookRenderer`/`EpubRenderer` consumindo o `PdfBook` já modelado; EPUB = XHTML+CSS+zip (sem dep nova pesada). Novo `ArtifactType.Epub`. |
| 1.2 | **Export DOCX** | medium | médio | `DocxRenderer` (OpenXML) do mesmo `PdfBook`. Permite ao cliente editar — remove objeção. |
| 1.3 | **Passe de continuidade** | low | alto | Novo prompt `prompts/ebook/continuity.md` + etapa no `ChapterJobHandler`/novo handler que recebe resumo dos capítulos anteriores. Resolve a maior falha de mercado. |
| 1.4 | **Torneio de capas** | medium | alto | `CoverJobHandler` gera N candidatas (já temos cadeia free-first), `ICoverQa` vira **score** comparativo (já é visão), escolhe a melhor por thumbnail/contraste/gênero. |
| 1.5 | **Frente D — imagens no corpo** | medium | alto | Media Gateway Incremento 3 (já planejado em docs/10); `PdfJobHandler` injeta 1 img/2–3 págs a partir das `query` do `visual-plan`. |

### 🥈 Onda 2 — O motor de conversão (high)

| # | Iniciativa | Esforço | Impacto | Encaixe |
|---|---|---|---|---|
| 2.1 | **Variantes de LP A/B** | high | muito alto | `LpLab` já existe; gerar 2–3 variantes (headline/hero/oferta) por produto, persistir, servir por rota. |
| 2.2 | **Smart Traffic / pick winner** | high | muito alto | Roteamento ponderado por conversão medida (já temos pixel + `MetricDaily` + `AnalyticsEvent`); evento fecha no `OptimizationService` (matar/iterar/escalar já existe → estender p/ "promover variante"). |
| 2.3 | **Auditoria de conversão (Frente E)** | medium | alto | Gate de IA com o checklist da Fase 4 (docs/11) antes de publicar — "linter de persuasão". Liga ao E16. |

### 🥉 Onda 3 — Superfícies premium (medium/high)

| # | Iniciativa | Esforço | Impacto | Encaixe |
|---|---|---|---|---|
| 3.1 | **Web Reader / Flipbook HTML5** | high | alto | Reusa o HTML do EPUB (1.1) num leitor web servido em `/read/{slug}`; page-turn, analytics, captura de lead. Maior "wow" percebido. |
| 3.2 | **Audiobook** | medium | médio | Piper TTS + FFmpeg já existem (vídeo); gerar MP3 por capítulo a partir do texto. Novo `ArtifactType.Audiobook`. |
| 3.3 | **Infográficos de dados** | medium | médio | Bloco `Infographic` já existe no parser; pré-compor no Skia (gráfico real a partir de `Stat`/`Comparison`). |
| 3.4 | **Personalização por origem** | medium | médio | Headline dinâmica por `utm_source` (dynamic text replacement) — alimenta o Pilar 2. |

---

## 3. Recomendação de sequência

```
SPRINT A (salto visível, ~1–2 semanas):  1.3 continuidade → 1.1 EPUB → 1.4 torneio de capas → 1.5 imagens no corpo
SPRINT B (profundidade + gate):          1.2 DOCX → 2.3 auditoria de conversão → 3.3 infográficos
SPRINT C (o diferencial autônomo):       2.1 variantes LP → 2.2 smart traffic/winner
SPRINT D (superfícies premium):          3.1 web reader/flipbook → 3.2 audiobook → 3.4 personalização
```

**Por que essa ordem:** o Sprint A entrega o que se *vê* (novos formatos + capa melhor + páginas ilustradas) com esforço baixo/médio, restaurando a sensação de avanço imediato. O Sprint C é o trabalho mais pesado, mas é o que ninguém copia — fica para quando o salto visível já estiver no ar e medindo.

### Princípios inegociáveis ao executar (do CLAUDE.md)
- Nenhum módulo chama IA direto → tudo por `IAiGateway`/`IMediaGateway`. Prompts em `/prompts`, nunca hardcoded.
- Trabalho longo (EPUB/DOCX/áudio/torneio) vira **job** com `IdempotencyKey` natural (ex.: `epub:{productId}`).
- Conteúdo nos artefatos via `IFileStore`/`IArtifactStore`; índice+hash no SQLite. Novos `ArtifactType` por migration.
- Transições de `Product` só pelos métodos do agregado; loop de conversão emite eventos → Outbox → handlers idempotentes.
- Todo entregável segue o padrão editorial (docs/11) e a identidade visual unificada por nicho (docs/15).

---

## 4. Métrica do salto (como saber que saímos da estagnação)

| Antes | Depois (alvo) |
|---|---|
| 1 formato (PDF) por produto | 4 superfícies (PDF, EPUB, web reader, áudio) |
| 1 capa, passa/reprova | melhor de N por score de visão |
| 0 imagens no corpo | ≥1 a cada 2–3 páginas |
| 1 LP estática | variantes medidas, vencedora promovida automaticamente |
| Sem gate de qualidade | auditoria de conversão antes de publicar |

O sucesso não é "ficou mais bonito" — é **produto vira ecossistema** e **o sistema melhora sozinho a conversão**.

---

## Fontes (pesquisa de mercado, 2026)
- [Best AI Ebook Generators 2026 — Inkfluence AI](https://www.inkfluenceai.com/blog/best-ai-ebook-generators-2026) (multi-formato + continuidade de capítulo como diferenciais)
- [10 Best AI Ebook Generators — FlipHTML5](https://fliphtml5.com/guide/tools/best-ai-ebook-generators-2026/)
- [Best AI Ebook Cover Generators 2026 — Inkfluence AI](https://www.inkfluenceai.com/blog/best-ai-ebook-cover-generators-2026)
- [Book Cover Design Trends 2026 — AIBookArt](https://www.aibookart.com/blog/book-cover-trends-2026) (cor quente +34% CTR; ilustrado > fotográfico)
- [Book Cover Design Psychology — WriteStats](https://writestats.com/book-cover-design-psychology-what-makes-readers-click-buy/) (decisão em <2s, thumbnail decide)
- [The 7 Best Landing Page Builders 2026 — Search Engine Journal](https://www.searchenginejournal.com/the-7-best-landing-page-builders-for-2026/561935/)
- [Conversion Rate Optimization with AI 2026 — Landingi](https://landingi.com/blog/conversion-rate-optimization-with-ai/) (variantes IA +15–30%)
- [Unbounce — Smart Traffic / CRO](https://unbounce.com/) (+20% conversão por roteamento)
- [How to Build a Self-Improving A/B Testing Agent — MindStudio](https://www.mindstudio.ai/blog/self-improving-ab-testing-agent-landing-pages-ad-copy)
- [Interactive Flipbooks vs Static PDFs — ZenFlip](https://zenflip.io/en/blog/interactive-flipbooks-vs-static-pdfs)
- [How Flipbooks Drive Sales & Engagement 2026 — Publitas](https://www.publitas.com/blog/how-flipbooks-can-drive-sales-engagement/) (+25% open rate mobile)
- [Great Ebook Layout Design Tips — Kulokale](https://kulokale.com/great-ebook-layout-design-tips/) (whitespace/grid/hierarquia = premium)
