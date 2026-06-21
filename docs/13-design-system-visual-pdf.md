# 13 — Design System Visual do PDF (geração, captação e montagem de imagens/elementos)

> **Documento de planejamento + execução.** Define como elevar o PDF de "texto-first" para "design-first", com geração e captação de imagens de fontes gratuitas, uma biblioteca de elementos visuais (iconografia, caixas de diálogo, timelines, stat cards, infográficos) e **toda a curadoria visual passando pela IA**. Complementa [11-padrao-editorial.md](11-padrao-editorial.md) (o padrão) e [12-pdf-recursos-visuais.md](12-pdf-recursos-visuais.md) (recursos QuestPDF + repositórios). Liga-se à Onda 2 (E14 Media Gateway) e Onda 3 (E15 aprendizado de estilo) do [roadmap](03-roadmap-mvp.md).

## 0. Diagnóstico (auditado 2026-06-21)

Extração de texto dos 4 PDFs (nosso + 3 exemplos de referência em `docs/ebooks-examples/`):

| PDF | Páginas | Palavras | Tamanho | Natureza |
|---|---|---|---|---|
| **Nosso** (`tomo-marketing-digital-curso`) | 59 | 9.112 | 1,1 MB | **texto-first** (~155 palavras/página) |
| Finanças — Do Caos ao Controle | ~11 | 635 | 7,8 MB | **design-first** |
| Marketing — Do Zero ao Resultado | ~11 | 585 | 6,2 MB | **design-first** |
| O Caminho para sua Melhor Versão | ~11 | 531 | 14,7 MB | **design-first** |

A diferença não é "mais imagens" — é filosofia oposta. Os exemplos são *lookbooks*: ~55 palavras/página, cada página é uma peça gráfica (foto full-bleed, infográfico, card de número, timeline, citação desenhada). O nosso é manuscrito tipograficamente correto, mas visualmente plano.

### Causa-raiz de "imagens ruins" (no código)

O `PdfJobHandler` **já injeta** ilustrações (1/capítulo, máx. 6, via Media Gateway). O problema é a qualidade, por 3 fatores somados:

1. **Prompt pede arte vazia** — `prompts/media/illustration.md` pede `abstract, minimalist, no text, soft colors`. Gera borrões decorativos que não provam nada (viola "imagens trabalham" do doc 11).
2. **Cai no pior elo** — sem chaves de API, a cadeia (Gemini→Cloudflare→HuggingFace→**Pollinations**→Pexels→Skia) usa Pollinations/Flux grátis (blobs aleatórios para "abstract") ou o piso Skia (só gradiente).
3. **Nunca usa foto real no corpo** — Pexels existe na cadeia, mas o brief é sempre "illustration"; fotografia editorial (o que torna os exemplos cativantes) nunca aparece.

### O que já é bom (preservar)

- Texto forte (copy PAS/AIDA, hooks, micro-CTA — Frentes A/B/C).
- Renderer com vocabulário de blocos: capa full-bleed, sumário, H2/H3, bullets, checklist desenhado, pull-quote, callout com ícone SVG, imagem, CTA.
- 6 fontes embarcadas + paleta emocional por nicho; `IconRegistry` (Lucide recolorível); Media Gateway com cache + telemetria.

**Conclusão:** não reconstruir — adicionar (a) um **diretor de arte por IA** que decide *o que* mostrar e *de onde* tirar, e (b) uma **biblioteca de elementos visuais desenhados** (o "design system específico").

## 1. Inventário — o que temos hoje

| Recurso | Estado | Onde |
|---|---|---|
| Geração IA de imagem free-first | ✅ Gemini, Cloudflare, HuggingFace, Pollinations | `Infrastructure/Media/*Resolver.cs` |
| Banco de fotos | ⚠️ só Pexels (subutilizado) | `PexelsMediaResolver.cs` |
| Piso garantido | ✅ gradiente Skia | `LocalSkiaImageResolver.cs` |
| Cache de mídia | ✅ content-addressable | `MediaGateway.cs` |
| Ícones vetoriais | ✅ Lucide SVG recolorível | `IconRegistry.cs` |
| Composição Skia | ✅ `IImageComposer` | Infra/Content |
| Fontes/cores por nicho | ✅ `NicheStyleCatalog` | Infra |
| Telemetria texto (IA) | ✅ `AiUsageRecord` **com `ProductId`** | `Records.cs` |
| Telemetria mídia | ⚠️ `MediaUsageRecord` **sem ProductId** | `Records.cs` |
| Tela telemetria de mídia | ✅ existe | `features/media-telemetry/` |

