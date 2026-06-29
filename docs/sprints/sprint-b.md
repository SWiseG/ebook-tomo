# Sprint B — Profundidade editorial + gate de qualidade

> **Meta da sprint:** fechar lacunas de "produto-ready" e qualidade. Adicionar **DOCX** (formato editável que
> remove objeção de compra), transformar a **auditoria de conversão** (que já existe como query manual) em um
> **gate automático** antes de publicar, e elevar o polimento editorial do PDF/EPUB com base nas regras de
> design premium.
>
> Itens: **B1** export DOCX · **B2** auditoria como gate de publicação · **B3** polish editorial (grid/whitespace/data-viz).
> Pré-requisito: Sprint A (o `IEbookExporter`/mapeamento de blocos facilita o DOCX).

---

## B1 — Export DOCX  ·  esforço: **medium**  ·  impacto: **médio**

### Objetivo
Entregar versão **.docx** editável. Para o comprador, "posso editar/personalizar" é argumento de venda; para
lead magnets e PLR (private label rights) é praticamente obrigatório.

### Base de pesquisa
- Comparativo de bibliotecas Word .NET 2026 (HackerNoon) + NuGet: **DocumentFormat.OpenXml 3.5.1** é o SDK oficial
  da Microsoft, **MIT, ativamente mantido** (release jan/2026), nativo .NET — encaixa no .NET 10 do projeto.

### Biblioteca / abordagem
- **DocumentFormat.OpenXml** (recomendada). Verbosa, mas robusta e sem dependência paga.
- Alternativa wrapper: **Openize.OpenXML-SDK** (menos linhas) — avaliar só se a verbosidade incomodar.
- Reusar o **mesmo `PdfBook`/`MarkdownBlock[]`** da Sprint A: um `DocxRenderer` mapeia blocos → parágrafos/estilos Word
  (Heading 1/2/3, body, listas, citação, caixa = tabela sombreada, imagem = `Drawing`). Aplicar fontes/cores do nicho via estilos nomeados.

### Encaixe arquitetural
- `IEbookExporter` da Sprint A ganha um `DocxRenderer` irmão do `EpubRenderer` (Infrastructure/Content).
- `ArtifactType.Docx` → **migration** + **rebuild**.
- `ContentJobs.Docx = "ebook.docx"` + `DocxJobHandler`, `IdempotencyKey` `docx:{productId}`. Re-entrante.
- `ContentPaths.Docx(slug)`; indexar artefato; expor no painel.

### Passos
1. Adicionar pacote `DocumentFormat.OpenXml` ao `Ebook.Infrastructure`.
2. `ArtifactType.Docx` + migration + rebuild.
3. `DocxRenderer`: estilos por paleta + mapeamento de blocos + capa (imagem em página própria).
4. `DocxJobHandler` no pipeline (paralelo ao EPUB/PDF).

### Testes
- Renderizar fixture → abrir o `.docx` (é um zip OpenXML) e validar partes (document.xml, estilos, ≥1 imagem).
- Não-regressão: blocos não suportados caem em parágrafo simples (espelha o "modo seguro" do PDF).

### Definition of Done / o que teremos
- Todo produto também em `.docx` editável com a identidade do nicho, indexado e baixável.
- Com EPUB (A2) + PDF + DOCX, somos "publication-ready" multi-formato a partir de **uma fonte**.

---

## B2 — Auditoria de conversão como GATE de publicação  ·  esforço: **medium**  ·  impacto: **alto**

### Objetivo
A auditoria já existe (`ConversionAudit`, `prompts/ebook/audit.md`, endpoint `/products/{id}/audit`, UI) mas é
**manual**. Transformá-la em **gate automático**: nenhum produto avança para publicação com score abaixo do limiar
— ele volta para iteração (regenerar capítulos fracos / reescrever headline) antes de ir ao ar.

### Base de pesquisa
- docs/11 Fase 4 (checklist de conversão) + docs/19 "Frente E".
- Mercado: o que diferencia é **consistência**; um gate garante piso de qualidade em produção autônoma 24/7.

### Abordagem (reusa o que existe)
- A lógica de pontuação já está em `ConversionAudit.cs` (purpose `ebook.review`/audit + parser). Em vez de só
  responder a query, **chamá-la no pipeline** após o manuscrito/review e **decidir**:
  - `score ≥ limiar` → segue para Cover/PDF.
  - `score < limiar` → emite evento/decisão de iteração (regenera os itens reprovados) e re-audita, com **teto de tentativas** para não loopar.

