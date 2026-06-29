# Sprint A — Multi-superfície: quick wins de visibilidade

> **Meta da sprint:** restaurar a sensação de avanço com 3 entregas de alto impacto e esforço baixo/médio que o
> cliente *vê*: o e-book ganha **continuidade real**, vira também **EPUB**, e a capa passa a ser a **melhor de N**.
> Nenhuma depende de IA de imagem nova (já temos Media Gateway) nem de frontend novo.
>
> Itens: **A1** passe de continuidade · **A2** export EPUB · **A3** torneio de capas.
> Pré-leitura obrigatória: `CLAUDE.md`, [docs/11](../11-padrao-editorial.md), [README desta pasta](README.md).

---

## A1 — Passe de continuidade entre capítulos  ·  esforço: **low**  ·  impacto: **alto**

### Objetivo
Hoje cada capítulo é gerado isolado (`ChapterJobHandler`), o que produz repetição de ideias, falta de ganchos
entre capítulos e ausência de "fio condutor". A pesquisa de mercado aponta **continuidade de capítulo como a
maior falha dos concorrentes** (Inkfluence). Queremos um passe que dê coesão sem reescrever tudo.

### Base de pesquisa
- Inkfluence AI 2026: "most tools write chapters in isolation, producing disjointed ebooks" — é o diferencial nº 1.
- Padrão editorial (docs/11 §6): cada capítulo deve abrir com hook e fechar com micro-CTA/ponte para o próximo.

### Abordagem (sem biblioteca nova)
Dois caminhos possíveis — **escolher o B** (mais barato em tokens e idempotente):

- **Opção A (cara):** reescrever cada capítulo com contexto dos anteriores. Dobra o custo de IA. ❌
- **Opção B (recomendada):** um **passe único de coesão** após o `ReviewJobHandler`, que recebe o manuscrito
  inteiro + outline e devolve: (1) frase-ponte ao fim de cada capítulo, (2) lista de repetições a remover,
  (3) ajuste do hook de abertura quando fraco. Aplicado como *patch* sobre o manuscrito, não regeração.

### Encaixe arquitetural
- **Já existe** `ReviewJobHandler` (`ContentJobs.Review = "ebook.review"`) rodando após os capítulos
  (`ChapterJobHandler` enfileira review) e antes do Cover. **Estender esse fluxo**, não criar etapa nova.
- Novo prompt: `prompts/ebook/continuity.md` (placeholders `{{outline}}`, `{{manuscript}}`), carregado por `IPromptLibrary`.
- Novo purpose no `IAiGateway`: `"ebook.continuity"` (cacheado por hash, como todo purpose).
- O handler aplica o resultado e regrava o manuscrito no `IFileStore` (`ContentPaths.Manuscript`). Sem migration.
- Idempotência: marcar no MetaJson/arquivo que a continuidade já passou (ou comparar hash) para não repassar em reentrega.

### Passos
1. Escrever `prompts/ebook/continuity.md` — saída **JSON estrito** (`bridges[]`, `removals[]`, `hookFixes[]`), seguindo o estilo do `audit.md`.
2. Estender `ReviewJobHandler` (ou criar `ContinuityJobHandler` se ficar grande) para chamar o purpose e aplicar o patch.
3. Aplicar patches de forma conservadora (só inserir pontes/ajustar hooks; remoções por correspondência exata de trecho).
4. Persistir manuscrito revisado; logar nº de pontes inseridas/repetições removidas.

### Testes
- Fake `IAiGateway` determinístico devolvendo um JSON de continuidade conhecido → asserta que as pontes entraram e as repetições saíram.
- Idempotência: rodar 2× não duplica pontes.

### Definition of Done / o que teremos
- Manuscritos com fio condutor: cada capítulo fecha com ponte para o próximo e abre com hook reforçado.
- Custo de IA ~1 chamada extra por e-book (não por capítulo). Sem mudança de schema.

---

## A2 — Export EPUB  ·  esforço: **medium**  ·  impacto: **alto**

### Objetivo
Entregar o e-book também em **EPUB 3** (formato nativo de e-readers/Kindle via conversão, Apple Books, Google
Play Livros). Hoje só há PDF. Multi-formato é o **diferencial nº 1** dos concorrentes 2026.