**Lacunas de fonte:** sem Unsplash/Pixabay; sem bundle de ilustrações CC0 (unDraw/Humaaans); sem `NicheVisualProfile` (perfil visual completo por nicho — doc 12, Parte 4).

## 2. Workstreams

O eixo central — *"toda revisão passa pela IA"* — é o **WS-B**. Os demais são os braços que ele aciona.

### WS-A — `NicheVisualProfile` (design system por nicho, fundação)
Estender `NicheStyleCatalog` para um perfil visual completo: **IconSet** temático (Lucide), **PhotoSeeds** (queries Pexels/Unsplash), **IllustrationStyle** (flat corporate / humano / geométrico), **motivos decorativos** (cor de abertura, ornamento, estilo de card). Alimenta capa, abertura de capítulo, caixas, corpo, LP e social → identidade coerente ponta a ponta.

### WS-B — Diretor de Arte por IA (núcleo)
Novo purpose `ebook.visual-plan`, prompt em `/prompts/media/art-direction.md`. Recebe **manuscrito + NicheVisualProfile** e devolve **plano visual por seção** (JSON):
- tipo de peça por seção: **foto** (concreto), **ilustração generativa** (abstrato), **infográfico/timeline/stat-card/quote-card/dialogue-box** (dado/processo), **ícone**;
- a **query/prompt exata** (concreta, não "abstract finance");
- **posicionamento** (após qual parágrafo) e **cor/estilo** do nicho.
Vira input do `PdfJobHandler` (hoje injeta cegamente 1 imagem/H2). É a passada de IA que falta. Reaproveita o style playbook do **E15** (fecha E15-04).

### WS-C — Captação de imagem melhor (fontes)
- **Reescrever** `illustration.md` para briefs concretos/específicos.
- **Roteamento por tipo**: concreto → foto (Pexels/Unsplash/Pixabay); abstrato → generativa; processo/dado → composição Skia (WS-E).
- **Novos resolvers**: `UnsplashMediaResolver` + `PixabayMediaResolver`.
- **Bundle CC0 curado** (unDraw/Humaaans/Open Peeps) em `assets/illustrations/` — determinístico, offline, por nicho.
- **Tratamento editorial**: duotone/overlay na cor do nicho (coerência de estilo).

### WS-D — Biblioteca de elementos visuais no renderer (o "design system específico")
Estender parser + `QuestPdfRenderer`:
- **Abertura de capítulo decorativa** (página dedicada: número gigante translúcido via `Layers` + ícone do nicho + gradiente + título display — padrão Z).
- **Drop cap** (capitular no 1º parágrafo).
- **Timeline** (passos numerados com conector).
- **Stat cards / números de impacto**.
- **Caixa de diálogo / quote card** (citação desenhada, estilo social).
- **Tabela comparativa** "antes → depois" (MultiColumn).
- **Sumário visual** (ícone por capítulo).
- **Divisores de seção SVG** + **corrigir cabeçalho** (hoje repete o título inteiro = ruído; trocar por título-corrente curto + nº de capítulo).
- A **IA emite os marcadores** (estender `chapter.md`) → decide onde entra timeline/stat/diálogo.

### WS-E — Montagem via Skia ("infographic composer")
Peças com layout rico (stat card sobre foto, quote card, infográfico de passos) compostas com `IImageComposer`/Skia numa imagem e inseridas no PDF. É onde acontece a "montagem" texto + imagem + ícone + cor numa peça única.

### WS-F — Gate de auditoria de conversão (Frente E / E16)
Passada final de IA validando o checklist da Fase 4 do doc 11 (imagem a cada 2–3 págs? cores 60-30-10? hook por capítulo?) **antes de publicar** — o "linter de persuasão".

## 3. Tela de proveniência do PDF (action no produto)

Ver de onde o PDF veio: quem fez texto, quem fez cada imagem, com que prompt.

**Lacuna:** `AiUsageRecord` tem `ProductId` (texto atribuível ✅); `MediaUsageRecord` não tem (imagem não atribuível ❌).

