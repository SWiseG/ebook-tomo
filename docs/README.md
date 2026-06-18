# Plataforma EBOOK — Planejamento

SaaS autônomo para descoberta de nichos, geração, publicação, divulgação e otimização contínua de e-books, operando em ciclos de 30 dias com custo mínimo.

## Índice da documentação

| Doc | Conteúdo |
|---|---|
| [01-arquitetura.md](01-arquitetura.md) | Visão, princípios, arquitetura completa, decisões técnicas, riscos |
| [02-backlog.md](02-backlog.md) | Épicos e histórias priorizadas (P0/P1/P2) com estimativas |
| [03-roadmap-mvp.md](03-roadmap-mvp.md) | Fases, marcos e critérios de sucesso do MVP |
| [04-modelo-de-dados.md](04-modelo-de-dados.md) | Diagrama ER, tabelas SQLite, schemas JSON dos artefatos |
| [05-fluxogramas.md](05-fluxogramas.md) | Fluxos Mermaid: ciclo mestre, pipelines, feedback loop |
| [06-estrutura-de-pastas.md](06-estrutura-de-pastas.md) | Árvore completa da solução (backend, frontend, deploy) |
| [07-documentacao-tecnica.md](07-documentacao-tecnica.md) | Padrões, AI Gateway, eventos, jobs, cache, testes, convenções |
| [08-implantacao.md](08-implantacao.md) | Infra, Docker, CI/CD, backups, secrets, runbook operacional |
| [09-distribuicao.md](09-distribuicao.md) | Módulo de distribuição social: canais por nicho, calendário, carrossel, gate de aprovação |
| [10-geracao-ia-midia.md](10-geracao-ia-midia.md) | Media Gateway (E14): geração de imagens free-first + loop de aprendizado de estilo (E15) |

## Resumo executivo

- **Monolito modular** ASP.NET Core 8 em **1 container Docker** num VPS Linux barato (~US$ 5/mês) — API + jobs + scheduler + Angular estático no mesmo processo.
- **SQLite (WAL) + filesystem**: banco apenas para índice/estado; todo conteúdo gerado (capítulos, conhecimento, prompts, métricas brutas) vive em **JSON/arquivos versionáveis**.
- **AI Gateway com hierarquia de custo**: cache → base de conhecimento reutilizável → **Claude via assinatura Pro (CLI headless)** → API paga (desligada por padrão). Orçamento de tokens por pipeline.
- **Ciclo autônomo de 30 dias**: descobrir nichos → enriquecer conhecimento → gerar e-book + PDF + capa → landing page → Kiwify → posts sociais → coletar métricas → **ROI Optimizer** decide matar/iterar/escalar, mantendo ≥ 10 produtos ativos.
- **Custo recorrente alvo: < US$ 6/mês** (VPS + domínio). Todas as integrações usam camadas gratuitas (Meta Graph API, X free tier, Pexels, Piper TTS, FFmpeg, QuestPDF Community, Cloudflare Pages).

## Status

🟢 **Em produção** (https://app.tomolibrary.com.br). M1 concluído (1º produto sincronizado Kiwify). Próximo marco: M2 (3 produtos ativos + funil de conversão medido). Ver [roadmap](03-roadmap-mvp.md) para as ondas de entrega.
