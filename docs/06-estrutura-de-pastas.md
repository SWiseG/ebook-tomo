# 06 — Estrutura de Pastas

```
EBOOK/
├── docs/                                # esta documentação
├── prompts/                             # biblioteca de prompts versionada (fora do código)
│   ├── ebook/ (outline.md, chapter.md, review.md, sales-copy.md)
│   ├── knowledge/ (structure-pack.md, summarize.md)
│   ├── social/ (calendar.md, post-*.md)
│   ├── lp/ (copy.md)
│   └── optimizer/ (cycle-analysis.md)
├── src/
│   ├── Ebook.Domain/
│   │   ├── Common/                      # Entity, AggregateRoot, IDomainEvent, Result
│   │   ├── Niches/                      # Niche, TrendSnapshot, NicheScore (VO), eventos
│   │   ├── Knowledge/                   # KnowledgeAsset, KnowledgePackRef
│   │   ├── Products/                    # Product (agregado + máquina de estados), Artifact, eventos
│   │   ├── Publishing/                  # SaleEvent, ApprovalRequest
│   │   ├── Social/                      # SocialPost, PostType
│   │   ├── Analytics/                   # MetricDaily
│   │   ├── Optimization/                # OptimizationRun, OptimizationDecision
│   │   └── Abstractions/                # IRepository<T>, IUnitOfWork, IFileStore, IClock
│   │
│   ├── Ebook.Application/
│   │   ├── Common/                      # ICommand/IQuery + dispatcher, behaviors (log, validação), IEventHandler
│   │   ├── Ai/                          # IAiGateway, AiRequest/Response, PromptLibrary, orçamento
│   │   ├── TrendDiscovery/              # casos de uso + ITrendSource, NicheScorer
│   │   ├── KnowledgeEnrichment/         # casos de uso + IContentExtractor, índice de reuso
│   │   ├── EbookGeneration/             # GenerateOutline, GenerateChapter, ReviewManuscript...
│   │   ├── PdfGeneration/               # IPdfRenderer, RenderEbookPdf
│   │   ├── LandingPages/                # ILpPublisher, GenerateLp, PublishLp
│   │   ├── KiwifyPublishing/            # IKiwifyAutomation, PublishProduct, HandleSaleWebhook
│   │   ├── SocialPublishing/            # ISocialNetwork, BuildCalendar, PublishDuePosts
│   │   ├── MediaGeneration/             # IImageComposer, IVideoComposer, IStockPhotoProvider, ITts
│   │   ├── Analytics/                   # TrackPixelHit, AggregateDailyMetrics, queries do dashboard
│   │   ├── Optimization/                # RunOptimizationCycle, ExecuteDecision
│   │   └── Administration/              # auth, settings, approvals, jobs/logs queries
│   │
│   ├── Ebook.Infrastructure/
│   │   ├── Persistence/                 # DbContext, migrations, repositórios, UnitOfWork
│   │   ├── FileStore/                   # JsonFileStore (escrita atômica, hash)
│   │   ├── Events/                      # OutboxWriter, OutboxDispatcher, ProcessedEventStore
│   │   ├── Jobs/                        # JobQueue, JobWorker, retry policy
│   │   ├── Scheduling/                  # Quartz setup + jobs (DiscoverNichesJob, OptimizeCycleJob...)
│   │   ├── Ai/                          # AiGateway, ClaudeCliProvider, ClaudeApiProvider, AiCache, TemplateProvider
│   │   ├── TrendSources/                # GoogleTrendsSource, RedditSource, AmazonSource, AutocompleteSource
│   │   ├── Scraping/                    # HtmlAgilityPack extractors, polite throttling
│   │   ├── Pdf/                         # QuestPDF: temas (ClassicTheme, ModernTheme, BoldTheme)
│   │   ├── LandingPages/                # templates HTML/SCSS, NginxLpPublisher
│   │   ├── Kiwify/                      # PlaywrightKiwifyAutomation, seletores centralizados, WebhookParser
│   │   ├── Social/                      # MetaGraphClient, XApiClient
│   │   ├── Media/                       # SkiaImageComposer + templates, FfmpegVideoComposer, PiperTts, PexelsClient
│   │   ├── Observability/               # Serilog config, ActivitySources, health checks
│   │   └── Security/                    # JwtService, AesSecretProtector
│   │
│   ├── Ebook.Api/
│   │   ├── Endpoints/                   # minimal APIs por área (Products, Niches, Approvals, Settings,
│   │   │                                #   Dashboard, Jobs, Logs, Auth, DevTools)
│   │   ├── Webhooks/                    # KiwifyWebhookEndpoint, PixelEndpoint (/px.gif)
│   │   ├── HostedServices/              # registro do dispatcher, worker, Quartz
│   │   ├── Middleware/                  # exception handling, request logging
│   │   ├── Program.cs                   # composição DI + pipeline + SPA fallback
│   │   └── appsettings*.json
│   │
│   └── Ebook.Admin/                     # Angular 21 standalone
│       ├── src/app/
│       │   ├── core/                    # auth (guard, interceptor), api client, layout shell
│       │   ├── shared/                  # componentes UI, pipes, SCSS design tokens
│       │   └── features/
│       │       ├── dashboard/           # KPIs, receita, consumo IA
│       │       ├── pipeline/            # produtos × etapas, ações
│       │       ├── products/            # detalhe, manuscrito, artefatos
│       │       ├── niches/              # ranking, aprovação de nicho
│       │       ├── approvals/           # fila de aprovações
│       │       ├── social/              # calendário, posts
│       │       ├── analytics/           # funil, ROI por produto
│       │       ├── optimizer/           # decisões de ciclo, relatórios
│       │       ├── jobs/                # fila, dead-letter, retry manual
│       │       ├── logs/                # visualizador de logs
│       │       └── settings/            # configurações, cotas, modos
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
├── Ebook.sln
├── Directory.Build.props                # nullable, analyzers, versão .NET
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
- Volumes de runtime (`/data/*`) ficam fora do repositório.