**Plano:**
1. Adicionar `ProductId` (+ opcional `ArtifactType`) a `MediaUsageRecord` (migration); propagar via `MediaBrief` → `RecordUsageAsync`.
2. **Manifesto de build** (`provenance.json` no FileStore por artefato): cada seção de texto (purpose, provider, tokens, ms) e cada imagem (provider, prompt, cache hit, fonte) + fontes/paleta.
3. **UI**: action "Ver proveniência" em `product-detail` → drawer com timeline do build (Texto: Claude/cache + tokens; Imagens: miniatura + provedor + prompt; Design: fontes/paleta/nicho). Endpoint `GET /products/{id}/provenance`.

## 4. Tela de uso por fonte externa (telemetria unificada)

Hoje `features/media-telemetry/` + `MediaTelemetryReader` cobrem **só mídia**. Uso de Claude/texto vive em `AiUsage` e não aparece.

**Plano:**
1. `AiTelemetryReader` (espelho do de mídia) agregando `AiUsageRecord` por provedor: gerações hoje/mês, tokens in/out, latência, cache-hit.
2. **DTO unificado "Fontes"** (texto + mídia) com colunas comuns: status (ligado/desligado), uso hoje/mês, unidade (tokens/imagens/bytes), latência média, cache hit %, cota restante, custo ($0 vs pago).
3. **UI**: evoluir a tela para **"Fontes Externas"** (grade unificada + cards de resumo: cache economizado, % grátis vs local). Reusa o padrão AG Grid de `media-telemetry`.

## 5. Sequenciamento (impacto × esforço)

| Fase | Entrega | Por quê | Liga a |
|---|---|---|---|
| **1** | WS-D parcial: abertura de capítulo decorativa + drop cap + cabeçalho corrigido + sumário com ícones | Só QuestPDF, sem IA/migration — maior salto/menor esforço | doc 12 |
| **2** | WS-C: prompt concreto + roteamento foto/ilustração + Unsplash/Pixabay | Resolve "imagens ruins" | Frente D, E14-07 |
| **3** | Tela de Fontes unificada (§4) + `ProductId` em `MediaUsage` | Visibilidade + base da proveniência | E14-08 |
| **4** | WS-B: Diretor de Arte por IA (visual-plan) | Núcleo — "toda revisão passa pela IA" | E15-04 |
| **5** | WS-D completo + WS-E: timelines, stat cards, diálogos, infográficos Skia | Design system específico | Frente C/D |
| **6** | Proveniência (§3) + `NicheVisualProfile` consolidado | Profundidade | — |
| **7** | WS-F: gate de auditoria de conversão | Qualidade garantida | E16, Frente E |

## 5.1 Status de execução (2026-06-21)

- ✅ **Fase 1** — `QuestPdfRenderer`: cabeçalho corrente curto, sumário visual (ícone + título por capítulo), abertura de capítulo decorativa (número grande + ícone + rótulo + título + régua), drop cap. Verificado com PDF real; 16 testes de Content OK.
- ✅ **Fase 5 (parte de elementos visuais, sem chave)** — novos blocos `Timeline` (lista numerada → passos com chip + conector), `Stat` (`> [!STAT] 97% | desc`) e `QuoteCard` (`> [!FRASE] frase — autor`, com ícone de aspas). Parser (`Markdown.cs`) + renderer + prompt `chapter.md` atualizados para a IA emitir os marcadores. Verificado parser→render com PDF real; 166 testes OK.
- ⏭️ **Pendente Fase 5**: tabela comparativa (antes→depois), infográficos compostos via Skia (WS-E), divisores de seção SVG.
- ⛔ **Fase 2 (imagens)** aguarda chaves de API na Railway (ver §6).

## 6. Notas de produção (Railway)

Deploy em produção é na **Railway**. Itens que exigem ação fora do código (avisar o usuário quando chegar a hora):
- **Chaves de API** dos provedores generativos/foto (Gemini, Cloudflare, HuggingFace, Unsplash, Pixabay) → variáveis de ambiente na Railway (`Media__Gemini__ApiKey`, etc.). Sem elas, a cadeia cai em Pollinations/Skia (causa das imagens ruins).
- Migrations novas (ex.: `ProductId` em `MediaUsage`) rodam no startup (`Migrate()`); só exigem deploy.
- Bundles de assets (ilustrações CC0) versionados no repo → entram no build da imagem, sem ação manual.