### Base de pesquisa
- Inkfluence/FlipHTML5 2026: "whether a tool outputs publication-ready files" é o divisor de águas; EPUB+PDF+DOCX é tabela.
- EPUB 3 = um `.zip` com `mimetype`, `META-INF/container.xml`, um pacote OPF (manifesto+spine), navegação XHTML e os conteúdos XHTML+CSS+imagens.

### Biblioteca / abordagem
Avaliadas (busca 2026):
- **QuickEPUB** (MIT, NuGet) — simples, HTML→EPUB. Bom ponto de partida; limitado em fontes/CSS avançado.
- **EpubSharp** — leitura + escrita EPUB 3.x; mais completo, menos mantido.
- **Hand-built (recomendado a médio prazo):** gerar o zip nós mesmos com `System.IO.Compression`. Já produzimos
  HTML (LP) e temos o modelo `PdfBook` estruturado — converter `MarkdownBlock[]` → XHTML é trivial e nos dá
  **controle total** de CSS/fontes embarcadas (as 6 fontes do `FontRegistry`) e imagens (as mesmas da Frente D).

**Decisão:** começar com **QuickEPUB** para validar o fluxo rápido; se o CSS/fontes ficarem limitados, migrar para
hand-built (o esforço é baixo porque o mapeamento de blocos já existe conceitualmente no `QuestPdfRenderer`).

### Encaixe arquitetural
- Nova interface em Application: `IEbookExporter` (ou `IEpubRenderer`) — declarada em `Application/Content`,
  implementada em `Infrastructure/Content` (regra de dependência). Consome o **mesmo `PdfBook`** já montado pelo `PdfBookComposer`.
- Novo `ArtifactType.Epub` → **migration** (enum persistido como string; só adicionar o valor + migration de índice se houver constraint).
- Novo job `ContentJobs.Epub = "ebook.epub"` + `EpubJobHandler`, enfileirado **em paralelo ao Pdf** (ou logo após),
  com `IdempotencyKey` `epub:{productId}`. Reusa manuscrito + capa + paleta + imagens já no FileStore.
- Indexar via `IArtifactStore.WriteBytesAsync(ContentPaths.Epub(slug), ...)` + `Artifact.Create(..., ArtifactType.Epub, ...)`.
- Expor no painel (lista de artefatos do produto) e no Kiwify (se o checkout aceitar múltiplos arquivos — verificar).

### Passos
1. Adicionar `ArtifactType.Epub`; criar migration; **rebuild**.
2. Definir `ContentPaths.Epub(slug)`; `ContentJobs.Epub` + payload.
3. Implementar `EpubRenderer` mapeando `MarkdownBlock` → XHTML (heading, parágrafo, bullets, pull quote, callout, image, etc.) + CSS por paleta do nicho + capa como primeira página.
4. `EpubJobHandler`: lê os artefatos, renderiza, grava, indexa. Re-entrante (pula se existe).
5. Disparar o job no pipeline (ex.: junto do `PdfJobHandler` ou após o PDF pronto).

### Testes
- Renderizar um `PdfBook` de fixture → abrir o zip em memória e validar estrutura (`mimetype` primeiro e *stored*, `container.xml`, OPF, ≥1 XHTML).
- Validar que imagens da Frente D entram no manifesto.
- Sem rede: tudo local.

### Riscos
- `mimetype` deve ser o **primeiro** arquivo do zip e **sem compressão** (regra EPUB) — QuickEPUB cuida; no hand-built, atenção.
- Validação formal (EPUBCheck) é Java/externo — não rodar em teste; validar estrutura mínima no teste e EPUBCheck manual no dev.

### Definition of Done / o que teremos
- Todo produto gera um `.epub` válido, com a identidade visual do nicho, imagens e capa, indexado e baixável no painel.
- Base pronta para o **leitor web/flipbook** da Sprint D (reusa o mesmo XHTML).

---

## A3 — Torneio de capas (melhor de N por score de visão)  ·  esforço: **medium**  ·  impacto: **alto**

