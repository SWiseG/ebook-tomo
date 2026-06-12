# 07 — Documentação Técnica

## 1. Stack e versões

| Camada | Tecnologia | Versão alvo |
|---|---|---|
| Backend | ASP.NET Core (minimal APIs) | .NET 10 LTS |
| ORM | EF Core + SQLite | 10.x |
| Scheduler | Quartz.NET (AdoJobStore SQLite) | 3.x |
| PDF | QuestPDF Community | 2024.x |
| Imagem | SkiaSharp | 2.88+ |
| Vídeo / TTS | FFmpeg (binário) / Piper TTS (pt-BR) | estáveis |
| Browser automation | Microsoft.Playwright | 1.4x |
| Scraping | HtmlAgilityPack + HttpClientFactory | — |
| Logs | Serilog (sink JSON arquivo) | 3.x |
| Frontend | Angular standalone + SCSS | 21 |
| Testes | xUnit + fakes manuais (sem mock framework); Vitest no front | — |

## 2. Padrões de implementação

### 2.1 CQRS-lite sem MediatR
Dispatcher próprio (~100 linhas) com `ICommand<TResult>`/`IQuery<TResult>` e behaviors encadeados (logging → validação → transação). Evita dependência licenciada e mantém pipeline explícito.

### 2.2 Domain Events + Outbox
- Agregado acumula eventos (`AggregateRoot.Raise(...)`); `UnitOfWork.SaveChanges` serializa para `OutboxEvent` **na mesma transação**.
- `OutboxDispatcher` (HostedService): poll curto (1 s com backoff quando ocioso) → `Channel` → handlers via DI.
- Idempotência: handler registra `(EventId, HandlerName)` em `ProcessedEvent`; reentrega é no-op.
- Handler decide: executa inline (rápido) ou enfileira `Job` (lento/IO pesado).

### 2.3 Jobs
- `IdempotencyKey` natural (ex.: `chapter:{productId}:{n}` ) impede duplicatas.
- Retry: 3 tentativas com backoff exponencial + jitter; depois `Dead` + alerta no painel.
- Worker com `SemaphoreSlim` (paralelismo 2–4) e categorias com concorrência 1 (Playwright, Claude CLI).

### 2.4 Resiliência de integrações
- `HttpClientFactory` + Polly: retry (transientes), circuit-breaker por integração, timeout explícito.
- Scraping educado: throttle por host, User-Agent fixo, cache de resposta 24 h.

## 3. AI Gateway — contrato e implementação

```csharp
public interface IAiGateway
{
    Task<AiResponse> CompleteAsync(AiRequest request, CancellationToken ct);
}

public sealed record AiRequest(
    string Purpose,            // "ebook.chapter", "lp.copy", "optimizer.analysis"...
    string PromptTemplate,     // nome em /prompts
    IReadOnlyDictionary<string, string> Variables,
    QualityTier Tier,          // Draft | Commercial | Premium
    int MaxOutputTokensEst,
    Guid? ProductId);

public sealed record AiResponse(
    string Content, AiProviderKind Provider, bool CacheHit, int DurationMs);
```

Cadeia de resolução (cada elo é um `IAiResolver` testável):
`CacheResolver → KnowledgeReuseResolver → TemplateResolver → ClaudeCliResolver → ClaudeApiResolver(off)`

### ClaudeCliProvider (assinatura Pro)
- Executa `claude -p "<prompt>" --output-format json` via `Process` no container (Claude Code CLI instalado na imagem; autenticação via volume `~/.claude` montado, login feito uma única vez no setup).
- **Concorrência 1** + fila com prioridade (`publish > generate > optimize > social`).
- Detecção de janela/limite: saída de erro reconhecida → job adiado com `ScheduledAt` = próxima janela estimada; pipeline retoma sozinho.
- Prompt sempre comprimido: instruções fixas vêm do template em `/prompts` (curtas), contexto variável é o mínimo necessário (ex.: capítulo recebe outline + resumo de 1 parágrafo dos capítulos anteriores, nunca o texto integral).
- Validação de saída: formato esperado (JSON/markdown), tamanho mínimo/máximo; 1 retry com instrução corretiva; falha → job `Failed`.

### Economia de tokens (regras obrigatórias)
1. Tudo que a IA produz é indexado (`KnowledgeAsset`/`AiCache`) — segunda solicitação semelhante não paga.
2. Conteúdo determinístico (descrições curtas, hashtags, variações de post) usa templates com placeholders, não IA.
3. Geração por capítulo (não o livro inteiro) → falha barata, retomável, contexto pequeno.
4. Revisão recebe diff/resumo, não o manuscrito completo, exceto no tier Premium.
5. `AiUsage` alimenta gráfico no painel; teto mensal corta para fila quando atingido.

## 4. Integrações externas

