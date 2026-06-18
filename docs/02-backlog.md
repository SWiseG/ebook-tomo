# 02 — Backlog

Prioridades: **P0** = MVP obrigatório · **P1** = pós-MVP imediato · **P2** = evolução.
Estimativas em pontos (1 ≈ meio dia útil).

## E00 — Fundação da plataforma (P0)

| ID | História | Pts |
|---|---|---|
| E00-01 | Como dev, quero a solution .NET com 4 projetos Clean Architecture + projetos de teste, para garantir a regra de dependência desde o início | 2 |
| E00-02 | Como dev, quero EF Core + SQLite (WAL) com migrations e repositórios genéricos, para persistir estado | 3 |
| E00-03 | Como dev, quero o FileStore (conteúdo JSON/MD indexado por hash) com API de leitura/gravação atômica | 3 |
| E00-04 | Como dev, quero Outbox + dispatcher de eventos in-process com handlers idempotentes | 5 |
| E00-05 | Como dev, quero fila de jobs persistida (retry exponencial, dead-letter) e worker em HostedService | 5 |
| E00-06 | Como dev, quero Quartz.NET com crons persistidos e painel de próximo disparo | 3 |
| E00-07 | Como operador, quero logs estruturados (Serilog JSON) com TraceId/ProductId e health checks | 2 |
| E00-08 | Como dev, quero Dockerfile multi-stage + docker-compose (api, nginx, litestream) rodando local | 3 |
| E00-09 | Como dev, quero CI no GitHub Actions (build, test, lint, imagem → GHCR) e CD via SSH | 3 |
| E00-10 | Como operador, quero autenticação no painel (login único, JWT) | 2 |
| E00-11 | Como dev, quero o esqueleto Angular (standalone, SCSS, layout, auth guard, interceptor) servido pelo Kestrel | 3 |

## E01 — AI Gateway (P0)

| ID | História | Pts |
|---|---|---|
| E01-01 | Como sistema, quero `IAiGateway` com pipeline cache → knowledge → template → Claude CLI, para minimizar tokens | 5 |
| E01-02 | Como sistema, quero provider Claude Pro via `claude -p` (headless) com fila, rate-limit e retomada pós-janela | 5 |
| E01-03 | Como sistema, quero cache content-addressable de respostas (hash de purpose+inputs) | 2 |
| E01-04 | Como operador, quero telemetria `AiUsage` (tokens, cache hit, duração) visível no painel | 2 |
| E01-05 | Como sistema, quero orçamento de chamadas por pipeline e teto mensal configurável | 2 |
| E01-06 | Como sistema, quero biblioteca de prompts versionada em arquivos (`/prompts`), com variáveis e tiers de qualidade | 3 |
| E01-07 | Como sistema, quero provider Claude API como fallback opcional (flag por purpose, desligado por padrão) | 2 |

## E02 — Trend Discovery Engine (P0)

| ID | História | Pts |
|---|---|---|
| E02-01 | Como sistema, quero coletar tendências do Google Trends (export csv/related queries) por categoria | 3 |
| E02-02 | Como sistema, quero coletar subreddits/threads em alta (Reddit JSON público) | 2 |
| E02-03 | Como sistema, quero coletar best-sellers de e-books Amazon por categoria (scrape leve) | 3 |
| E02-04 | Como sistema, quero coletar autocomplete Google/YouTube para long-tails | 2 |
| E02-05 | Como sistema, quero motor de score (volume, concorrência, monetizável, afinidade com histórico) e ranking de nichos | 5 |
| E02-06 | Como sistema, quero cron de 30 dias que gera snapshot, pontua e emite `NicheDiscovered` para os top N | 2 |
| E02-07 | Como operador, quero ver/editar nichos no painel (aprovar, descartar, forçar) | 3 |
| E02-08 | Como sistema, quero que o score aprenda com o feedback do ROI Optimizer (pesos ajustáveis) | 3 (P1) |

## E03 — Knowledge Enrichment Engine (P0)

| ID | História | Pts |
|---|---|---|
| E03-01 | Como sistema, quero pesquisar e extrair conteúdo público do nicho (Wikipedia, blogs, Reddit) em JSON estruturado | 5 |
| E03-02 | Como sistema, quero índice de conhecimento por tema/nicho (keywords + metadados) para reuso entre produtos | 3 |
| E03-03 | Como sistema, quero sumarização/estruturação do material bruto via AI Gateway em `KnowledgePack` (fatos, dores, objeções, vocabulário do público) | 3 |
| E03-04 | Como sistema, quero detectar sobreposição com conhecimento existente e reaproveitar sem nova chamada de IA | 3 |

## E04 — Ebook Generator (P0)