### Objetivo
Hoje geramos **uma** capa e o QA de visão (`ClaudeVisionCoverQa`) só **aprova/reprova**. Passar a gerar **N
candidatas** (variando direção de arte/cena/cor) e **escolher a melhor por score comparativo** (thumbnail test,
contraste, legibilidade do título, aderência ao gênero).

### Base de pesquisa
- AIBookArt/WriteStats 2026: decisão de compra em **<2s**; o **thumbnail** decide; **cor quente +34% CTR**;
  **ilustrado > fotográfico**; sinalização de gênero é o que faz o leitor "achar" o livro.

### Abordagem (sem biblioteca nova)
- Reusar a cadeia free-first do `IMediaGateway` (Gemini→Cloudflare→HF→Pollinations) para gerar **N** fundos/capas
  a partir de variações do `CoverPlanDto` (ex.: 2 cenas + 2 esquemas de cor → 3–4 candidatas).
- Transformar `ICoverQa` de booleano em **score 0–100** com critérios explícitos (já é visão; só mudar o prompt
  `prompts/media/cover-qa.md` para devolver score por critério) e **escolher o maior**.
- Avaliar a capa **reduzida a ~150px** (thumbnail test programático) além do tamanho cheio.

### Encaixe arquitetural
- `CoverJobHandler.RenderAsync`: hoje faz `TryFullAiCoverAsync ?? composer.RenderCover`. Generalizar para um
  **loop de candidatas** → lista de PNGs → `coverQa.ScoreAsync` em cada → escolhe o melhor; fallback Skia se todas ruins.
- `ICoverQa`: adicionar `Task<CoverScore> ScoreAsync(byte[] png, string title, ...)` (mantém o `ReviewAsync` atual para compat ou substitui).
- Gating de custo: setting `cover.tournamentSize` (default 1 = comportamento atual; 3 quando ligado) para controlar consumo de cota.
- Sem migration (a capa escolhida grava no mesmo `ContentPaths.Cover`). Opcional: guardar as rejeitadas em `cover-candidates/` para o loop de estilo E15.

### Passos
1. Ajustar `prompts/media/cover-qa.md` para JSON com `score`, `thumbnailScore`, `contrast`, `titleLegible`, `genreFit`, `issues`.
2. Estender `ICoverQa`/`ClaudeVisionCoverQa` com `ScoreAsync`.
3. No `CoverJobHandler`, gerar N candidatas (variações do plano) respeitando `cover.tournamentSize` e cota.
4. Escolher a de maior score; persistir; logar score e provedor vencedor.

### Testes
- Fake `ICoverQa` com scores fixos → asserta que a de maior score é escolhida.
- `tournamentSize=1` reproduz exatamente o comportamento atual (não-regressão).
- Cota esgotada no meio → usa as candidatas que deu + fallback Skia.

### Definition of Done / o que teremos
- Capa final é a **melhor de N**, medida por critérios que correlacionam com venda (thumbnail/contraste/gênero).
- Custo controlado por setting; rejeitadas viram material de aprendizado para E15.

---

## Ordem sugerida e fechamento da sprint
```
A1 continuidade (low, 1 prompt + 1 handler)  →  A3 torneio de capas (reusa QA)  →  A2 EPUB (migration + renderer)
```
**Ao final da Sprint A:** e-book coeso, em PDF **e** EPUB, com a melhor capa de várias. Três mudanças visíveis ao
cliente, sem tocar no frontend além de listar o novo artefato. `dotnet test Ebook.slnx` verde.

## Fontes
- [Best AI Ebook Generators 2026 — Inkfluence](https://www.inkfluenceai.com/blog/best-ai-ebook-generators-2026)
- [QuickEPUB (NuGet)](https://www.nuget.org/packages/QuickEPUB) · [EpubSharp](https://github.com/asido/EpubSharp) · [Willowcat.EbookCreator](https://github.com/cjprieb/Willowcat.EbookCreator)
- [Book Cover Design Trends 2026 — AIBookArt](https://www.aibookart.com/blog/book-cover-trends-2026)
- [Book Cover Design Psychology — WriteStats](https://writestats.com/book-cover-design-psychology-what-makes-readers-click-buy/)