| Integração | Mecanismo | Credenciais | Observações |
|---|---|---|---|
| Kiwify (criação) | Playwright headless Chromium, `storageState` persistido em `/data/secrets` | login/senha (AES em repouso) | Seletores em `KiwifySelectors.cs` único; screenshots de falha salvos p/ debug |
| Kiwify (vendas) | Webhook `POST /webhooks/kiwify` | token de verificação | Payload bruto salvo no FileStore antes de processar |
| Meta (IG/FB) | Graph API v19+ (`/photos`, `/media`, `/media_publish`, reels) | Page Access Token longo + IG Business ID | Requer app review (`pages_manage_posts`, `instagram_content_publish`) — iniciar na Fase 0 |
| X | API v2 `POST /2/tweets` | OAuth 1.0a user context | Free tier ~500 writes/mês — cota controlada em `Setting` |
| Pexels/Unsplash | REST com API key grátis | api key | Cache local de fotos por keyword |
| Google Trends | Endpoint csv/related não-oficial | — | Tratar como instável; fallback nas outras fontes |
| Reddit | `https://www.reddit.com/r/{sub}/top.json` | — | Rate limit respeitado (60 req/10 min) |

## 5. API REST (painel)

Padrões: `/api/v1/*`, JSON camelCase, `ProblemDetails` para erros, JWT Bearer (cookie HttpOnly no SPA), paginação `?page=&size=`.

Principais grupos:
```
POST   /api/v1/auth/login
GET    /api/v1/dashboard/summary
GET    /api/v1/niches?status=        POST /api/v1/niches/{id}/approve|discard
GET    /api/v1/products              GET  /api/v1/products/{id}
POST   /api/v1/products/{id}/advance|retry|regenerate
GET    /api/v1/approvals?status=     POST /api/v1/approvals/{id}/approve|reject
GET    /api/v1/jobs?status=          POST /api/v1/jobs/{id}/retry
GET    /api/v1/analytics/funnel?productId=&from=&to=
GET    /api/v1/optimizer/runs        POST /api/v1/optimizer/decisions/{id}/approve|veto
GET    /api/v1/ai/usage?from=&to=
GET    /api/v1/settings              PUT  /api/v1/settings/{key}
GET    /api/v1/logs?level=&module=&traceId=
-- públicos --
GET    /px.gif?p={slug}&e={visit|click}&utm_...   (pixel)
POST   /webhooks/kiwify
GET    /health/live | /health/ready
```

## 6. Frontend Angular

- Standalone components + lazy routes por feature; signals para estado local, service + `BehaviorSubject` só onde há estado cruzado.
- `ApiClient` tipado gerado a mão (DTOs espelhados); interceptor para auth/erros (toast global).
- SCSS: design tokens em `styles/_tokens.scss` (cores, espaçamento, tipografia); tema escuro padrão.
- Build de produção copiado para `wwwroot` do API no Docker build — **um único deploy**.

## 7. Observabilidade

- Serilog: template JSON compacto; propriedades padrão `Module`, `ProductId`, `JobId`, `TraceId` (via `LogContext`).
- `ActivitySource` por módulo (spans em pipelines longos); exporter OTLP desligado por padrão.
- Health checks com checagens custom: espaço em disco > 1 GB, fila de jobs sem `Dead` recente, último cron executado dentro da tolerância.
- Métricas internas (contadores em SQLite via agregação diária) — sem Prometheus no MVP.

## 8. Estratégia de testes

| Nível | Escopo | Ferramentas |
|---|---|---|
| Unidade (Domain) | máquina de estados do Product, NicheScorer, regras do Optimizer | xUnit puro |
| Unidade (Application) | casos de uso com `FakeAiGateway` determinístico (respostas canned por purpose) | xUnit + NSubstitute |
| Infra | FileStore (escrita atômica), parsers (webhook, trends), AiCache, repositórios (SQLite in-memory) | xUnit |
| Integração | WebApplicationFactory: comando → outbox → handler → job → estado final; webhook Kiwify ponta a ponta | xUnit |
| E2E sintético (prod) | cron semanal valida login Kiwify/Playwright e publica post de teste em conta sandbox | job interno |
| Front | unit de services/guards + smoke de rotas | Jest |

Meta de cobertura: Domain ≥ 90 %, Application ≥ 80 %. **Nenhum teste chama IA ou rede real** (fakes/fixtures gravadas).

## 9. Convenções de código

- C#: `Directory.Build.props` com `Nullable=enable`, `TreatWarningsAsErrors=true`, analyzers (`Microsoft.CodeAnalysis.NetAnalyzers`); estilo via `.editorconfig`.
- Result pattern (`Result<T>` com erro tipado) nos casos de uso; exceções só para bugs/infra.
- Datas sempre UTC (`IClock` injetável); dinheiro como `decimal` + moeda.
- Git: trunk-based, branches `feat/*`, conventional commits, PR com CI verde obrigatório.
- Idioma: código/identificadores em inglês; conteúdo gerado e UI do painel em pt-BR.
