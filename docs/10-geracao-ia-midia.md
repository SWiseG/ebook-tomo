# 10 — Geração de Mídia com IA (E14/E15)

> Arquitetura do Media Gateway e do loop de aprendizado de estilo. Documento normativo — toda decisão de implementação deve respeitar estas convenções.

## Motivação

A geração de imagens e vídeos sempre foi local (SkiaSharp + FFmpeg). Isso garante disponibilidade e custo zero, mas a qualidade visual é limitada. IAs generativas (Gemini, Cloudflare, HuggingFace, Pollinations) produzem resultados muito superiores nos seus free tiers. O objetivo é:

1. **Usar a IA gratuita primeiro** — aproveitar ao máximo os free tiers antes de cair no local.
2. **Nunca quebrar** — o Skia local é o piso; a cadeia sempre entrega algo.
3. **Aprender com o que funciona** — o Claude Pro analisa a mídia gerada e ensina o sistema, tanto os prompts dos provedores externos quanto os presets do Skia.

---

## Arquitetura: Media Gateway (E14)

### Padrão

Espelha exatamente o `IAiGateway` de texto (ver `src/Ebook.Infrastructure/Ai/AiGateway.cs`):

- **`IMediaGateway`** — único ponto de acesso a geração de imagens e frames de vídeo (Application layer).
- **`IMediaResolver`** — cada provedor é um elo da cadeia; retorna `null` quando não consegue atender (cota esgotada, erro transitório), `Result<byte[]>` de falha para abortar, ou `Result<byte[]>` de sucesso.
- **`MediaUsage`** — tabela SQLite registrando cada geração (provedor, tipo, bytes, duração, data); usada para checar a cota antes de tentar o provedor.
- **Cache content-addressable** — toda imagem gerada é gravada em disco com chave `SHA256(brief + provedor)`; cache hit = custo zero, sem chamar o provedor.

### Cadeia de resolvers (ordem)

```
cache
  └─ 1. GeminiImageResolver     (Gemini/Imagen, free tier AI Studio)
  └─ 2. CloudflareImageResolver  (Flux/SDXL, cota diária grátis)
  └─ 3. HuggingFaceImageResolver (SDXL/Flux, free rate-limit)
  └─ 4. PollinationsResolver     (sem chave, custo zero)
  └─ 5. LocalSkiaResolver        (IImageComposer atual — nunca falha)
```

Cada resolver checa sua cota na tabela `MediaUsage` **antes** de chamar a API. Ao atingir o limite (configurável em `appsettings.json` por provedor, ou em Settings), passa para o próximo.

### Interfaces (Application layer)

```csharp
// Application/Media/MediaContracts.cs
public interface IMediaGateway
{
    Task<Result<byte[]>> GenerateImageAsync(MediaBrief brief, CancellationToken ct = default);
}

public interface IMediaResolver
{
    string ProviderName { get; }
    Task<Result<byte[]>?> TryResolveAsync(MediaBrief brief, CancellationToken ct);
}

public sealed record MediaBrief(
    string Template,      // "cover" | "card" | "carousel-slide" | "video-frame"
    string Prompt,        // prompt em linguagem natural, enriquecido pelo nicho
    string NicheSlug,
    NichePalette Palette,
    int Width, int Height);
```

### Briefs de imagem (`/prompts/media/`)

Um brief é um prompt em `/prompts/media/{template}.md` com placeholders `{{var}}`, idêntico ao padrão da `IPromptLibrary` de texto. Exemplo:

```
/prompts/media/cover.md
/prompts/media/card.md
/prompts/media/carousel-slide.md
/prompts/media/video-frame.md
```

O `IPromptLibrary` existente já carrega e interpola esses arquivos — basta criar os templates.

### Integração nos pipelines existentes

Hoje: `CoverJobHandler` chama `IImageComposer.RenderCover(...)` diretamente.
Depois: chama `IMediaGateway.GenerateImageAsync(brief)` → a cadeia tenta os provedores → se todos falharem, o `LocalSkiaResolver` chama o `IImageComposer`.

A interface `IImageComposer` **não muda** — ela vira o último resolver. Toda a Application layer permanece estável.

### Telemetria

A tela de "Consumo IA" no painel ganha uma seção "Mídia" ao lado de "Texto":

| Coluna | Descrição |
|---|---|
| Provedor | Gemini / Cloudflare / HuggingFace / Pollinations / Local |
| Geradas hoje | contagem do dia |
| Cota diária | configurável por provedor |
| Cota mensal restante | calculada em tempo real |
| Cache hits | imagens servidas do cache (custo zero) |

---

## Arquitetura: Loop de Aprendizado (E15)

### Visão geral

```
mídia gerada por IA (E14)  +  métricas de conversão (E11/E12)
    │
    ▼ job semanal "style.learn"
Claude Pro (vision) analisa amostras:
  - composição, paleta, tipografia
  - gancho visual (headline, posição, contraste)
  - layout (hierarquia, espaço negativo)
  - porquê provavelmente converte (associação com métricas)
    │
    ▼ KnowledgeAsset(MediaStyle) por nicho  ──►  gravado na KB existente
    │
    ├─► Realimentação A: injeta aprendizados nos briefs de /prompts/media/
    │   (próxima geração do provedor generativo usa estilo refinado)
    │
    └─► Realimentação B: atualiza presets de paleta/layout/tipografia do Skia
        (o fallback local também melhora com o tempo)
```

