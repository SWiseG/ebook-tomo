# 03 — Roadmap

> Princípio condutor: **medir → gerar com IA free → aprender → persuadir**. Cada onda termina em produção. Sem dados reais de venda e conversão, o otimizador e o loop de aprendizado decidem no escuro — por isso a medição vem primeiro.

## Estado (2026-06-17)

| Fase original | Estado |
|---|---|
| F0 — Fundação (E00, E01) | ✅ completo |
| F1 — Pipeline de Conteúdo (E02–E05, E09) | ✅ completo |
| F2 — Publicação (E06, E07) | ✅ M1 concluído (1º produto sincronizado Kiwify) · E07-01 substituído por fluxo manual-assistido · E07-02 webhooks pendente |
| F3 — Divulgação (E08, E09 cards) | ✅ A1–A6 completos (canal por nicho, gate de aprovação, calendário, carrossel) · A7 Reels pendente |
| F4 — Inteligência (E11, E12, E13) | ⚠️ parcial — analytics (E11-01/02), E12-02/03 a confirmar |
| F5 — Autonomia Total | ❌ não iniciado |

---

## Ondas de entrega

```
Onda 1       Onda 2       Onda 3       Onda 4       Onda 5       Onda 6
Medir $   ►  IA de Mídia ► Aprender  ► Vídeo c/IA ► Persuasão  ► Autonomia
(E07-02        (E14)         (E15)      (A7+E14-09)   (E16)        (F5+WS5)
 E11 E12)
```

---

## Onda 1 — Fechar o loop do dinheiro

Sem vendas e conversão reais no banco, o restante do sistema mede o nada. Esta onda fecha o que estava planejado e ainda falta.

| Item | O quê | Prioridade |
|---|---|---|
| **E07-02** | Webhooks Kiwify (venda/refund) → `SaleEvent` → dashboard de funil | crítico |
| **E11-01** | Pixel próprio (GET 1×1 + endpoint) por LP com UTM | crítico |
| **E11-02** | Agregação diária `MetricDaily` (visitas, cliques, vendas, receita, conversão) | crítico |
| **E11-03** | Dashboard de funil/ROI conectado aos dados reais | conectar |
| **UTM uniformes** | UTMs consistentes em todos os posts sociais (hoje só o Reel tem) | alta |
| **E12-02/03** | "Matar" → arquiva + repõe substituto; "Iterar" → preço/headline/calendário | alta |

**Marco M2:** 3 produtos ativos com tráfego orgânico + funil de conversão medido ponta a ponta.

---

## Onda 2 — E14: Media Gateway (imagem free-first)

O AI Gateway de **texto** já é uma cadeia de resolvers com cota e cache. Espelhamos o mesmo padrão para **mídia**:

```
IMediaGateway
  └─ cadeia IMediaResolver, em ordem, com cota (MediaUsage por provedor/dia/mês):
     cache → Gemini/Imagen → Cloudflare Workers AI → HuggingFace → Pollinations → LOCAL Skia
              (bate cota ou falha → cai para o próximo; local nunca falha)
     toda mídia nova é cacheada (content-addressable, igual ao ai-cache)
```

| ID | História | Pts |
|---|---|---|
| E14-01 | `IMediaGateway` + cadeia `IMediaResolver`, tabela `MediaUsage` (cota/provedor/dia/mês), cache content-addressable de mídia (bytes) | 5 |
| E14-02 | Resolver **Gemini/Imagen** (text→image, free tier AI Studio) | 3 |
| E14-03 | Resolver **Cloudflare Workers AI** (Flux/SDXL, cota diária grátis) | 3 |
| E14-04 | Resolver **HuggingFace Inference** (SDXL/Flux, free rate-limit) | 2 |
| E14-05 | Resolver **Pollinations** (sem chave, custo zero) — último antes do local | 2 |
| E14-06 | Resolver **Local Skia** (embrulha `IImageComposer` atual — fallback garantido, nunca falha) | 2 |
| E14-07 | Briefs de imagem por template (capa/card/carrossel/cena) em `/prompts/media/*` + prompt de geração por nicho | 3 |
| E14-08 | Telemetria no painel: qual provedor gerou, cota restante, custo zero vs geração local | 2 |

**Marco:** todas as capas, cards e carrosseis novos passam pelo Media Gateway — custo de imagem = $0 até esgotar a cota.

---

## Onda 3 — E15: Loop de aprendizado de estilo

