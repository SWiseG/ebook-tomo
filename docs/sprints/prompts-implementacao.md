# Prompts de Implementação — Sprints A–E

> **Como usar:** Cole cada prompt como primeira mensagem de uma sessão nova do Claude Code.
> Cada sessão termina com `dotnet build Ebook.slnx -warnaserror` + `dotnet test Ebook.slnx` verdes.
> Não pule etapas — cada número depende dos anteriores.
> Referência completa: `docs/sprints/sprint-{a,b,c,d,e}.md`.

---

## Prompt 0 — Doc Drift (Sprint E Cat.1) · sem código · fazer PRIMEIRO

```
Você está no projeto ebook-tomo (ASP.NET Core .NET 10, solução Ebook.slnx).
Leia CLAUDE.md e docs/sprints/sprint-e.md (Categoria 1) antes de começar.

Corrija os 6 itens de doc drift listados lá (E1–E6). Resumo do que fazer:

E1: Renumerar docs/12-landing-page-roadmap.md → docs/17-landing-page-roadmap.md (corrige conflito de numeração).
E2: Criar docs/17 ou redirecionar referências ao doc 17 inexistente para o novo arquivo renomeado acima.
E3: Atualizar docs/README.md e docs/06-estrutura-de-pastas.md: "ASP.NET Core 8" → ".NET 10", "Ebook.sln" → "Ebook.slnx".
E4: Reescrever docs/06-estrutura-de-pastas.md com a árvore real de src/ (use o filesystem, não suponha).
E5: Em docs/11-padrao-editorial.md, na Parte 3, marcar Frente D / infográficos / ConversionAudit como ✅ (já implementados);
    manter apenas o gate automático (B2) como pendente.
E6: Em docs/README.md, atualizar a seção "Status" para refletir o estado atual do portfólio.

Regras:
- Só editar arquivos de docs/. Zero toque em código C# ou TypeScript.
- Atualizar o índice em docs/README.md se renumerar arquivos.
- Ao final, listar os arquivos alterados.

Ao final temos: documentação fiel ao código, sem mais confusão de versão ou numeração.
```

---

## Prompt 1 — A1: Passe de Continuidade · sem migration

```
Você está no projeto ebook-tomo (ASP.NET Core .NET 10, solução Ebook.slnx).
Leia CLAUDE.md e docs/sprints/sprint-a.md (seção A1) antes de começar.

CONTEXTO:
- ChapterJobHandler gera capítulos isolados → ReviewJobHandler faz revisão.
- IAiGateway em Application/Ai (propósitos cacheados por hash).
- IPromptLibrary carrega prompts de /prompts/{area}/{nome}.md com placeholders {{var}}.
- IFileStore grava conteúdo; ContentPaths.Manuscript(slug) devolve o caminho.
- Nenhum módulo chama IA diretamente — sempre via IAiGateway.

TAREFA — criar o passe de coesão após o ReviewJobHandler:

1. Criar prompts/ebook/continuity.md
   - Placeholders: {{outline}}, {{manuscript}}
   - Saída: JSON estrito com bridges[] (ponte ao fim de cada cap), removals[] (trechos repetidos), hookFixes[] (ajustes de abertura)
   - Seguir o estilo de prompts/ebook/audit.md como referência de formato

2. Estender o ReviewJobHandler (src/Ebook.Application ou Infrastructure/Content — locate primeiro):
   - Após a revisão existente, chamar IAiGateway com purpose "ebook.continuity" e o prompt acima
   - Aplicar os patches de forma conservadora: inserir bridges ao fim de cada capítulo, remover trechos
     por correspondência exata, ajustar hooks de abertura
   - Regravar o manuscrito no IFileStore
   - Idempotência: marcar no MetaJson (ou comparar hash) para não repassar em re-entrega
   - Logar nº de bridges inseridas, repetições removidas

3. Testes (sem mock framework — fakes manuais, padrão do projeto):
   - Fake IAiGateway determinístico devolvendo JSON conhecido → bridges entraram, repetições saíram
   - Rodar 2× não duplica bridges (idempotência)

Ao final: dotnet build Ebook.slnx -warnaserror && dotnet test Ebook.slnx

ENTREGÁVEL: manuscritos com fio condutor real. 1 chamada de IA extra por e-book (não por capítulo).
```