| ID | História | Pts |
|---|---|---|
| E04-01 | Como sistema, quero gerar outline (título, subtítulo, capítulos, promessa) a partir do KnowledgePack | 3 |
| E04-02 | Como sistema, quero gerar capítulos um a um (job por capítulo, retomável), com contexto mínimo | 5 |
| E04-03 | Como sistema, quero passada de revisão (coerência, tom, CTA finais) conforme tier de qualidade | 3 |
| E04-04 | Como sistema, quero metadados comerciais (descrição de venda, bullets de benefícios, preço sugerido, bônus) | 2 |
| E04-05 | Como operador, quero ler/editar o manuscrito no painel antes de aprovar (modo RequireApproval) | 3 |
| E04-06 | Como sistema, quero score de qualidade automático (estrutura, tamanho, repetição) que bloqueia manuscrito ruim | 3 (P1) |

## E05 — PDF Generator (P0)

| ID | História | Pts |
|---|---|---|
| E05-01 | Como sistema, quero 3 temas QuestPDF profissionais (capa, sumário, tipografia, headers/footers, página de CTA) | 5 |
| E05-02 | Como sistema, quero renderizar o manuscrito em PDF com seleção de tema por nicho | 3 |
| E05-03 | Como sistema, quero capa gerada (Image Generator) embutida + mockup 3D do e-book para marketing | 2 |

## E06 — Landing Page Generator (P0)

| ID | História | Pts |
|---|---|---|
| E06-01 | Como sistema, quero 2 templates de LP de alta conversão (HTML/SCSS estáticos: herói, dores, conteúdo, prova, oferta, FAQ, CTA) | 5 |
| E06-02 | Como sistema, quero preencher o template com copy do AI Gateway + imagens do produto | 3 |
| E06-03 | Como sistema, quero publicar a LP no nginx do VPS sob `lp.dominio/slug` com pixel de analytics próprio | 3 |
| E06-04 | Como sistema, quero variantes A/B de headline/oferta controladas pelo ROI Optimizer | 5 (P1) |
| E06-05 | Como sistema, quero deploy alternativo via Cloudflare Pages (CDN grátis) | 3 (P2) |

## E07 — Kiwify Publisher (P0)

| ID | História | Pts |
|---|---|---|
| E07-01 | Como sistema, quero automação Playwright que cria produto na Kiwify (nome, preço, arquivo, checkout) com sessão persistida | 8 |
| E07-02 | Como sistema, quero receber webhooks de venda/refund da Kiwify com validação de token e gravar `SaleEvent` | 3 |
| E07-03 | Como operador, quero gate de aprovação antes de publicar (modo RequireApproval) com preview completo | 2 |
| E07-04 | Como sistema, quero teste sintético semanal do fluxo Playwright com alerta em caso de quebra de layout | 3 (P1) |
| E07-05 | Como sistema, quero despublicar/arquivar produto na Kiwify quando o ROI Optimizer decidir matar | 3 (P1) |

## E08 — Social Media Publisher (P0 — IG/FB; X em P1)

| ID | História | Pts |
|---|---|---|
| E08-01 | Como sistema, quero integração Meta Graph API (página FB + IG Business) para publicar imagem+texto | 5 |
| E08-02 | Como sistema, quero calendário de conteúdo por produto (lançamento, prova social, dor/solução, oferta) gerado via templates + AI Gateway | 5 |
| E08-03 | Como sistema, quero agendamento diário (cron) que publica a fila do calendário com UTM por post | 3 |
| E08-04 | Como sistema, quero integração X API v2 free tier respeitando cota mensal | 3 (P1) |
| E08-05 | Como sistema, quero coletar métricas dos posts (alcance, cliques) para o Analytics | 3 (P1) |

## E09 — Image Generator (P0)

| ID | História | Pts |
|---|---|---|
| E09-01 | Como sistema, quero composição programática SkiaSharp (templates: capa de e-book, card social 1080×1080, story 1080×1920) | 5 |
| E09-02 | Como sistema, quero buscar fotos de fundo no Pexels/Unsplash por keywords do nicho (com cache local) | 2 |
| E09-03 | Como sistema, quero paleta/tipografia por nicho (config JSON) para identidade consistente | 2 |

## E10 — Video Generator (P1)

| ID | História | Pts |
|---|---|---|
| E10-01 | Como sistema, quero roteiro curto (30–45 s) gerado via AI Gateway a partir do calendário | 2 |
| E10-02 | Como sistema, quero TTS local (Piper, voz pt-BR) gerando narração | 3 |
| E10-03 | Como sistema, quero FFmpeg montando slideshow (cards do Image Generator + narração + legendas + música CC0) em MP4 9:16 | 5 |
| E10-04 | Como sistema, quero publicar o vídeo como Reel via Graph API | 3 |

## E11 — Analytics Engine (P0)

| ID | História | Pts |
|---|---|---|
| E11-01 | Como sistema, quero pixel próprio (GET 1×1 + endpoint) registrando visita/clique de checkout por LP com UTM | 3 |
| E11-02 | Como sistema, quero agregação diária (`MetricDaily`): visitas, cliques, vendas, receita, conversão por produto/canal | 3 |
| E11-03 | Como operador, quero dashboard com funil (impressão→visita→clique→venda), receita e ROI por produto | 5 |
| E11-04 | Como sistema, quero consolidar métricas sociais (E08-05) no funil | 2 (P1) |

