# 09 — Módulo de Distribuição & Tráfego Social

> Planejamento do módulo que leva o produto publicado ao público. Pré-requisito: produto
> `Synchronized` (M1 concluído). Cobre F3 (Divulgação) e prepara F5 (autonomia + vídeo).
> **Decisões travadas com o usuário (2026-06-17):**
> - **Canal por nicho** (não conta central, não por produto).
> - **Orgânico agora** (cards + carrossel + Reels + afiliados Kiwify); **tráfego pago depois**.
> - 1ª entrega inclui: **tela de calendário + publicação orgânica + carrossel + Reels + gate de aprovação**.

## 1. Estado atual (o que já existe no código)

- **Calendário** (E08-02): IA gera 10–14 posts/30 dias (tipos Launch/Value/Proof/Offer) por produto → `SocialPost` (status `Planned`), com UTM e agendamento.
- **Cards** (E09): SkiaSharp renderiza **1 imagem** por post (feed 1080×1080 / story 1080×1920) + fundo Pexels + paleta por nicho. **Carrossel não existe.**
- **Publicação** (E08-01): `MetaGraphPublisher` (IG/FB via Graph API) — hoje **1 conta global** (`Meta__PageId`/`Meta__IgUserId` únicos), **gated** (sem credenciais).
- **Agendamento** (E08-03): cron diário `DispatchDuePostsJob` enfileira posts vencidos **se `social.autoPublish`** — senão ficam agendados.
- **Reels** (E10): roteiro (IA) → TTS Piper (pt-BR) → FFmpeg monta 9:16 → publica como Reel. **Pronto, mas desligado** (`video.enabled=false`) e **Piper/FFmpeg ausentes no container**.
- **Métricas** (E11/E12): pixel + UTM + funil/ROI por produto já ligados; otimizador usa `Synchronized`.
- **UI**: **não há tela de social** — só lista read-only no detalhe do produto.

## 2. Mudança central de modelo: Canal por nicho

Hoje o publisher usa **uma** conta Meta global. Passa a existir o conceito de **Canal**:

- **Entidade `Channel`** (novo): vinculada a um **nicho**, com plataforma (Meta/IG+FB), credenciais
  próprias (`PageId`, `IgUserId`, token de longa duração, `PublicMediaBaseUrl`), handle e status do token.
- Roteamento: ao publicar um `SocialPost`, resolve o **Channel do nicho do produto**. Sem canal
  configurado para o nicho → post fica `Planned`/`Skipped` com aviso (não quebra).
- Credenciais por canal saem do `appsettings`/env único e passam a ser **gravadas/gerenciadas** (tela de
  Canais). Segredos nunca no repo (mesma política do resto).

## 3. Ciclo de vida do módulo (end-to-end)

```
1. Produto → Synchronized            (dispara a distribuição)
2. IA gera a CAMPANHA                 calendário + roteiros de Reel + (futuro) textos de anúncio
3. Render de ASSETS                   cards, CARROSSEL (multi-slide), REEL (vídeo) — por paleta do nicho
4. GATE de aprovação (decisão do usuário)  revisar/editar copy, trocar/regerar arte, aprovar por post
5. Agendamento por Canal              (orgânico) — cadência ~2–3/semana
6. Publicação orgânica                Graph API no Canal do nicho, com UTM por post
7. Métricas                           alcance/cliques (post) + visitas/vendas (pixel+UTM+webhook)
8. ROI Optimizer                      escalar / iterar (nova copy-arte) / matar
9. Aprendizados                       realimentam prompts e score de nicho (F5)
   (Fase futura) Tráfego PAGO         Meta Ads sobre os melhores criativos; Afiliados Kiwify
```

## 4. Capacidades a construir (1ª entrega)

1. **Canal por nicho** — entidade + CRUD + roteamento do publisher + tela de Canais.
2. **Gate de aprovação** — `SocialPost` ganha estado de aprovação; só publica após aprovado no painel
   (substitui o auto cego). `social.autoPublish` vira "auto-aprovar" opcional por canal/produto.