---

## Prompt 2 — A3: Torneio de Capas · sem migration

```
Você está no projeto ebook-tomo (ASP.NET Core .NET 10, solução Ebook.slnx).
Leia CLAUDE.md e docs/sprints/sprint-a.md (seção A3) antes de começar.

CONTEXTO:
- CoverJobHandler.RenderAsync hoje faz TryFullAiCoverAsync ?? composer.RenderCover.
- ICoverQa / ClaudeVisionCoverQa aprovam/reprovam booleano via prompts/media/cover-qa.md.
- IMediaGateway (cadeia free-first: Gemini→Cloudflare→HF→Pollinations) gera imagens.
- SettingKeys já existem; adicionar cover.tournamentSize (default 1 = comportamento atual).
- CoverPlanDto tem os dados de direção de arte.

TAREFA:

1. Atualizar prompts/media/cover-qa.md
   - Output: JSON com score (0-100), thumbnailScore, contrast, titleLegible, genreFit, issues[]
   - Avaliar a imagem também reduzida ~150px (thumbnail test)

2. Estender ICoverQa / ClaudeVisionCoverQa:
   - Adicionar Task<CoverScore> ScoreAsync(byte[] png, string title, ...) 
   - Manter ReviewAsync atual para compatibilidade (ou substituir internamente)
   - CoverScore: record com os campos acima

3. Refatorar CoverJobHandler.RenderAsync:
   - Ler cover.tournamentSize dos Settings
   - Gerar N variantes do CoverPlanDto (variar cena e esquema de cor — 2 variações cada = até 4 candidatas)
   - Chamar ScoreAsync em cada candidata via IMediaGateway (visão)
   - Escolher a de maior score; fallback Skia se todas < limiar ou cota esgotada
   - Persistir em ContentPaths.Cover (mesmo path de antes)
   - Logar score vencedor e provedor

4. Testes:
   - Fake ICoverQa com scores fixos → maior score é escolhido
   - tournamentSize=1 → comportamento idêntico ao atual (não-regressão)
   - Cota esgotada → usa candidatas disponíveis + fallback Skia

Ao final: dotnet build Ebook.slnx -warnaserror && dotnet test Ebook.slnx

ENTREGÁVEL: capa final = melhor de N, custo controlado por setting.
```

---

## Prompt 3 — A2: Export EPUB · com migration

```
Você está no projeto ebook-tomo (ASP.NET Core .NET 10, solução Ebook.slnx).
Leia CLAUDE.md e docs/sprints/sprint-a.md (seção A2) antes de começar.

CONTEXTO:
- QuestPdfRenderer mapeia PdfBook (= manuscrito + PdfBookComposer) → PDF.
- PdfBook contém MarkdownBlock[] (Heading, Paragraph, BulletList, PullQuote, Callout, Image, etc.).
- ArtifactType é enum persistido como string — adicionar valor = migration.
- IArtifactStore.WriteBytesAsync + Artifact.Create indexam artefatos.
- ContentPaths tem métodos estáticos para cada caminho.
- Jobs: ContentJobs enum + handler com IdempotencyKey + registro automático por scan.
- Regra de dependência: Application declara interface, Infrastructure implementa.

TAREFA (nesta ordem para não quebrar o rebuild):

1. ArtifactType.Epub + migration
   dotnet dotnet-ef migrations add AddEpubArtifactType \
     --project src/Ebook.Infrastructure \
     --startup-project src/Ebook.Api \
     --output-dir Persistence/Migrations
   Rebuild antes de continuar.

2. ContentPaths.Epub(slug) — mesmo padrão dos outros métodos.

3. ContentJobs.Epub = "ebook.epub" + payload record (ProductId + Slug).

4. Interface IEbookExporter em Application/Content:
   Task<byte[]> ExportEpubAsync(PdfBook book, CoverData cover, NichePalette palette);

5. EpubRenderer em Infrastructure/Content — hand-built (System.IO.Compression):
   - mimetype: primeiro arquivo, sem compressão (ZipArchiveEntry.CompressionLevel = NoCompression)
   - META-INF/container.xml padrão
   - OPF: manifesto (todos os XHTML + CSS + imagens + capa) + spine
   - nav.xhtml (navegação EPUB 3)
   - chapters/{n}.xhtml: mapear cada MarkdownBlock → XHTML semântico
     (h1/h2/h3, p, ul/li, blockquote para PullQuote, aside para Callout, img para Image)
   - style.css: tipografia da paleta (fontes do FontRegistry, cores do nicho, line-height 1.5)
   - capa: cover.xhtml com a imagem PNG como primeira entrada do spine
   - Imagens: incluir os bytes das imagens do FileStore no zip

6. EpubJobHandler em Infrastructure/Content:
   - Implementa IJobHandler<EpubPayload>
   - IdempotencyKey: "epub:{productId}"
   - Lê PdfBook (ou remonta a partir do manuscrito — ver como PdfJobHandler faz)
   - Chama EpubRenderer, grava via IArtifactStore.WriteBytesAsync, indexa Artifact
   - Re-entrante: pula se artefato EPUB já existe e não está desatualizado

7. Disparar EpubJobHandler no pipeline: enfileirar junto ou logo após o PdfJobHandler.

8. Testes:
   - Fixture de PdfBook → ExportEpubAsync → abrir zip em memória:
     - mimetype é o primeiro entry e não está comprimido
     - container.xml existe
     - ≥1 arquivo .xhtml no manifesto
     - capa no spine
   - Sem rede; imagens e cover como bytes de fixture.

Ao final: dotnet build Ebook.slnx -warnaserror && dotnet test Ebook.slnx

ENTREGÁVEL: todo produto gera .epub válido com identidade do nicho, indexado no painel.
Base pronta para o leitor web/flipbook (Sprint D1 reusa este XHTML).
```