## E12 — ROI Optimizer / Feedback Loop (P0 mínimo, P1 completo)

| ID | História | Pts |
|---|---|---|
| E12-01 | Como sistema, quero cron de 30 dias que classifica produtos (escalar / manter / iterar / matar) por regras de ROI e conversão | 5 |
| E12-02 | Como sistema, quero que "matar" arquive produto e dispare descoberta de substituto, mantendo ≥ 10 ativos | 3 |
| E12-03 | Como sistema, quero que "iterar" gere ações concretas (novo preço, nova headline LP, novo calendário social) | 5 |
| E12-04 | Como sistema, quero relatório de ciclo (o que funcionou, decisões, aprendizados em JSON) alimentando pesos do Trend Score e templates de prompt | 5 (P1) |
| E12-05 | Como operador, quero revisar/vetar decisões do otimizador no painel antes da execução | 2 |

## E13 — Painel Admin Angular (P0 transversal)

| ID | História | Pts |
|---|---|---|
| E13-01 | Dashboard geral: produtos ativos, receita do mês, pipeline em andamento, jobs falhos, consumo IA | 5 |
| E13-02 | Pipeline view: cada produto com etapa atual e ações (reprocessar, aprovar, pular) | 5 |
| E13-03 | Fila de aprovações (manuscrito, publicação, decisões do otimizador) | 3 |
| E13-04 | CRUD de configurações (tokens, cotas, crons, modos Auto/RequireApproval, temas) | 3 |
| E13-05 | Visualizador de logs estruturados com filtros | 3 |
| E13-06 | Tela de nichos e knowledge base (busca, reuso) | 3 (P1) |

**Total P0: ~150 pts (~15 semanas-pessoa). Caminho crítico: E00 → E01 → E04 → E07.**

---

## E14 — Media Gateway (P1 — Onda 2)

Espelha o AI Gateway de texto para **geração de imagens**: cadeia de resolvers com cota por provedor, cache content-addressable de bytes e fallback garantido no Skia local.

| ID | História | Pts |
|---|---|---|
| E14-01 | `IMediaGateway` + cadeia `IMediaResolver`, tabela `MediaUsage` (cota/provedor/dia/mês), cache de mídia (bytes, content-addressable) | 5 |
| E14-02 | Resolver Gemini/Imagen (text→image, free tier AI Studio) | 3 |
| E14-03 | Resolver Cloudflare Workers AI (Flux/SDXL, cota diária grátis) | 3 |
| E14-04 | Resolver HuggingFace Inference (SDXL/Flux, free rate-limit) | 2 |
| E14-05 | Resolver Pollinations (sem chave, custo zero — último antes do local) | 2 |
| E14-06 | Resolver Local Skia (embrulha `IImageComposer` — fallback garantido, nunca falha) | 2 |
| E14-07 | Briefs de imagem por template (capa/card/carrossel/cena) em `/prompts/media/*` | 3 |
| E14-08 | Telemetria no painel: provedor usado, cota restante, custo | 2 |
| E14-09 | Frames de cena de vídeo via Media Gateway (imagens por cena em vez de só cards Skia) | 3 |
| E14-10 | Resolver de vídeo generativo (Veo/RunwayML free quando disponível) | 2 |

## E15 — Loop de Aprendizado de Estilo (P1 — Onda 3)

O Claude Pro analisa a mídia gerada e ensina o sistema a gerar melhor, realimentando tanto os prompts dos provedores externos quanto os presets do Skia local.

| ID | História | Pts |
|---|---|---|
| E15-01 | Job `style.learn` (cron semanal): seleciona mídia recente + de melhor desempenho por nicho | 3 |
| E15-02 | AI Gateway (Claude vision, purpose `style.analyze`): descreve composição, paleta, tipografia, gancho, layout | 4 |
| E15-03 | `KnowledgeAssetType.MediaStyle` — playbook de estilo por nicho na KB existente | 2 |
| E15-04 | Realimentação A: aprendizados injetados nos prompts dos provedores (E14-07) | 3 |
| E15-05 | Realimentação B: presets de paleta/layout para Skia local — fallback também melhora | 3 |

## E16 — Marketing & Persuasão Studio (P1 — Onda 5)

Base curada de frameworks de persuasão + ferramentas que geram e avaliam copy, integradas aos pipelines de LP, social e vídeo.

| ID | História | Pts |
|---|---|---|
| E16-01 | Base de frameworks (AIDA, PAS, PASTOR, Cialdini, fórmulas de gancho) como `KnowledgeAssetType.MarketingFramework` | 3 |
| E16-02 | Linter de persuasão: pontua copy contra os frameworks e aponta lacunas | 5 |
| E16-03 | Gerador de ganchos/headlines e ângulos A/B por framework + nicho | 4 |
| E16-04 | Tela Marketing Studio: compor/avaliar copy, gerar variações, enviar para A/B de LP | 5 |
| E16-05 | Integração nos pipelines: LP, social e roteiro consultam ângulos vencedores; A/B alimenta ROI Optimizer | 4 |