3. **Tela de Calendário** — visão mensal por produto/canal: status por post, **preview** do
   card/carrossel/reel, **editar copy**, **regenerar arte**, **aprovar / agendar / publicar agora**.
4. **Carrossel** — nova arte multi-slide no Skia (capa + N slides de valor) + publicação como carrossel
   no IG (Graph API: container `CAROUSEL` com filhos).
5. **Reels** — ligar E10: Piper + FFmpeg no container (Dockerfile), `video.enabled`, geração e
   publicação de Reel pelo Canal.
6. **Publicação orgânica multi-canal** — `MetaGraphPublisher` lê credenciais do Canal (não do env global).

## 5. Telas (UI) — novo grupo "Distribuição" no menu

- **Calendário**: grade mensal por produto; chips de status (Planned/Aguardando aprovação/Agendado/
  Publicado/Falhou); preview do criativo; ações por post (editar, regerar, aprovar, publicar agora).
- **Canais**: lista de canais por nicho; conectar Meta (Página+IG), validade do token, health; criar/editar.
- **Desempenho**: alcance/cliques/CTR por post e por produto, integrado ao funil/ROI existente.
- **Detalhe do produto**: a aba social atual vira interativa (atalho para o calendário do produto).
- (Fase paga, futuro) **Campanhas**: orçamento, públicos, criativos, performance de anúncios.

## 6. O que o usuário precisa fornecer

**Para destravar o orgânico (Fase A — fecha M2):**
1. **Meta Business Manager + App** (Graph API) em **Live**, com `instagram_content_publish`,
   `pages_manage_posts`, `pages_read_engagement` + **App Review** aprovado.
2. **Por nicho ativo** (começar pelo 1º, ex.: Finanças): **1 Página FB + 1 conta IG Business** vinculada.
3. **Token de longa duração** (idealmente *System User token*) por canal → `PageId`, `IgUserId`,
   `AccessToken`, `PublicMediaBaseUrl` (URL pública das mídias — Railway).
4. **Identidade** de cada canal: handle, nome, bio, logo (paleta por nicho já existe).

**Decisões (sem ativo):** nível de aprovação (gate por padrão; auto opcional), cadência, redes futuras.

**Fase paga (depois):** Ad Account + Marketing API + método de pagamento + orçamento.
**Vídeo:** confirmar ligar E10 (eu cuido de Piper/FFmpeg no container).

## 7. Fases de implementação (entregáveis verificáveis)

- **Fase A — Orgânico + Canais + Calendário:** entidade Channel + tela de Canais + tela de Calendário
  + gate de aprovação + publicação orgânica IG/FB pelo canal do nicho. ✅ saída: 1ª campanha orgânica
  do produto sincronizado publicada e rastreada (rumo ao M2).
- **Fase B — Conteúdo rico:** carrossel (Skia multi-slide) + Reels (E10 ligado no container).
- **Fase C — Aquisição paga:** Meta Ads (Marketing API) sobre os criativos vencedores.
- **Fase D — Escala:** afiliados Kiwify, multi-canal por nicho, TikTok/YouTube Shorts, feedback loop (F5).

## 8. Riscos / pontos de atenção

- **App Review do Meta** pode levar dias — é o caminho crítico da Fase A; iniciar cedo.
- **Token expira** — usar System User token; tela de Canais mostra validade e alerta.
- **Mídia pública**: o Graph API busca `image_url`/`video_url` por URL pública (`/media/...`) — exige
  `PublicMediaBaseUrl` acessível (já há rota `/media` pública).
- **Reels no container**: adicionar Piper+FFmpeg aumenta a imagem; validar no Railway.
- **Cold start de audiência**: orgânico cresce devagar; por isso o pago entra na Fase C para aquisição real.