---

## Prompt 4 — B2: Gate de Auditoria · sem migration nova

```
Você está no projeto ebook-tomo (ASP.NET Core .NET 10, solução Ebook.slnx).
Leia CLAUDE.md e docs/sprints/sprint-b.md (seção B2) antes de começar.

CONTEXTO:
- ConversionAudit já existe (locate em src/) — audita o manuscrito via IAiGateway (purpose "ebook.review"/audit).
- O endpoint /products/{id}/audit e a UI já existem — é manual hoje.
- Product é um agregado com transições de estado via métodos (AdvanceStage, etc.) — NUNCA setar Status direto.
- Domain Events → Outbox (IUnitOfWork.SaveChangesAsync) → OutboxDispatcher → IDomainEventHandler<T> idempotente.
- SettingKeys: adicionar audit.gateMinScore (default 70) e audit.maxRetries (default 1).
- ReviewJobHandler roda após os capítulos e antes do Cover.

TAREFA — transformar auditoria em gate automático no pipeline:

1. Extrair/reusar ConversionAuditService (se hoje acoplado ao QueryHandler, refatorar para serviço injetável).
   Não duplicar a lógica — apenas tornar o score acessível ao pipeline.

2. No ReviewJobHandler (ou num ContinuityJobHandler se A1 já criou um — checar o estado atual):
   Após a revisão e o passe de continuidade:
   a. Chamar ConversionAuditService → obtém score
   b. Se score >= audit.gateMinScore → enfileira Cover (fluxo atual)
   c. Se score < limiar E tentativas < audit.maxRetries:
      - Emitir evento de domínio ManuscriptAuditFailed (ou similar) → Outbox → handler idempotente
      - Handler re-enfileira a revisão/regeneração dos itens reprovados (capítulos fracos, headline, hook)
      - Incrementar contador de tentativas (MetaJson do produto)
   d. Se score < limiar E tentativas >= maxRetries → avança mesmo assim (não trava produção), logar aviso

3. Persistir o último score no MetaJson do produto para o dashboard (campo auditScore).

4. Configurar gating:
   - audit.gateMinScore default 0 (zero = gate desligado = comportamento atual, não quebra produção legada)
   - Documentar em SettingKeys

5. Testes:
   - Fake ConversionAuditService com score 50, limiar 70 → ManuscriptAuditFailed emitido, Cover não enfileirado
   - Score 80, limiar 70 → Cover enfileirado
   - Tentativas esgotadas → avança mesmo com score baixo (não loopa)
   - gateMinScore=0 → comportamento idêntico ao atual (não-regressão)

Ao final: dotnet build Ebook.slnx -warnaserror && dotnet test Ebook.slnx

ENTREGÁVEL: nenhum produto publica abaixo do limiar sem pelo menos uma tentativa de melhoria.
```

