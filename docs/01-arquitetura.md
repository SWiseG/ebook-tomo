# 01 — Arquitetura

## 1. Visão

Uma plataforma autônoma que executa um **ciclo de negócio de 30 dias** sem intervenção humana obrigatória:

```
Descobrir nicho → Pesquisar/Enriquecer → Gerar e-book → PDF + Capa
→ Landing Page → Publicar na Kiwify → Divulgar (IG/FB/X)
→ Medir (vendas, tráfego, conversão) → Otimizar (matar/iterar/escalar)
```

O sistema mantém **≥ 10 produtos ativos**, aprende a cada ciclo (retroalimentação) e opera com **custo marginal próximo de zero** por produto.

## 2. Princípios arquiteturais

1. **Custo primeiro**: 1 VPS, 1 container, zero serviços pagos. Toda integração usa free tier.
2. **Tokens são o recurso mais caro**: nenhuma chamada de IA acontece sem antes consultar cache e base de conhecimento. Tudo que a IA gera é persistido e indexado para reuso.
3. **Filesystem é o banco de conteúdo**: SQLite guarda só estado, índice e métricas agregadas. Conteúdo (capítulos, pesquisas, prompts, posts) é JSON/Markdown em disco — auditável, versionável, com backup trivial.
4. **Autonomia com válvula de segurança**: cada etapa pode operar em modo `Auto` ou `RequireApproval` (fila de aprovação no painel). MVP inicia com aprovação humana nos pontos de publicação externa; metas de longo prazo removem isso.
5. **Falhar barato e isolado**: cada módulo é um conjunto de handlers idempotentes acionados por eventos. Falha em um produto não para o ciclo dos demais.
6. **Clean Architecture pragmática**: 4 projetos .NET, DDD simplificado (entidades ricas onde há regra, anêmicas onde não há), sem over-engineering.

## 3. Estilo arquitetural

**Monolito modular, event-driven in-process**, hospedado num único processo ASP.NET Core:

```
┌──────────────────────────── Container Docker ────────────────────────────┐
│  ASP.NET Core 8 (Kestrel)                                                 │
│  ┌──────────────┐  ┌───────────────────────────────────────────────────┐ │
│  │ REST API     │  │ Hosted Services                                    │ │
│  │ /api/*       │  │  • Quartz Scheduler (crons persistidos)            │ │
│  │ Webhooks     │  │  • Outbox Dispatcher (Channels)                    │ │
│  │ Static SPA   │  │  • Job Worker (fila SQLite, retry/backoff)         │ │
│  └──────────────┘  └───────────────────────────────────────────────────┘ │
│  ┌───────────────────────────────────────────────────────────────────── ┐│
│  │ Módulos (Application): Trend │ Knowledge │ Ebook │ Pdf │ Lp │ Kiwify ││
│  │ │ Social │ Image │ Video │ Analytics │ RoiOptimizer                  ││
│  └──────────────────────────────────────────────────────────────────────┘│
│  ┌──────────────────────────────────────────────────────────────────────┐│
│  │ Infra: EF Core (SQLite WAL) │ FileStore │ AI Gateway │ HttpClients   ││
│  │ Playwright │ QuestPDF │ SkiaSharp │ FFmpeg │ Serilog                 ││
│  └──────────────────────────────────────────────────────────────────────┘│
└───────────────────────────────────────────────────────────────────────────┘
         │ volumes: /data/db  /data/content  /data/artifacts  /data/logs
```

Justificativa: microsserviços/filas externas (RabbitMQ, Redis) adicionariam custo e operação sem benefício no volume esperado (dezenas de produtos, centenas de jobs/dia). O desacoplamento exigido vem de **eventos + interfaces**, não de processos separados. Se escala exigir, módulos já isolados por contrato podem ser extraídos.

## 4. Camadas (Clean Architecture)

| Projeto | Responsabilidade | Depende de |
|---|---|---|
| `Ebook.Domain` | Entidades, Value Objects, Domain Events, enums, interfaces de repositório, regras de negócio puras | nada |
| `Ebook.Application` | Casos de uso (commands/queries), handlers de eventos, orquestração de pipelines, contratos de serviços externos (`IAiGateway`, `IPublisher`...), validação | Domain |
| `Ebook.Infrastructure` | EF Core/SQLite, FileStore JSON, AI Gateway, Quartz, Outbox, integrações (Kiwify, Meta, X, Pexels), PDF, imagem, vídeo | Application, Domain |
| `Ebook.Api` | Endpoints REST (minimal APIs), webhooks, auth, composição (DI), hosted services, serve o SPA Angular | todos |
| `Ebook.Admin` (Angular) | Painel SPA: dashboards, fila de aprovação, produtos, configurações, logs | API REST |

