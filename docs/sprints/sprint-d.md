# Sprint D — Superfícies premium (web reader, audiobook, áudio-social)

> **Meta da sprint:** transformar o e-book de "arquivo para baixar" em **experiências**. O maior "wow" percebido:
> um **leitor web / flipbook** com analytics e captura de lead, e um **audiobook** (reusando o TTS que já temos).
> São as superfícies que dão visibilidade de marca e abrem novos canais.
>
> Itens: **D1** leitor web / flipbook HTML5 · **D2** audiobook · **D3** personalização por origem (se não feita na C4).
> Pré-requisito: Sprint A (o XHTML do EPUB é a base do leitor web).

---

## D1 — Leitor web / flipbook HTML5  ·  esforço: **high**  ·  impacto: **alto**

### Objetivo
Servir o e-book como página web navegável (page-turn ou scroll com progresso), em `/read/{slug}`, com analytics de
leitura e **captura de lead** (e-mail por capítulo/amostra grátis) — um canal sustentável ao lado de LP e social.

### Base de pesquisa
- ZenFlip/Publitas 2026: flipbooks interativos batem PDF estático em tempo-na-página e lead gen; **+25% open rate
  mobile**; 81% dos profissionais dizem que conteúdo interativo prende mais atenção.

### Biblioteca / abordagem
- **Reusar o XHTML do EPUB (Sprint A2)** como fonte do conteúdo — não regerar nada.
- Efeito de virar página: **StPageFlip** (MIT, JS) para desktop; em mobile, **scroll vertical com barra de progresso**
  (melhor UX touch). Decidir por *progressive enhancement*: scroll por padrão, page-flip onde couber.
- Servido como **estático** (mesma estratégia da LP, Cloudflare/Nginx) — sem novo backend de runtime.

### Encaixe arquitetural
- Novo job `ContentJobs.WebReader = "ebook.webreader"` + handler que monta o bundle HTML do leitor a partir do
  conteúdo do EPUB + capa + paleta. `ArtifactType` novo (`WebReader`) **ou** reusar `LpBundle`-like → **migration** se novo tipo.
- Captura de lead: reaproveitar o pixel/analytics; o submit de e-mail pode postar no mesmo endpoint de analytics ou
  num endpoint novo (decidir conforme LGPD/consentimento — ver docs/legais).
- Amostra grátis vs conteúdo completo: gate por capítulo (ex.: 1º capítulo livre → CTA de compra) — alinhado ao funil.

### Passos
1. Decidir `ArtifactType.WebReader` (migration) vs reuso; definir `ContentPaths` e rota `/read/{slug}`.
2. Montar o template do leitor (scroll + progresso; StPageFlip opcional) consumindo o XHTML do EPUB.
3. Instrumentar analytics de leitura (capítulos vistos, % concluído) e captura de lead com consentimento.
4. CTA de compra ao fim da amostra, apontando para a LP/checkout.

### Testes
- Geração do bundle a partir de um EPUB fixture → HTML não-vazio com N capítulos e a capa.
- Sem rede; analytics e lead testados com fakes.

### Riscos
- LGPD na captura de e-mail (consentimento + política) — checar `LegalPrivacyUrl`/Settings legais que já existem.
- Peso de imagens: servir via `/media/` (como já fazemos na LP), nunca base64 inline pesado (lição do docs/17 P2-9).

### DoD
- Cada produto tem um leitor web em `/read/{slug}` com amostra grátis, analytics e captura de lead → novo topo de funil.

---

## D2 — Audiobook  ·  esforço: **medium**  ·  impacto: **médio**

### Objetivo
Gerar um **audiobook MP3** do e-book (capítulo a capítulo + faixa única), reusando o TTS já integrado. Top players
2026 (Inkfluence) oferecem audiobook como diferencial; nós já temos a infra de voz.

### Base de pesquisa
- Inkfluence 2026 lista geração de audiobook entre os recursos "completos".
- Já temos `ITtsEngine` (Piper) e `IVideoComposer`/FFmpeg no `GenerateVideoJobHandler` — voz e mux de áudio resolvidos.

### Biblioteca / abordagem
- **Piper TTS** (já no projeto) para narração por capítulo; **FFmpeg** (já no projeto) para concatenar/normalizar e
  exportar MP3 com capítulos (metadados/marcadores opcionais).
- Texto-fonte: o manuscrito já no FileStore (limpar marcadores visuais/markdown antes de narrar).

### Encaixe arquitetural
- `ContentJobs.Audiobook = "ebook.audiobook"` + `AudiobookJobHandler`, `IdempotencyKey` `audiobook:{productId}`. Re-entrante.
- `ArtifactType.Audiobook` → **migration** + rebuild. `ContentPaths.Audiobook(slug)`.
- Reusar `ITtsEngine.SynthesizeAsync` por capítulo + `IVideoComposer`/uma abstração de áudio para concatenar (ou um
  `IAudioComposer` fino sobre FFmpeg se a interface de vídeo não servir bem). Indexar e expor no painel.

### Passos
1. `ArtifactType.Audiobook` + migration + rebuild.
2. Limpeza do manuscrito → texto narrável (remover sintaxe de blocos/imagens).
3. Narrar por capítulo (Piper) → concatenar/normalizar (FFmpeg) → MP3 com capítulos.
4. `AudiobookJobHandler` no pipeline (opcional/gated por Settings — custo de CPU).

### Testes
- Pipeline com fakes de TTS/compositor → MP3 não-vazio, nº de capítulos correto. Sem invocar Piper/FFmpeg reais em teste.

### Riscos
- Tempo de CPU/duração: gerar sob demanda ou em horário ocioso; gating por Settings (`audiobook.enabled`).
- Qualidade de voz pt-BR do Piper — avaliar voz/modelo; é o ponto que pode exigir ajuste.

### DoD
- Produtos selecionados ganham audiobook MP3 indexado e baixável — novo formato de entrega e de marketing.

---

## D3 — Personalização por origem (caso não feita na Sprint C)
Ver **C4** em [sprint-c.md](sprint-c.md). Mantida aqui como dependência cruzada caso a Sprint C seja adiada.

---

## Ordem sugerida e fechamento
```
D1 leitor web/flipbook (reusa EPUB da Sprint A)  →  D2 audiobook (reusa TTS/FFmpeg)
```
**Ao final da Sprint D:** um produto = PDF + EPUB + DOCX + **leitor web** + **audiobook**. De "um arquivo" para um
**ecossistema de superfícies**, cada uma um canal de aquisição. Esse é o salto que dá visibilidade.

## Fontes
- [Interactive Flipbooks vs Static PDFs — ZenFlip](https://zenflip.io/en/blog/interactive-flipbooks-vs-static-pdfs)
- [How Flipbooks Drive Sales & Engagement 2026 — Publitas](https://www.publitas.com/blog/how-flipbooks-can-drive-sales-engagement/)
- [StPageFlip (page-turn JS, MIT)](https://github.com/Nodlik/StPageFlip)
- [Best AI Ebook Generators 2026 — Inkfluence](https://www.inkfluenceai.com/blog/best-ai-ebook-generators-2026) (audiobook como recurso completo)
