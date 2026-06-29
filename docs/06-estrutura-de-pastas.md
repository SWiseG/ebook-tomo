# 06 — Estrutura de Pastas

```
EBOOK/
├── docs/                                # esta documentação
├── prompts/                             # biblioteca de prompts versionada (fora do código)
│   ├── ebook/ (outline.md, chapter.md, review.md, sales-copy.md)
│   ├── knowledge/ (structure-pack.md, summarize.md)
│   ├── social/ (calendar.md, post-*.md)
│   ├── lp/ (copy.md)
│   ├── media/ (lp-hero.md, ...)
│   └── optimizer/ (cycle-analysis.md)
├── src/
│   ├── Ebook.Domain/
│   │   ├── Common/                      # Entity, AggregateRoot, IDomainEvent, Result
│   │   ├── Niches/                      # Niche, TrendSnapshot, NicheScore (VO), eventos
│   │   ├── Knowledge/                   # KnowledgeAsset, KnowledgePackRef
│   │   ├── Products/                    # Product (agregado + máquina de estados), Artifact, eventos
│   │   ├── Sales/                       # SaleEvent
│   │   ├── Social/                      # SocialPost, PostType
│   │   ├── Analytics/                   # MetricDaily
│   │   ├── Optimization/                # OptimizationRun, OptimizationDecision
│   │   └── Abstractions/                # IRepository<T>, IUnitOfWork, IFileStore, IClock
│   │
│   ├── Ebook.Application/
│   │   ├── Common/                      # ICommand/IQuery + dispatcher, behaviors (log, validação), IEventHandler
│   │   │   ├── Events/                  # IDomainEventHandler<T>, outbox helpers
│   │   │   ├── Jobs/                    # IJobQueue, IJobHandler, IdempotencyKey
│   │   │   ├── Messaging/               # IDispatcher, Result<T>
│   │   │   ├── Realtime/                # IRealtimeNotifier
│   │   │   ├── Settings/                # ISettingStore, SettingKeys
│   │   │   └── Text/                    # IMarkdownParser, helpers de texto
│   │   ├── Ai/                          # IAiGateway, AiRequest/Response, IPromptLibrary, orçamento
│   │   ├── Discovery/                   # TrendDiscovery: casos de uso + ITrendSource, NicheScorer
│   │   ├── Knowledge/                   # KnowledgeEnrichment: casos de uso + IContentExtractor
│   │   ├── Content/                     # geração de conteúdo unificada
│   │   │   ├── Images/                  # NicheStyleCatalog, PaletteCatalog, BrandKit, BrandResolver,
│   │   │   │                            #   BrandDirector, NichePalette, ImageComposer, StylePlaybook
│   │   │   ├── Lp/                      # LandingPageBuilder, LpContent, LpTemplateSelector, LpJobHandler
│   │   │   │   └── Lab/                 # LpLab (visualização e testes de LP)
│   │   │   └── Pdf/                     # IPdfRenderer, PdfJobHandler, MarkdownBlockKind, ConversionAudit
│   │   ├── Publishing/                  # IKiwifyAutomation, PublishProduct, HandleSaleWebhook
│   │   ├── Social/                      # ISocialNetwork, BuildCalendar, PublishDuePosts
│   │   ├── Media/                       # IMediaGateway, geração de imagens free-first (E14)
│   │   ├── Video/                       # IVideoComposer, GenerateVideoJobHandler (gated: video.enabled)
│   │   ├── Analytics/                   # TrackPixelHit, AggregateDailyMetrics, queries do dashboard
│   │   ├── Optimization/                # RunOptimizationCycle, ExecuteDecision, OptimizationExecutor
│   │   ├── Administration/              # auth, settings, approvals, jobs/logs queries
│   │   │   ├── Auth/                    # AdminAuth, JwtClaims
│   │   │   ├── Dashboard/               # queries de KPI, funil, IA
│   │   │   ├── Media/                   # telemetria de mídia gerada
│   │   │   ├── Provenance/              # rastreabilidade de artefatos
│   │   │   └── Sources/                 # gestão de fontes de trend
│   │   └── DevTools/                    # utilitários de desenvolvimento (hash-password, echo AI)
│   │
│   ├── Ebook.Infrastructure/
│   │   ├── Persistence/                 # DbContext, migrations, repositórios, UnitOfWork
│   │   │   └── Migrations/
│   │   ├── FileStore/                   # JsonFileStore (escrita atômica, hash)
│   │   ├── Events/                      # OutboxWriter, OutboxDispatcher, ProcessedEventStore
│   │   ├── Jobs/                        # JobQueue, JobWorker, retry policy
│   │   ├── Scheduling/                  # Quartz setup + jobs (DiscoverNichesJob, OptimizeCycleJob...)
│   │   ├── Ai/                          # AiGateway, ClaudeCliProvider, ClaudeApiProvider, AiCache, TemplateProvider
│   │   ├── Discovery/                   # GoogleTrendsSource, RedditSource, AmazonSource, AutocompleteSource,
│   │   │                                #   HtmlAgilityPack extractors, polite throttling
│   │   ├── Content/                     # QuestPdfRenderer, SkiaImageComposer, FontRegistry, IconRegistry,
│   │   │                                #   PexelsPhotoProvider, ProductReader
│   │   ├── Publishing/                  # PlaywrightKiwifyAutomation, seletores centralizados, WebhookParser
│   │   ├── Social/                      # MetaGraphClient, XApiClient
│   │   ├── Media/                       # FfmpegVideoComposer, PiperTts, media gateway providers
│   │   ├── Video/                       # pipeline FFmpeg/Piper (gated por video.enabled)
│   │   ├── Analytics/                   # repositórios e agrupamento de métricas
│   │   ├── Optimization/                # repositórios de ciclo de otimização
│   │   ├── Administration/              # repositórios de admin, aprovações
│   │   ├── Settings/                    # SettingStore persistido (SQLite)
│   │   ├── Observability/               # Serilog config, ActivitySources, health checks
│   │   └── Security/                    # JwtService, AesSecretProtector
│   │
│   ├── Ebook.Api/
│   │   ├── Endpoints/                   # minimal APIs (Endpoints.cs, PublicEndpoints.cs)
│   │   ├── Observability/               # middleware de observabilidade, health endpoints
│   │   ├── OpenApi/                     # Scalar + /openapi/v1.json (só em Development)
│   │   ├── Realtime/                    # notificações em tempo real
│   │   ├── assets/
│   │   │   ├── fonts/                   # 6 fontes profissionais embarcadas (Google variáveis → subset)
│   │   │   └── icons/                   # SVGs de ícones (Phosphor/Lucide subset)
│   │   ├── Properties/                  # launchSettings.json
│   │   ├── Program.cs                   # composição DI + pipeline + SPA fallback
│   │   └── appsettings*.json
│   │
│   └── Ebook.Admin/                     # Angular 21 standalone
│       ├── src/app/
│       │   ├── core/                    # auth (guard, interceptor), api client, layout shell
│       │   ├── shared/                  # componentes UI, pipes, SCSS design tokens
│       │   │   └── ag-grid/             # wrappers AG Grid
│       │   ├── theme/                   # variáveis de tema global
│       │   └── features/
│       │       ├── dashboard/           # KPIs, receita, consumo IA
│       │       ├── products/            # detalhe, manuscrito, artefatos
│       │       ├── niches/              # ranking, aprovação de nicho
│       │       ├── channels/            # canais de distribuição (social, Kiwify)
│       │       ├── sources/             # fontes de trend configuráveis
│       │       ├── optimizer/           # decisões de ciclo, relatórios
│       │       ├── jobs/                # fila, dead-letter, retry manual
│       │       ├── logs/                # visualizador de logs
│       │       ├── settings/            # configurações, cotas, modos
│       │       ├── lp-lab/              # laboratório de LPs (preview + comparação)
│       │       ├── media-telemetry/     # telemetria de imagens geradas
│       │       ├── login/               # tela de autenticação
│       │       ├── shell/               # layout raiz, navegação
│       │       └── tutorial/            # guia de primeiro uso
│       └── src/styles/                  # SCSS global (tokens, temas)
│
├── tests/
│   ├── Ebook.Domain.Tests/              # regras de negócio puras (xUnit)
│   ├── Ebook.Application.Tests/         # casos de uso com fakes (AI Gateway fake determinístico)
│   ├── Ebook.Infrastructure.Tests/      # SQLite in-memory, FileStore temp, parsers
│   └── Ebook.IntegrationTests/          # WebApplicationFactory: API + outbox + jobs ponta a ponta
│
├── deploy/
│   ├── docker-compose.yml               # api + nginx + litestream
│   ├── nginx/ (nginx.conf, lp.conf)     # SPA + /lp/* estático + TLS
│   ├── litestream.yml
│   └── .env.example
│
├── .github/workflows/
│   ├── ci.yml                           # build, test, lint (back+front)
│   └── deploy.yml                       # imagem → GHCR → SSH deploy
│
├── Ebook.slnx
├── Directory.Build.props                # nullable, analyzers, versão .NET 10
├── .editorconfig
├── CLAUDE.md                            # guia do repo para agentes
└── README.md
```

## Convenções

- **1 pasta por módulo** em Application e Infrastructure com o mesmo nome — navegação espelhada.
- Casos de uso nomeados como verbo (`GenerateOutline`, `PublishDuePosts`); 1 arquivo = command + handler + validação.
- Interfaces de integração declaradas em **Application**, implementadas em **Infrastructure** (regra de dependência).
- Prompts **nunca** hardcoded em C# — sempre em `/prompts`, carregados pela `PromptLibrary` (hot-reload em dev).
- Templates visuais (PDF, LP, cards) em Infrastructure junto ao seu renderer.
- Conteúdo gerado (`Content/`) em Application abrange PDF, LP e Imagens sob uma única pasta — espelhado em `Infrastructure/Content/`.
- Volumes de runtime (`/data/*`) ficam fora do repositório.