Regra de dependência estrita: nada em Domain/Application referencia pacote de infraestrutura.

## 5. Módulos e contratos

Cada módulo expõe casos de uso e reage a eventos. Entrada/saída sempre por DTOs; integrações externas atrás de interface.

| # | Módulo | Entrada (trigger) | Saída (evento) | Integrações |
|---|---|---|---|---|
| 1 | **Trend Discovery** | Cron 30d `discover-niches` | `NicheDiscovered` (top N pontuados) | Google Trends (csv export), Reddit JSON público, Amazon best-sellers (scrape), autocomplete Google/YouTube |
| 2 | **Knowledge Enrichment** | `NicheSelected` | `KnowledgeReady` | Scraping leve (HtmlAgilityPack), Wikipedia API, Reddit; índice local de conhecimento p/ reuso |
| 3 | **Ebook Generator** | `KnowledgeReady` | `EbookDrafted` | AI Gateway (outline → capítulos → revisão) |
| 4 | **PDF Generator** | `EbookDrafted` | `PdfReady` | QuestPDF Community (temas profissionais) |
| 5 | **Landing Page Generator** | `PdfReady` | `LandingPageReady` | Templates HTML/SCSS + copy via AI Gateway; publica estático (nginx local ou Cloudflare Pages) |
| 6 | **Kiwify Publisher** | `LandingPageReady` (+aprovação) | `ProductPublished` | Playwright (criação de produto via navegador), webhook Kiwify (vendas) |
| 7 | **Social Media Publisher** | `ProductPublished` + cron diário | `PostPublished` | Meta Graph API (IG+FB), X API v2 free tier |
| 8 | **Image Generator** | sob demanda dos módulos 4/5/7 | `ImageReady` | SkiaSharp (composição programática) + Pexels/Unsplash (fotos grátis) |
| 9 | **Video Generator** | cron semanal por produto ativo | `VideoReady` | FFmpeg (slideshow) + Piper TTS (voz local grátis) |
| 10 | **Analytics Engine** | Webhooks + pixel próprio na LP + crons de coleta | `MetricsAggregated` (diário) | Webhook Kiwify, pixel 1x1 próprio, métricas Meta/X |
| 11 | **ROI Optimizer** | Cron 30d `optimize-cycle` | `OptimizationDecided` → realimenta módulos 1, 3, 5, 7 | interno (regras + AI Gateway p/ análise) |

## 6. AI Gateway — estratégia de custo de tokens

Componente central. **Nenhum módulo chama IA diretamente.**

```
Requisição { purpose, inputs, qualityTier, maxTokens }
   │
   ├─ 1. Cache exato (hash SHA-256 de purpose+inputs normalizados) ──→ hit? retorna (custo 0)
   ├─ 2. Knowledge Base (conteúdo semelhante já gerado p/ nicho/tema) → reuso/adaptação local
   ├─ 3. Template determinístico (quando o purpose tem template: posts, descrições) 
   ├─ 4. Claude via assinatura Pro ── `claude -p` (CLI headless, sem custo por token)
   │       • fila com rate-limit/janela de uso, prioridade por valor de negócio
   │       • prompts comprimidos: contexto mínimo, instruções reutilizáveis em arquivos
   └─ 5. Claude API (pay-per-token) ── DESLIGADO por padrão; flag por purpose + teto mensal
   
Toda resposta → persistida no FileStore + indexada (purpose, nicho, hash, tokens) → vira insumo dos passos 1–2 futuros.
```

Regras:
- **Orçamento por pipeline**: cada execução de geração de e-book tem teto de chamadas/tokens definido em `Settings`; estourou → pausa e enfileira para a próxima janela.
- **Tiers de qualidade**: `Draft` (1 passada), `Commercial` (outline + capítulos + 1 revisão), `Premium` (+ revisão de consistência). ROI Optimizer ajusta o tier por nicho conforme conversão.
- **Telemetria**: tabela `AiUsage` registra cada chamada (purpose, tokens estimados, cache hit/miss, duração) — exposta no painel.

## 7. Eventos, jobs e scheduler

- **Domain Events** → gravados na tabela `OutboxEvent` na mesma transação do agregado (consistência garantida).
- **Outbox Dispatcher** (HostedService + `System.Threading.Channels`): lê pendentes, despacha para handlers registrados, marca processado. Entrega *at-least-once*; handlers idempotentes (chave natural ou `ProcessedEvent`).
- **Job Queue** (tabela `Job`): trabalhos longos (gerar capítulo, renderizar vídeo, automação Playwright) viram jobs com `status`, `attempts`, retry exponencial e *dead-letter* visível no painel.
- **Quartz.NET** (store SQLite): crons — `discover-niches` (30d), `optimize-cycle` (30d), `daily-metrics` (1d), `social-calendar` (1d), `weekly-video` (7d), `health-housekeeping` (1d).

