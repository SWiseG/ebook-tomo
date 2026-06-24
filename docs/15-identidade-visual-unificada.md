# docs/15 — Identidade visual unificada (PDF · LP · Cover) + Hero da LP

> Investigação + planejamento (sem código). Objetivo: **uma única direção de arte por produto**
> — cores, fontes, tipografia E contexto de imagens — gerada por IA e replicada com coerência nos
> três artefatos (PDF, landing page, capa). Priorizar **Gemini** na capa com contexto mais amplo.
> Elevar o **hero da LP**, hoje simples demais. Complementa docs/13 (design system do PDF) e
> docs/14 (sistema de capas).

## 1. Diagnóstico — o que JÁ está unificado vs. o que NÃO está

| Dimensão | PDF | LP | Cover | Fonte única? |
|---|---|---|---|---|
| Cores (background/accent/onDark) | ✓ | ✓ | ✓ | **SIM** — `NichePalette` via `IPaletteResolver` (docs/14 WP-1) |
| HeadingFont / BodyFont | ✓ | ✓ (Google Fonts) | ✓ | **SIM** |
| **DisplayFont** (impacto) | ✗ usa HeadingFont | ✗ hero usa HeadingFont | ✓ | **NÃO — só a capa** |
| Escala tipográfica / spacing | própria (QuestPDF) | própria (CSS) | própria (Skia) | **NÃO** — cada artefato define a sua |
| Motivos (eyebrow caps, régua accent, selo, cards de benefício) | parcial | parcial | ✓ | **NÃO** — inventados em separado |
| Contexto/estilo das imagens | `visual-plan` (por capítulo) | `lp-hero` (1 prompt) | `cover-plan.scene` | **NÃO — 3 IAs independentes** |

**O que está bom (alicerce):** a paleta já é fonte única (docs/14) — cores + 2 fontes coerentes nos
três. É a base para o resto.

**Lacunas confirmadas no código:**

1. **DisplayFont só na capa.** `NichePalette.DisplayFont` é usado em `SkiaImageComposer` (capa/cards),
   mas o **hero da LP** (`LandingPage.cs` → `.hero h1` usa `HeadingFont`) e as **aberturas de capítulo
   do PDF** não herdam o impacto display. Inconsistência tipográfica entre os artefatos.

2. **Imagens sem direção única.** Três prompts independentes, estéticas que podem divergir:
   - PDF: `prompts/ebook/visual-plan.md` (foto vs ilustração por capítulo).
   - LP: `prompts/media/lp-hero.md` ("modern editorial digital art").
   - Capa: `cover-plan.scene` → "documentary photography".
   Um mesmo produto pode sair com **capa fotográfica + capítulo ilustrado chapado + hero genérico** —
   sem um "moodboard" que amarre subject/luz/estilo/cor.

3. **Capa NÃO prioriza Gemini.** `CoverJobHandler.ResolveBackgroundAsync` usa
   `MediaBrief(..., MediaKind.Photo)` → a cadeia prioriza **bancos de foto** (Pexels/Unsplash/Pixabay),
   não Gemini. O caminho Gemini (`MediaKind.CoverWithText`, docs/14 WP-5) está **gated OFF**. E o
   contexto enviado é só a `scene` curta — sem título, promessa, mood, paleta ou estilo.

4. **Hero da LP é simples.** É fundo de cor sólida/gradiente + `h1` + sub + CTA + **thumbnail 2D**
   da capa (`CoverDataUri`). Sem ilustração de fundo, sem camadas, e — importante — o **mockup 3D**
   (`RenderMockup`, já gerado pelo `CoverJobHandler`) **não é usado na LP**. A ilustração de herói
   (`lp-hero`) aparece numa **faixa showcase mais abaixo**, não atrás do hero.

## 2. Plano (5 frentes, sem código nesta etapa)

### Frente A — Direção de arte única por produto ("a fonte da verdade")
Estender a paleta para um **BrandKit** gerado por IA uma vez por produto e consumido pelos três:
- Já temos: background, accent, onDark, headingFont, bodyFont, displayFont.
- **Adicionar:** `mood` (ex.: "warm, editorial, optimistic"), `imageStyle` (ex.: "documentary
  photography, natural light, real people"), `subjectGuidance` (quem/o quê aparece nas cenas do
  nicho), `motifs` (régua accent, eyebrow em caixa-alta, cards arredondados) e **tokens de escala
  tipográfica** (display/h1/h2/body em proporção).