### Encaixe arquitetural
- Reusar a transição de estágio do agregado `Product` (`AdvanceStage`) — **não** setar status direto (regra do CLAUDE.md).
- Inserir o gate no `ReviewJobHandler` (ou logo após), antes de enfileirar `Cover`.
- Setting `audit.gateMinScore` (default p.ex. 70) e `audit.maxRetries` (default 1) — gating configurável; default não trava produção legada.
- Emitir evento de domínio (ex.: `ManuscriptAuditFailed`) → Outbox → handler idempotente que enfileira a iteração. Sem migration nova (usa Settings + eventos existentes).
- Registrar o score no MetaJson do artefato/produto para o dashboard.

### Passos
1. Extrair a auditoria para um serviço reutilizável (se hoje está acoplada à query handler).
2. Inserir a decisão no pipeline pós-review com limiar/tentativas por Settings.
3. Caminho de falha: regenerar apenas o que reprovou (hook/CTA/headline) e re-auditar, respeitando o teto.
4. Expor o último score na tela do produto (já há UI de auditoria — só persistir o resultado do gate).

### Testes
- Fake IA com score abaixo do limiar → produto não avança e dispara iteração; acima → avança.
- Teto de tentativas respeitado (não entra em loop).
- Default desligado/limiar baixo reproduz comportamento atual (não-regressão).

### Definition of Done / o que teremos
- Piso de qualidade garantido: nada publica abaixo do limiar de conversão. O "linter de persuasão" deixa de ser opcional.

---

## B3 — Polish editorial (grid, whitespace, data-viz)  ·  esforço: **medium**  ·  impacto: **médio**

### Objetivo
Subir o PDF/EPUB de "bom" para "premium" com os fatores que a pesquisa associa a profissionalismo: **grid
consistente, whitespace generoso, hierarquia clara, minimalismo** — e melhorar os **infográficos** (que hoje são
bandas de métricas) com gráficos de dados reais quando fizer sentido.

### Base de pesquisa
- Kulokale/FlipBuilder 2026: whitespace + grid + hierarquia + minimalismo = percepção de qualidade; "readers seldom
  notice good typography, but a professional layout builds trust".

### Biblioteca / abordagem
- **Grid/whitespace/hierarquia:** ajuste de margens, baseline e escala tipográfica no `QuestPdfRenderer` e no CSS do EPUB. Sem lib.
- **Data-viz real:** hoje `ComposeInfographics` desenha bandas de métricas no Skia. Para gráficos (barra/linha/pizza),
  avaliar **ScottPlot 5** (MIT, renderiza via **SkiaSharp** — já é dependência do projeto, exporta PNG) e injetar como `MarkdownBlock.Image`.
  Manter o fallback atual quando não houver série numérica.

### Encaixe arquitetural
- `IImageComposer.RenderInfographic` ganha (ou um novo método) suporte a séries → ScottPlot → PNG, com as cores da paleta do nicho.
- Sem migration. Sem novo job. Mudanças contidas no renderer + no parser (reconhecer série de dados num bloco).

### Passos
1. Revisar escala tipográfica e margens (PDF + CSS EPUB) contra a régua do docs/11 (40/26/18/12, line-height 1.5).
2. Avaliar ScottPlot; se aprovado, adicionar pacote ao `Ebook.Infrastructure` e implementar gráfico a partir de série numérica.
3. Garantir coerência de cor (paleta do nicho) e fallback para a banda de métricas atual.

### Testes
- Composição de gráfico a partir de série fixa → PNG não-vazio, dimensões corretas.
- Bloco sem série numérica → cai no infográfico atual (não-regressão).

### Definition of Done / o que teremos
- Layout com respiro e hierarquia consistentes; infográficos com gráficos reais quando há dados. Percepção de "feito por designer".

---

## Ordem sugerida e fechamento
```
B2 gate (reusa auditoria, destrava qualidade)  →  B1 DOCX (reusa exporter da Sprint A)  →  B3 polish/data-viz
```
**Ao final da Sprint B:** trio de formatos (PDF/EPUB/DOCX), publicação com piso de qualidade automático e
acabamento editorial premium. `dotnet build -warnaserror` + `dotnet test` verdes.

## Fontes
- [Definitive C# Word Library Comparison 2026 — HackerNoon](https://hackernoon.com/the-definitive-c-word-library-comparison-for-2026) · [DocumentFormat.OpenXml (NuGet)](https://www.nuget.org/packages/documentformat.openxml) · [Open-XML-SDK (GitHub)](https://github.com/dotnet/Open-XML-SDK)
- [Great Ebook Layout Design Tips — Kulokale](https://kulokale.com/great-ebook-layout-design-tips/)
- [ScottPlot](https://scottplot.net/) (charting via SkiaSharp, MIT)