O Claude Pro vira o "professor de design": analisa a mídia gerada pelas IAs grandes (e a que **converteu** melhor, via Onda 1) e ensina o sistema a gerar melhor.

| ID | História | Pts |
|---|---|---|
| E15-01 | Job agendado `style.learn` (cron semanal): seleciona mídia recente + de melhor desempenho por nicho | 3 |
| E15-02 | AI Gateway (Claude vision, purpose `style.analyze`): descreve composição, paleta, tipografia, gancho visual, layout, porquê converte | 4 |
| E15-03 | Novo `KnowledgeAssetType.MediaStyle` (playbook de estilo por nicho) gravado na KB existente | 2 |
| E15-04 | Realimentação A: aprendizados injetados nos prompts dos provedores generativos (E14-07) via `IPromptLibrary` | 3 |
| E15-05 | Realimentação B: aprendizados geram presets de paleta/layout/tipografia para o Skia local — o fallback também melhora | 3 |

**Marco:** após 4 semanas, o sistema gera briefs de imagem melhores sem intervenção humana.

---

## Onda 4 — Vídeo com IA (conclui E10)

| ID | História | Pts |
|---|---|---|
| A7 / E10-02/03 | Piper (TTS pt-BR) + FFmpeg no Dockerfile; ligar E10 em produção | 5 |
| E14-09 | Frames de cena via Media Gateway — imagens generativas por cena em vez de só cards Skia | 3 |
| E14-10 | Resolver de vídeo generativo (ex.: Veo, RunwayML free) quando/se disponível free — senão FFmpeg local segue como espinha dorsal | 2 |

> **Nota:** para vídeo generativo a oferta free ainda é escassa. O FFmpeg local continua a espinha dorsal; a IA entra como frames/b-roll por cena — sem reescrever o pipeline atual.

---

## Onda 5 — E16: Marketing & Persuasão Studio

| ID | História | Pts |
|---|---|---|
| E16-01 | Base curada de frameworks de persuasão (AIDA, PAS, PASTOR, 6 princípios de Cialdini, fórmulas de gancho, tratamento de objeções) na KB como `KnowledgeAssetType.MarketingFramework` | 3 |
| E16-02 | **Linter de persuasão**: pontua copy (LP/social/vídeo) contra os frameworks, aponta lacunas (sem promessa, sem prova, sem urgência etc.) | 5 |
| E16-03 | Gerador de **ganchos/headlines** e de **ângulos A/B** parametrizado por framework e por nicho | 4 |
| E16-04 | Tela **Marketing Studio** no painel: compor/avaliar copy, gerar variações, enviar ângulos para o A/B de LP | 5 |
| E16-05 | Integração nos pipelines: copy de LP, social e roteiro de vídeo consultam os ângulos vencedores; A/B alimenta o ROI Optimizer | 4 |

**Marco M3:** 10 produtos ativos, funil medido ponta a ponta, copy otimizada por dados de conversão reais.

---

## Onda 6 — Onboarding & Autonomia Total

| Item | O quê |
|---|---|
| **WS5 — Onboarding** | Reescrever `/tutorial` cobrindo fluxo real (nicho→ebook→aprovação→LP→Kiwify→social) + tooltips contextuais |
| **E12-04** | Feedback loop completo: aprendizados de ciclo realimentam score de nicho e templates de prompt |
| **F5 — Autonomia** | Modos `Auto` por padrão; aprovação vira exceção/alerta; teste sintético Playwright/Kiwify; hardening; runbook |

**Marco M4 (fim F5 + 30d):** 1º ciclo 100% autônomo concluído — 30 dias sem intervenção, ≥ 10 produtos ativos, relatório mensal automático, decisão de otimização executada sem humano.

---

## Marcos de negócio

| Marco | Meta | Onda |
|---|---|---|
| M1 | 1º produto à venda ✅ | — |
| M2 | 3 produtos ativos + funil de conversão medido | Onda 1 |
| M3 | 10 produtos ativos + copy otimizada por dados + IA generativa de mídia | Ondas 2–5 |
| M4 | 1º ciclo 100% autônomo (30 dias sem intervenção) | Onda 6 |

---

## Fora de escopo

Multi-idioma, multi-conta social, tráfego pago, e-mail marketing, afiliados Kiwify, multi-tenant/SaaS para terceiros, app mobile, multi-usuário com papéis (decisão 2026-06-17).
