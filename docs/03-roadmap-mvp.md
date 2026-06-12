# 03 — Roadmap MVP

Premissa: 1 desenvolvedor + Claude Code, dedicação parcial. Fases sequenciais com entregáveis verificáveis; cada fase termina com o sistema **implantado e funcionando em produção** (deploy contínuo desde a Fase 0).

```
F0 Fundação ──► F1 Pipeline de Conteúdo ──► F2 Publicação ──► F3 Divulgação ──► F4 Inteligência ──► F5 Autonomia Total
  (2 sem)            (3 sem)                  (2 sem)            (2 sem)           (3 sem)             (2 sem)
```

## Fase 0 — Fundação (2 semanas) · Épicos: E00, E01

**Objetivo:** esqueleto produtivo em produção com IA funcionando.

Entregáveis:
- Solution Clean Architecture + testes, CI/CD GitHub Actions, container no VPS.
- SQLite + FileStore + Outbox/eventos + fila de jobs + Quartz.
- AI Gateway completo (cache → knowledge → Claude CLI) com telemetria.
- Painel Angular: login, dashboard vazio, tela de jobs e logs.
- **Paralelo (sem código):** criar conta Meta Developers + app review IG/FB; conta X Developer; validar fluxo manual Kiwify.

✅ Critério de saída: `POST /api/dev/ai-echo` gera texto via assinatura Pro, evento percorre outbox, job com retry visível no painel, deploy automático no merge.

## Fase 1 — Pipeline de Conteúdo (3 semanas) · Épicos: E02, E03, E04, E05, E09 (capa)

**Objetivo:** do nada a um **PDF comercial completo** sem intervenção.

Entregáveis:
- Trend Discovery com ≥ 3 fontes + score + tela de nichos.
- Knowledge Enrichment com índice de reuso.
- Ebook Generator (outline → capítulos retomáveis → revisão) + metadados comerciais.
- PDF Generator com 3 temas + capa via Image Generator.
- Pipeline view no painel com gate de aprovação do manuscrito.

✅ Critério de saída: disparar “gerar produto” num nicho descoberto e receber PDF profissional + capa + copy de venda, consumo de IA dentro do orçamento configurado.

## Fase 2 — Publicação (2 semanas) · Épicos: E06, E07

**Objetivo:** produto à venda com página própria.

Entregáveis:
- 2 templates de LP + copy automática + publicação no nginx com pixel.
- Kiwify Publisher via Playwright (com gate de aprovação) + webhooks de venda.
- Registro de `SaleEvent` e vínculo LP ↔ checkout.

✅ Critério de saída: 1 produto real publicado na Kiwify com LP no ar; venda de teste aparece no painel via webhook.

## Fase 3 — Divulgação (2 semanas) · Épicos: E08, E09 (cards)

**Objetivo:** tráfego orgânico automatizado.

Entregáveis:
- Cards sociais (feed + story) por template SkiaSharp.
- Calendário de conteúdo por produto + publicação automática IG/FB com UTM.
- (Se cota aprovada) X API integrado.

✅ Critério de saída: produto novo gera calendário de 30 dias e posts saem sozinhos no horário, rastreáveis por UTM.

## Fase 4 — Inteligência (3 semanas) · Épicos: E11, E12 (mínimo), E13

**Objetivo:** medir e decidir.

Entregáveis:
- Analytics: pixel + agregação diária + dashboard de funil/ROI.
- ROI Optimizer v1: classificação escalar/manter/iterar/matar com revisão humana, reposição automática para manter ≥ 10 produtos.
- Painel completo (aprovações, configurações, nichos).

✅ Critério de saída: ciclo de 30 dias fecha com relatório de decisões e disparo automático de substituições.

## Fase 5 — Autonomia Total + Vídeo (2 semanas) · Épicos: E10, E12 completo, P1s críticos

**Objetivo:** remover o humano do caminho feliz.

Entregáveis:
- Video Generator (Reels) integrado ao calendário.
- Feedback loop completo: aprendizados realimentam score de nicho e prompts.
- Modos `Auto` ativados por padrão; aprovação vira exceção (alertas).
- Teste sintético Playwright/Kiwify, A/B de LP, hardening e runbook.

✅ Critério de saída: **30 dias sem intervenção** mantendo ≥ 10 produtos ativos, com relatório mensal automático.

## Marcos de negócio

| Marco | Meta |
|---|---|
| M1 (fim F2) | 1º produto à venda |
| M2 (fim F3) | 3 produtos ativos com tráfego orgânico |
| M3 (fim F4) | 10 produtos ativos, funil medido ponta a ponta |
| M4 (fim F5 + 30d) | 1º ciclo 100% autônomo concluído; decisão de otimização executada sem humano |

## Fora de escopo do MVP (P2)

Multi-idioma, multi-conta social, tráfego pago, e-mail marketing, afiliados Kiwify, multi-tenant/SaaS para terceiros, app mobile.