- Persistir junto ao `ProductPalette` (ou um `products/{slug}/brand.json`). Novo prompt
  `ebook/brand.md` estende o atual `ebook/palette.md`.
- **Toda** geração de imagem (`visual-plan`, `lp-hero`, cover scene) passa a receber
  `imageStyle` + `mood` + `subjectGuidance` no prompt → coerência visual real entre PDF, LP e capa.

### Frente B — Tipografia display coerente
- Hero da LP usa `DisplayFont` no `h1` (carregar a 3ª família no `Fonts()` do `LandingPage.cs`).
- Aberturas de capítulo do PDF avaliam usar display (hoje HeadingFont).
- Tokens de escala (Frente A) garantem a MESMA hierarquia proporcional nos três.

### Frente C — Capa priorizando Gemini com contexto amplo
- Trocar o fundo da capa de `MediaKind.Photo` → priorizar **Gemini** (generativo) com prompt **rico**:
  `scene` + título + promessa + nicho + `mood` + `imageStyle` + `subjectGuidance` + hex da paleta +
  "editorial, premium, no text".
- Recomendação de arquitetura: **fundo Gemini (sem texto) + composição Skia 2.0 por cima** como
  default (seguro, já temos a composição rica) — e o **full-AI com texto** (CoverWithText + QA de
  visão, docs/14) como opção ligável. Assim o Gemini ganha "contexto mais amplo" sem o risco de texto
  embaralhado.

### Frente D — Hero da LP 2.0 (pesquisa de padrões que convertem)
Padrões de heroes de info-produto (Hotmart/Kiwify e gringos) a adotar, em variantes por template:
1. **Split com mockup 3D** — texto à esquerda; **mockup 3D do e-book** (já geramos `RenderMockup`!)
   em frame, à direita, sobre **gradiente-mesh/blobs** da paleta. (Troca o PNG plano pelo 3D.)
2. **Hero com ilustração de fundo** — a `lp-hero` vai para TRÁS do hero, com scrim, e o título
   **display** por cima (espelhando a capa).
3. **Prova social no hero** — pilha de avatares + rating + "X alunos" + logos de mídia acima da dobra.
4. **Camadas/“glow”** — blobs accent desfocados, grid sutil, badges flutuantes (como os exemplos do
   Gamma em docs/cover-examples).
5. **Coerência com a capa** — mesmo eyebrow caps + régua accent do sistema de capas.
Entregar 2–3 variantes (uma por template Aurora/Editorial/Vibrant), todas dirigidas pela Frente A.

### Frente E — Coerência de motivos (polish posterior)
Unificar os "tijolos": eyebrow em caixa-alta + régua accent + card de benefício com check aparecem
**igual** na capa, no PDF e na LP → linguagem visual reconhecível do produto.

## 3. Sequência sugerida
1. **Frente A** (brand kit por IA) — destrava as demais (todas consomem a direção de arte).
2. **Frente C** (Gemini na capa) — ganho imediato e isolado.
3. **Frente B** (display coerente) — barato, alto impacto percebido.
4. **Frente D** (hero 2.0) — maior esforço, maior impacto em conversão.
5. **Frente E** (motivos) — acabamento.

## 4. Riscos & mitigação
- **Gemini sem chave/cota** → fallback para bancos/Skia (cadeia free-first já existe).
- **Brand kit por IA inválido** → validação (cores #RRGGBB, fontes do set) + catálogo determinístico
  (mesmo padrão do `PaletteDirector`, docs/14 WP-2).
- **Hero mais pesado** → vigiar o peso do HTML (capa/hero em data URI já pesam); servir imagens
  grandes via `/media/` (nota já registrada em docs/12).

## 5. Resumo executivo
A **coerência de cor e fonte já existe**. Falta (a) uma **direção de arte única por produto** que
inclua o **contexto das imagens** e os **tokens de tipografia/motivos** — para os três artefatos
beberem da mesma fonte; (b) **priorizar Gemini na capa** com um prompt de contexto amplo; e (c)
**reconstruir o hero da LP** (mockup 3D + ilustração de fundo + prova social + display), hoje o elo
mais fraco. As Frentes A→E entregam isso de forma incremental, cada uma com valor isolado.