### Job `style.learn`

- **Cron:** semanal (segunda-feira 03:00 UTC, configurável).
- **Seleção de amostras:** últimas N imagens geradas por nicho (N configurável, padrão 10), filtradas por `MediaUsage`. Se houver dados de `MetricDaily`, prioriza as associadas a produtos com maior conversão.
- **Chamada de IA:** `IAiGateway.CompleteAsync` com `purpose = "style.analyze"`, enviando as URLs das imagens (via `PublicMediaBaseUrl`). O Claude Pro descreve os padrões visuais em JSON estruturado.
- **Gravação:** `KnowledgeAsset.Create(nicheId, KnowledgeAssetType.MediaStyle, ...)` — mesmo repositório/padrão de reuso que o `KnowledgePack`.
- **Realimentação A:** o resultado é injetado no próximo render de `/prompts/media/*.md` via `IPromptLibrary` (variável `{{styleGuide}}`).
- **Realimentação B:** o resultado gera um `NicheStylePreset.json` (paleta/tipografia refinada) que o `LocalSkiaResolver` usa como override do `PaletteCatalog` padrão.

### `KnowledgeAssetType` (adição ao enum existente)

```csharp
// src/Ebook.Domain/Knowledge/KnowledgeAsset.cs
public enum KnowledgeAssetType
{
    RawResearch,
    KnowledgePack,
    Summary,
    MediaStyle,        // playbook de estilo visual por nicho (E15)
    MarketingFramework // frameworks de persuasão curados (E16)
}
```

---

## Provedores — detalhes técnicos

### Gemini / Imagen (E14-02)

- **API:** Google AI Studio (Gemini 2.0 Flash para texto, Imagen 3 para imagem).
- **Free tier (2026):** 15 req/min, 1500 req/dia (text); Imagen ~100 imagens/dia no free.
- **Autenticação:** `GOOGLE_AI_API_KEY` (env var Railway).
- **Endpoint imagem:** `POST https://generativelanguage.googleapis.com/v1beta/models/imagen-3.0-generate-002:predict`.
- **Resposta:** base64 PNG.
- **Cota no código:** checar `MediaUsage` onde `Provider = "Gemini"` e `Date = today`.

### Cloudflare Workers AI (E14-03)

- **API:** `POST https://api.cloudflare.com/client/v4/accounts/{account_id}/ai/run/@cf/black-forest-labs/flux-1-schnell`.
- **Free tier:** 10.000 neurônios/dia (≈ 100–500 imagens/dia dependendo do modelo).
- **Autenticação:** `CF_ACCOUNT_ID` + `CF_API_TOKEN` (env vars).
- **Resposta:** PNG binário direto.

### HuggingFace Inference (E14-04)

- **API:** `POST https://api-inference.huggingface.co/models/black-forest-labs/FLUX.1-schnell`.
- **Free tier:** rate-limited; sem cota diária fixa — o servidor retorna 503 ao exceder.
- **Autenticação:** `HF_API_TOKEN` (env var).
- **Resposta:** PNG binário ou JSON com base64.
- **Tratamento:** 503 → `null` (próximo resolver).

### Pollinations (E14-05)

- **API:** `GET https://image.pollinations.ai/prompt/{encoded-prompt}?width=1080&height=1080&nologo=true`.
- **Free tier:** sem chave, sem cota declarada — mas sujeito a rate-limit e instabilidade.
- **Resposta:** PNG binário no body do GET.
- **Uso:** último antes do local; custo zero.

### Local Skia (E14-06)

- Embrulha `IImageComposer.RenderCover/RenderSocial/RenderCarousel/RenderMockup`.
- Nunca retorna `null` — sempre entrega.
- Provedor de fallback garantido.

---

## Vídeo generativo (E14-10 — futuro)

A espinha dorsal de vídeo (Piper TTS + FFmpeg) não muda. Quando houver provedores de vídeo gratuitos com API estável (ex.: Veo via AI Studio, RunwayML free), um `IVideoFrameResolver` pode substituir os cards Skia por frames gerados por IA, mantendo o FFmpeg para montar o MP4.

---

## Configuração (`appsettings.json`)

```json
"Media": {
  "Providers": {
    "Gemini": { "ApiKey": "", "DailyLimit": 100, "Enabled": true },
    "Cloudflare": { "AccountId": "", "ApiToken": "", "DailyLimit": 200, "Enabled": true },
    "HuggingFace": { "ApiToken": "", "Enabled": true },
    "Pollinations": { "Enabled": true }
  },
  "StyleLearnCron": "0 3 * * 1",
  "StyleLearnSampleSize": 10
}
```

Todas as chaves devem vir de env vars Railway em produção (nunca hardcoded). Em dev local, ficam em `appsettings.Development.json` (gitignored).