---

## Prompt 5 — B1: Export DOCX · com migration

```
Você está no projeto ebook-tomo (ASP.NET Core .NET 10, solução Ebook.slnx).
Leia CLAUDE.md e docs/sprints/sprint-b.md (seção B1) antes de começar.

CONTEXTO:
- A2 (Prompt 3) já criou IEbookExporter e o padrão de mapeamento MarkdownBlock → formato.
- PdfBook/MarkdownBlock[] é a fonte comum — o DocxRenderer é irmão do EpubRenderer.
- ArtifactType é enum como string; ContentPaths e ContentJobs seguem o mesmo padrão do EPUB.
- Biblioteca: DocumentFormat.OpenXml (MIT, .NET 10 compatível) — adicionar ao Ebook.Infrastructure.

TAREFA (nesta ordem):

1. dotnet add src/Ebook.Infrastructure package DocumentFormat.OpenXml
   ArtifactType.Docx + migration + rebuild.

2. ContentPaths.Docx(slug) + ContentJobs.Docx = "ebook.docx".

3. DocxRenderer em Infrastructure/Content:
   Mapear MarkdownBlock[] → parágrafos Word com estilos nomeados:
   - Heading1/2/3 (ParagraphStyleId = "Heading1" etc.)
   - Body (Normal)
   - BulletList → NumberingDefinition
   - PullQuote → estilo "Quote" com itálico + recuo
   - Callout → tabela 1×1 com fundo cinza claro (cor da paleta)
   - Image → Drawing inline (bytes da imagem do FileStore)
   - Capa: primeira página com a imagem da capa em largura total
   Aplicar fonte primária/secundária da NichePalette (via FontRegistry ou nome da fonte).
   Sem imagem suportada → parágrafo placeholder "[imagem]".

4. DocxJobHandler:
   - IdempotencyKey: "docx:{productId}"
   - Mesmo padrão do EpubJobHandler
   - Re-entrante

5. Disparar no pipeline paralelo ao EPUB/PDF.

6. Testes:
   - Fixture → DocxRenderer → abrir como zip (é OpenXML) e verificar:
     - word/document.xml existe e não está vazio
     - word/styles.xml existe
     - ≥1 imagem em word/media/
   - Bloco sem suporte → cai em parágrafo simples, não lança exceção

Ao final: dotnet build Ebook.slnx -warnaserror && dotnet test Ebook.slnx

ENTREGÁVEL: todo produto também em .docx editável com identidade do nicho.
Com PDF + EPUB + DOCX = publication-ready multi-formato a partir de uma fonte.
```

---

## Prompt 6 — B3: Polish Editorial · sem migration

```
Você está no projeto ebook-tomo (ASP.NET Core .NET 10, solução Ebook.slnx).
Leia CLAUDE.md e docs/sprints/sprint-b.md (seção B3) antes de começar.

CONTEXTO:
- QuestPdfRenderer usa Style.From(palette) — paleta do nicho já disponível.
- ComposeInfographics existe em algum IImageComposer/SkiaSharp — localizar antes de editar.
- FontRegistry tem as 6 fontes do projeto; NichePalette tem primary/secondary/accent.
- EpubRenderer (A2) tem o CSS do EPUB.
- docs/11 define a escala tipográfica: 40/26/18/12pt, line-height 1.5.

TAREFA:

1. Revisar escala tipográfica no QuestPdfRenderer:
   - H1=40, H2=26, H3=18, body=12, caption=10
   - line-height equivalente no QuestPDF (LineHeight/Spacing)
   - Margens: mínimo 20mm laterais, 25mm topo/base
   - Verificar se já está conforme; ajustar só o que divergir

2. Mesmo ajuste no CSS gerado pelo EpubRenderer (Prompt 3):
   - font-size em rem coerente com a escala acima
   - line-height: 1.5; margin nas sections

3. Avaliar ScottPlot 5 para data-viz:
   - dotnet add src/Ebook.Infrastructure package ScottPlot (se decidir prosseguir)
   - ScottPlot já usa SkiaSharp (já é dependência do projeto) → exporta PNG
   - Adicionar método IImageComposer.RenderChartAsync(ChartData) → byte[] PNG
   - ChartData: título, tipo (Bar/Line), series[], cores da paleta
   - No parser de blocos: reconhecer bloco de dados (ex.: ``` data ou tabela numérica) → ChartData → RenderChartAsync → MarkdownBlock.Image
   - Fallback: se não há série numérica → ComposeInfographics atual (não-regressão)