## 8. Dados e armazenamento

- **SQLite** (EF Core, WAL, `busy_timeout`): estado, índices, métricas agregadas, fila de jobs, outbox. Um único arquivo `/data/db/ebook.db`.
- **FileStore** (`/data/content`, `/data/artifacts`): JSON/Markdown/PDF/PNG/MP4 organizados por nicho/produto. Caminho + hash indexados no SQLite. Atende o requisito “JSON persistente” e “uso mínimo de banco”.
- **Backup**: Litestream (replicação contínua do SQLite) + `rclone` dos diretórios de conteúdo para storage grátis/barato (detalhe em [08-implantacao.md](08-implantacao.md)).

Detalhes completos em [04-modelo-de-dados.md](04-modelo-de-dados.md).

## 9. Observabilidade

- **Serilog** com sink JSON estruturado (arquivo rotativo em `/data/logs`) + enriquecimento (`TraceId`, `ProductId`, `Module`, `JobId`).
- **Health checks** (`/health/live`, `/health/ready`): DB, disco, fila, último ciclo executado.
- **Painel admin**: visão de pipeline (estado de cada produto por etapa), jobs falhos, consumo de IA, logs filtráveis (lidos do arquivo JSON) — sem stack extra de observabilidade no MVP.
- **OpenTelemetry**: instrumentação preparada (ActivitySource por módulo), exporter desligado por padrão — liga-se um Grafana/Tempo no futuro sem refatorar.

## 10. Segurança

- Painel admin com login único (usuário/senha + JWT curto, cookie HttpOnly). Sem multi-tenant no MVP.
- Webhooks validados por assinatura/token (Kiwify envia token configurável).
- Secrets só via variáveis de ambiente / arquivo `.env` fora do repositório.
- Playwright roda em contexto isolado; credenciais Kiwify/Meta criptografadas em repouso (DPAPI não existe no Linux → AES com chave em env var).

## 11. Decisões técnicas (ADR resumido)

| Decisão | Escolha | Alternativas rejeitadas | Por quê |
|---|---|---|---|
| Topologia | Monolito modular, 1 container | Microsserviços, serverless | Custo, simplicidade operacional, volume baixo |
| Mensageria | Outbox + Channels in-process | RabbitMQ, Redis Streams | Zero infra extra; entrega suficiente |
| Scheduler | Quartz.NET + SQLite store | Hangfire, cron do SO | Persistência de estado, misfire handling, grátis |
| Banco | SQLite WAL + FileStore JSON | Postgres | Requisito; backup/operação triviais |
| PDF | QuestPDF Community | wkhtmltopdf, Playwright print | Licença Community grátis (< US$ 1M receita), API C# tipada, qualidade alta |
| IA | Claude Pro via CLI headless | API direta | Requisito de usar assinatura; API como fallback opcional |
| Imagens | SkiaSharp + Pexels | DALL-E/SD pagos, SD local | VPS sem GPU; composição programática é grátis e consistente |
| Vídeo | FFmpeg + Piper TTS | APIs de vídeo pagas | Custo zero, qualidade aceitável p/ reels |
| Kiwify | Playwright + webhooks | API oficial | Kiwify não expõe API pública de criação de produto; webhooks cobrem vendas |
| LP hosting | nginx no próprio VPS (MVP) → Cloudflare Pages | Vercel/Netlify | Zero custo, CDN grátis na evolução |
| Frontend | Angular 17+ standalone + SCSS | — | Requisito |

## 12. Riscos e mitigações

| Risco | Impacto | Mitigação |
|---|---|---|
| Kiwify mudar o fluxo de criação de produto (quebra Playwright) | Publicação para | Seletores centralizados + teste sintético semanal + alerta no painel + modo manual assistido |
| Limites do X API free tier (~500 posts/mês) | Menos alcance | Calendário respeita cota; prioriza IG/FB (Graph API sem custo) |
| Instagram exige conta Business + app Meta aprovado | Atraso no módulo 7 | Iniciar processo de app review na Fase 0; fallback: publicar só FB até aprovação |
| Janelas de uso da assinatura Claude Pro | Pipeline pausa | Fila com prioridade + retomada automática; tiers de qualidade reduzem chamadas |
| Qualidade de conteúdo IA insuficiente para venda | Reputação/refund | Tier `Commercial` com passada de revisão + gate de aprovação humana opcional + score de qualidade no feedback loop |
| Scraping (Trends/Amazon) instável | Descoberta degrada | Múltiplas fontes independentes; score funciona com subconjunto |
| VPS único (SPOF) | Downtime | Litestream + IaC docker-compose: restore < 30 min em VPS novo |