4. Testes:
   - RenderChartAsync com série fixa → PNG não-vazio, dimensões corretas (ex.: 800×400)
   - Bloco sem série numérica → cai no infográfico atual (não lança exceção)
   - Build -warnaserror passa com ScottPlot

Ao final: dotnet build Ebook.slnx -warnaserror && dotnet test Ebook.slnx

ENTREGÁVEL: layout com respiro e hierarquia consistentes; gráficos reais quando há dados.
```

---

## Prompt 7 — C1 + C2: Variantes de LP + Smart Traffic · com migration

```
Você está no projeto ebook-tomo (ASP.NET Core .NET 10, solução Ebook.slnx).
Leia CLAUDE.md e docs/sprints/sprint-c.md (seções C1 e C2) antes de começar.

CONTEXTO:
- LpJobHandler gera 1 LP via LandingPageBuilder → ArtifactType.LpBundle (HTML auto-contido).
- LpLab (EnqueueTestLpCommand + GenerateTestLp) já gera variantes internamente — reusar.
- Pixel /px.gif + AnalyticsEvent + MetricDaily medem visita/checkout/venda por produto.
- SettingKeys: adicionar lp.variantCount (default 1), lp.smartTraffic (default false).

TAREFA — executar C1 depois C2 na mesma sessão:

--- C1: Variantes ---

1. Migration: nova tabela LpVariant (id, productId, variantTag, filePath, createdAt).
   dotnet dotnet-ef migrations add AddLpVariant \
     --project src/Ebook.Infrastructure \
     --startup-project src/Ebook.Api \
     --output-dir Persistence/Migrations
   Rebuild.

2. LpJobHandler: ler lp.variantCount; gerar N variantes via LandingPageBuilder com variações de:
   - Headline (3 versões de copy: problema/solução/resultado)
   - Ordem de seções (prova social antes vs depois do hero)
   - Rótulo do CTA (ex.: "Quero agora" vs "Baixar agora" vs "Acessar")
   Persistir cada variante como arquivo separado (slug + "-v{n}"); inserir LpVariant no banco.

3. Pixel: adicionar dimensão variant ao AnalyticsEvent (campo variantTag — nullable, compat).
   Servir variante por ?v={tag} na rota existente da LP.

4. Testes:
   - variantCount=2 → 2 registros LpVariant, 2 arquivos distintos
   - variantCount=1 → 1 registro, comportamento idêntico ao atual (não-regressão)
   - Pixel inclui variantTag quando presente

--- C2: Smart Traffic (roteamento por conversão) ---

5. LpVariantRouter em Domain (sem dependência de infra — função pura):
   - Entrada: lista de VariantStats (variantTag, visits, conversions)
   - Algoritmo: Thompson Sampling (poucas linhas — sem lib):
     - Para cada variante: amostrar Beta(conversions+1, visits-conversions+1)
     - Retornar o variantTag com maior amostra
   - Sem dados (visits=0 em todas) → round-robin / uniforme
   - Injetar IRandom (interface fina) para testabilidade com seed fixo

6. Aplicar LpVariantRouter no endpoint que serve a LP:
   - Ler MetricDaily por (productId, variantTag) via IMetricsReader
   - Montar VariantStats; chamar Router; redirecionar (ou servir) a variante escolhida
   - Respeitar setting lp.smartTraffic: se false → round-robin simples (compat)

7. Testes:
   - Variante com conversion rate maior recebe maioria das amostras após 1000 rodadas (seed fixo)
   - Sem dados → distribuição ~uniforme (diferença < 10% entre variantes)
   - smartTraffic=false → round-robin (não-regressão)

Ao final: dotnet build Ebook.slnx -warnaserror && dotnet test Ebook.slnx

ENTREGÁVEL: produto publica N variantes rastreáveis; tráfego flui automaticamente para a que converte mais.
```

---

## Prompt 8 — C3 + C4: Promoção Automática + Personalização por Origem

```
Você está no projeto ebook-tomo (ASP.NET Core .NET 10, solução Ebook.slnx).
Leia CLAUDE.md e docs/sprints/sprint-c.md (seções C3 e C4) antes de começar.

CONTEXTO:
- OptimizationService roda ciclo mensal; decisão "Iterate" já sugere refreshLandingPage.
- LpVariant e MetricDaily por variante existem (Prompt 7).
- roi.autoExecute (SettingKey existente) controla execução automática vs proposta.
- LandingPageBuilder gera o HTML; a LP é estático (Cloudflare/Nginx).

TAREFA:

--- C3: Promoção automática da vencedora ---

1. Calcular vencedora no Domain (função pura, sem infra):
   - PromotionEligibility(List<VariantStats>, minVisits, minDays) → VariantTag? vencedora
   - Critério: variante com conversion rate > melhor rival + margem de 5pp E volume >= minVisits E janela >= minDays
   - Retorna null se critério não atingido

2. Estender OptimizationService (decisão Iterate):
   - Calcular elegibilidade com Settings lp.promote.minVisits (default 100), lp.promote.minDays (default 7)
   - Se vencedora identificada + roi.autoExecute=true → emitir evento LpVariantPromoted → Outbox → handler:
     - Copiar arquivo da variante vencedora para o path principal (slug sem ?v)
     - Marcar as demais como inativas no banco
     - Opcionalmente: enfileirar nova leva de desafiantes (EnqueueTestLpCommand)
   - Se roi.autoExecute=false → registrar proposta (log/MetaJson), sem executar

3. Testes:
   - Vencedora clara + volume suficiente + autoExecute=true → LpVariantPromoted emitido
   - Volume insuficiente → null, sem evento
   - autoExecute=false → proposta registrada, sem evento

--- C4: Personalização por origem de tráfego ---

4. No LandingPageBuilder, injetar um pequeno script JS no HTML gerado:
   - Script: lê UTM params da URL (utm_source, utm_campaign, utm_term)
   - Substitui tokens pré-definidos no HTML (data-dtr-headline, data-dtr-eyebrow)
     por variantes mapeadas em um objeto JS embutido
   - Fallback: texto padrão (sem parâmetros → nenhuma mudança)
   - O mapa origem→texto vem de um campo opcional da copy (ou Settings lp.dtrMap JSON)
   - Compatível com C1/C2: funciona em qualquer variante

5. Testes:
   - HTML gerado contém os tokens data-dtr-* e o script de substituição
   - Sem configuração dtrMap → texto padrão presente (não-regressão)

Ao final: dotnet build Ebook.slnx -warnaserror && dotnet test Ebook.slnx

ENTREGÁVEL: sistema converge sozinho para melhor LP; a página fala a língua de cada campanha.
```

---

## Prompt 9 — D2: Audiobook · com migration

```
Você está no projeto ebook-tomo (ASP.NET Core .NET 10, solução Ebook.slnx).
Leia CLAUDE.md e docs/sprints/sprint-d.md (seção D2) antes de começar.

CONTEXTO:
- ITtsEngine (Piper TTS) e IVideoComposer/FFmpeg já existem no projeto (Video/).
- O módulo de vídeo está desligado (video.enabled=false) mas a infra de voz/audio está lá.
- Manuscrito está em FileStore (ContentPaths.Manuscript); capítulos são MarkdownBlock[].
- ArtifactType enum como string; mesmo padrão de ContentPaths/ContentJobs/handler.
- SettingKeys: adicionar audiobook.enabled (default false — gating de CPU).

TAREFA:

1. ArtifactType.Audiobook + migration + rebuild.

2. ContentPaths.Audiobook(slug) + ContentJobs.Audiobook = "ebook.audiobook".

3. Limpeza do manuscrito para narração:
   - Método TextCleaner.ForNarration(MarkdownBlock[]) → string[] (um por capítulo)
   - Remove: marcadores markdown (#, **, *, [], ()), URLs, descrições de imagem ([alt text])
   - Mantém: texto corrido, subtítulos como sentenças

4. AudiobookJobHandler:
   - IdempotencyKey: "audiobook:{productId}"
   - Verificar audiobook.enabled → pular se false (log info)
   - Para cada capítulo: ITtsEngine.SynthesizeAsync(texto) → byte[] WAV/MP3
   - Concatenar capítulos via FFmpeg (reusar IVideoComposer ou criar IAudioComposer fino):
     ffmpeg -i "concat:cap1.mp3|cap2.mp3|..." -acodec copy output.mp3
     (se não tiver método direto, chamar ffmpeg via Process — ver como GenerateVideoJobHandler faz)
   - Normalizar volume (-2 LUFS ou equivalente)
   - Gravar via IArtifactStore; indexar Artifact

5. Testes (sem invocar Piper/FFmpeg reais):
   - Fake ITtsEngine retorna bytes fixos por capítulo
   - Fake IAudioComposer (ou IVideoComposer) concatena os bytes
   - Resultado: bytes não-vazios, nº de capítulos correto
   - audiobook.enabled=false → handler retorna sem processar

Ao final: dotnet build Ebook.slnx -warnaserror && dotnet test Ebook.slnx

ENTREGÁVEL: produtos selecionados ganham audiobook MP3 indexado e baixável.
```

---

## Prompt 10 — D1: Web Reader / Flipbook · com migration

```
Você está no projeto ebook-tomo (ASP.NET Core .NET 10, solução Ebook.slnx).
Leia CLAUDE.md e docs/sprints/sprint-d.md (seção D1) antes de começar.

CONTEXTO:
- A2 (Prompt 3) gerou EpubRenderer com capítulos em XHTML e CSS por paleta.
- A LP já é servida como estático (mesma estratégia a seguir).
- Pixel /px.gif e AnalyticsEvent já existem para instrumentação.
- LGPD: Settings LegalPrivacyUrl e consentimento já existem — reusar.

TAREFA:

1. ArtifactType.WebReader + migration + rebuild.

2. ContentPaths.WebReader(slug) + ContentJobs.WebReader = "ebook.webreader".

3. WebReaderBundler em Infrastructure/Content:
   - Reusar o XHTML dos capítulos do EPUB (ou remontar da mesma fonte PdfBook)
   - Gerar um bundle HTML auto-contido em /read/{slug}/index.html:
     a. Scroll vertical com barra de progresso (implementação base, sem lib)
     b. StPageFlip (MIT, JS) como progressive enhancement para desktop
        — embutir o JS minificado via CDN tag ou inline (avaliar tamanho; se >50kb, só scroll)
     c. Amostra grátis: capítulos 1-2 livres; demais ocultados com CTA de compra
     d. CTA aponta para a LP/checkout do produto (ContentPaths.Lp ou Settings)
   - Analytics: snippet de leitura que posta /px.gif com event=chapter_view&chapter={n}&product={id}
   - Captura de lead: formulário de e-mail ao fim do cap 2 (consentimento LGPD, link LegalPrivacyUrl)
     — submete para endpoint /products/{id}/lead (criar endpoint simples ou reusar existente)

4. WebReaderJobHandler:
   - IdempotencyKey: "webreader:{productId}"
   - Re-entrante; indexar artefato

5. Disparar no pipeline após EpubJobHandler (depende do XHTML pronto).

6. Rota pública: verificar se /read/{slug} já existe ou criar no Api (serve estático como a LP).

7. Testes:
   - Fixture PdfBook → WebReaderBundler → HTML contém:
     - capítulo 1 visível, capítulo 3+ oculto (classe hidden ou display:none)
     - CTA de compra presente
     - snippet de analytics
     - formulário de lead com link de privacidade
   - Sem rede

Ao final: dotnet build Ebook.slnx -warnaserror && dotnet test Ebook.slnx

ENTREGÁVEL: /read/{slug} = leitor web com amostra grátis, analytics de leitura e captura de lead.
Novo topo de funil sem custo de infra adicional.
```

---

## Prompt 11 — E restante: Higiene e Consolidação

```
Você está no projeto ebook-tomo (ASP.NET Core .NET 10, solução Ebook.slnx).
Leia CLAUDE.md e docs/sprints/sprint-e.md (Categorias 2, 3 e 4) antes de começar.
Este prompt é o cleanup final — todas as Sprints A–D já foram entregues.

CONTEXTO: o código está limpo. O objetivo é decidir conscientemente, não refatorar por estética.
Antes de qualquer mudança, abrir o arquivo e confirmar o que ele faz.

TAREFA — executar na ordem:

--- E7: Paleta/Brand Chain ---
Localizar: NicheStyleCatalog, PaletteCatalog, PaletteResolver, PaletteDirector, NichePalette, BrandKit, BrandResolver, BrandDirector.
Mapear o papel de cada um. Adicionar um README.md curto em src/Ebook.Application/Content/Images/ documentando a cadeia:
"catálogo = defaults estáticos → resolver = lê persistido → director = gera via IA + persiste".
Só fundir se duas camadas comprovadamente fizerem a mesma coisa. Documentar a decisão.

--- E8: PdfTheme legado ---
Verificar se algum fluxo de produção (não-teste) ainda usa PdfTheme/Style.For(theme).
Se só testes usam: substituir nas fixtures por paleta de teste; remover PdfTheme se zero referências restarem.
Se produção usa: documentar onde e deixar (sem refatorar agora).
Build verde a cada passo.

--- E9: Seletores de nicho ---
Verificar PdfThemeSelector, LpTemplateSelector, NicheStyleCatalog.Classify.
Se Classify já é o ponto central e os seletores só consomem: adicionar comentário de 1 linha em cada
("delegado para NicheStyleCatalog.Classify"). Sem refatorar.

--- E10/E11: Decisões de produto ---
Verificar Video/ e Social/:
- Video: confirmar video.enabled=false no appsettings. Adicionar comentário em VideoSchedulerJob:
  "Desligado por padrão; infra TTS/FFmpeg reaproveitada pelo AudiobookJobHandler (Sprint D2)."
- Social: confirmar que está gated/seguro; documentar em SettingKeys se houver chave.

--- E12: Prompts órfãos ---
Cruzar prompts/**/*.md com referências no código (grep por cada nome de arquivo sem extensão).
Listar órfãos. Remover só se tiver certeza (ex.: prompts/dev/echo.md — verificar se é dev-only).

--- E13/E15: ArtifactType e SettingKeys ---
Confirmar que todos os ArtifactType adicionados (Epub, Docx, Audiobook, WebReader) têm migration.
Revisar SettingKeys: comentar/remover chaves sem referência no código.

--- E16: Régua final ---
dotnet build Ebook.slnx -warnaserror && dotnet test Ebook.slnx

ENTREGÁVEL: código limpo e documentado; sistemas de estilo com papéis claros; features laterais gated
documentadas; suíte verde. Entropia zero para a próxima Sprint.
```

---

## Resumo do que temos ao final de cada prompt

| # | Prompt | Entregável final |
|---|---|---|
| 0 | Doc Drift | Docs fiéis ao código, numeração correta, versão .NET 10 |
| 1 | A1 Continuidade | Manuscritos com fio condutor; 1 chamada IA extra por e-book |
| 2 | A3 Torneio de Capas | Capa = melhor de N candidatas por score |
| 3 | A2 EPUB | Todo produto em .epub válido com identidade do nicho |
| 4 | B2 Gate de Auditoria | Nenhum produto publica abaixo do limiar sem tentativa de melhoria |
| 5 | B1 DOCX | Todo produto em .docx editável — tri-formato completo |
| 6 | B3 Polish | Layout premium; gráficos reais onde há dados |
| 7 | C1+C2 Variantes+Traffic | N LPs rastreáveis; tráfego flui para a melhor |
| 8 | C3+C4 Promoção+DTR | LP vencedora promovida sozinha; copy adapta por campanha |
| 9 | D2 Audiobook | MP3 indexado e baixável por produto |
| 10 | D1 Web Reader | /read/{slug} com amostra grátis, analytics e lead |
| 11 | E Higiene | Código limpo, papéis documentados, suíte verde |
