# Sprint C — Motor de conversão autônomo (o diferencial defensável)

> **Meta da sprint:** fechar o loop **gera → serve → mede → aprende → promove**. É o trabalho mais pesado e o que
> nenhum concorrente humano copia, porque exige rodar 24/7. Reusa muita infra que já temos (`LpLab`, pixel,
> `AnalyticsEvent`, `MetricDaily`, `OptimizationService`).
>
> Itens: **C1** variantes de LP em produção · **C2** Smart Traffic (roteamento por conversão) · **C3** promoção
> automática da vencedora · **C4** personalização por origem de tráfego.
> Pré-requisito: Sprints A/B no ar (precisamos de produtos publicando com qualidade para medir).

---

## Contexto técnico (o que já existe e vamos reaproveitar)
- `LpJobHandler` gera **1** LP e grava como `ArtifactType.LpBundle` (HTML auto-contido). `LandingPageBuilder` já
  monta 3 templates e aceita variações de copy.
- `LpLab` (`EnqueueTestLpCommand` + `GenerateTestLp`) **já gera variantes sob demanda** — hoje só para teste interno.
- Pixel `/px.gif` + `AnalyticsEvent` + `MetricDaily` + `IMetricsReader` medem visita→checkout→venda por produto.
- `OptimizationService` roda ciclo mensal e já classifica Iterate/Kill/Scale; "Iterate" já sugere `refreshLandingPage`.

O salto é **conectar essas peças num loop fechado por variante**, não construir do zero.

---

## C1 — Variantes de LP em produção  ·  esforço: **high**  ·  impacto: **muito alto**

### Objetivo
Publicar **2–3 variantes** da LP por produto (diferentes headline/hero/ordem de prova/oferta) em vez de uma única,
para que o sistema descubra qual converte mais.

### Base de pesquisa
- Landingi/Unbounce 2026: variantes geradas por IA elevam conversão **15–30%**; testar headline/CTA/hero é o de maior alavanca.

### Abordagem
- Reusar `LandingPageBuilder` para emitir N variantes determinísticas (ex.: variar `Headline`, ordem de seções,
  rótulo do CTA, template). A copy alternativa vem de uma chamada de IA (purpose `lp.variants`) ou de regras.
- Persistir cada variante como artefato/arquivo com um **id de variante** estável.

### Encaixe arquitetural
- Novo modelo de domínio leve: `LpVariant` (produto, id, caminho, métricas agregadas) — **migration** (nova tabela)
  ou guardar variantes no MetaJson de `LpBundle` + métricas em `MetricDaily` com dimensão `variant`. **Decidir cedo:**
  recomendação = nova tabela `LpVariant` (consultas de "vencedora" ficam limpas).
- `LpJobHandler` passa a gerar N variantes; rota pública serve por id (`/lp/{slug}?v={variant}` ou path).
- Sem quebrar o pixel: o `data-cta`/pixel já existem; adicionar a dimensão `variant` ao evento.

### Passos
1. Modelar `LpVariant` (+ migration + rebuild) **ou** decidir pela abordagem MetaJson (documentar a escolha).
2. `LpJobHandler` gera N variantes (Settings `lp.variantCount`, default 1 = comportamento atual).
3. Servir variante por id; propagar `variant` no pixel/analytics.

### Testes
- Geração de N variantes distintas a partir de uma copy fixture.
- `variantCount=1` reproduz o comportamento atual (não-regressão).

### DoD
- Produto publica com N variantes rastreáveis individualmente.

---

## C2 — Smart Traffic (roteamento por conversão)  ·  esforço: **high**  ·  impacto: **muito alto**

### Objetivo
Distribuir o tráfego entre variantes **proporcional ao desempenho** — explorar pouco as ruins, explotar as boas —
em vez de 50/50 fixo. É o "Smart Traffic" do Unbounce.

### Base de pesquisa
- Unbounce: roteamento inteligente dá **+20%** de conversão média vs A/B clássico.
- Algoritmo: **multi-armed bandit** — Thompson Sampling (bayesiano, robusto com pouco dado) ou epsilon-greedy (mais simples). **Sem biblioteca** (poucas dezenas de linhas), implementável no Domain (testável puro).

### Abordagem
- Ao servir a LP, o roteador escolhe a variante via bandit usando as métricas acumuladas (`MetricDaily` por variante).
- Começa uniforme (sem dados) e converge para a melhor conforme o pixel acumula visitas/vendas.

### Encaixe arquitetural
- `LpVariantRouter` no **Domain** (função pura: dado o histórico de cada variante → escolhe id). Testável sem infra.
- Aplicado no endpoint público que serve a LP (Api), lendo métricas via `IMetricsReader`.
- Determinismo em teste: injetar a fonte de aleatoriedade (seed) para asserts.

### Passos
1. Implementar `LpVariantRouter` (Thompson Sampling recomendado) no Domain + testes de convergência.
2. Ligar no serving da LP, lendo métricas por variante.
3. Setting `lp.smartTraffic` (default off → round-robin/única, compat).

### Testes
- Convergência: variante com conversão maior recebe a maioria das amostras após N rodadas (seed fixo).
- Sem dados → distribuição ~uniforme.

### DoD
- Tráfego flui para a variante que mais converte, automaticamente, sem intervenção.

---

## C3 — Promoção automática da vencedora  ·  esforço: **medium**  ·  impacto: **alto**

### Objetivo
Quando uma variante vence com **significância** (volume mínimo + diferença estável), promovê-la a padrão e
aposentar as demais — fechando o loop com o ciclo de otimização que já existe.

### Base de pesquisa
- MindStudio "self-improving A/B agent": o valor está em **fechar o loop** (promover + gerar nova leva de desafiantes).

### Encaixe arquitetural
- Estender `OptimizationService` (decisão **Iterate** já existe): adicionar passo "promover variante vencedora" e,
  opcionalmente, gerar uma nova leva de desafiantes (volta ao C1). Emite evento de domínio → Outbox → handler idempotente.
- Critério de parada: volume mínimo de visitas/variante + janela mínima (Settings `lp.promote.minVisits`, `lp.promote.minDays`).
- Respeitar o gate humano existente (`roi.autoExecute`): se desligado, a promoção fica **proposta** para veto.

### Passos
1. Calcular vencedora com significância simples (intervalo/limiar) no Domain.
2. No ciclo de otimização, propor/executar a promoção conforme `roi.autoExecute`.
3. (Opcional) gerar nova leva de desafiantes ao promover.

### Testes
- Vencedora clara + volume suficiente → decisão de promoção; volume insuficiente → mantém teste.
- `autoExecute` off → fica proposta (veto humano).

### DoD
- O sistema converge sozinho para a melhor LP e mantém um teste vivo — melhora contínua de conversão.

---

## C4 — Personalização por origem de tráfego  ·  esforço: **medium**  ·  impacto: **médio**

### Objetivo
Trocar headline/abertura conforme `utm_source`/termo (dynamic text replacement) — relevância eleva conversão de tráfego pago.

### Base de pesquisa
- Convertri/Unbounce 2026: dynamic text replacement e personalização por origem são padrão para Ads.

### Abordagem
- A LP já é HTML estático auto-contido. Adicionar um **pequeno script** que, a partir dos parâmetros de URL,
  substitui tokens pré-definidos (headline/eyebrow) por variantes mapeadas — **sem servidor** (mantém o deploy estático/Cloudflare).
- As variações de texto por origem vêm da copy (campo novo opcional) ou de um mapa simples por `utm_campaign`.

### Encaixe arquitetural
- Extensão no `LandingPageBuilder` (fragmento JS + tokens). Sem migration. Compatível com C1/C2 (variante × origem).

### Passos
1. Definir tokens substituíveis e o mapa origem→texto (na copy/Settings).
2. Injetar o script de substituição no HTML (com fallback para o texto padrão).

### Testes
- Render contém os tokens e o script; sem parâmetro → texto padrão (não-regressão).

### DoD
- A mesma LP fala a língua de cada campanha sem perder o deploy estático.

---

## Ordem sugerida e fechamento
```
C1 variantes  →  C2 smart traffic  →  C3 promoção automática  →  C4 personalização
```
**Ao final da Sprint C:** cada produto publica múltiplas LPs, o tráfego é roteado para a melhor, a vencedora é
promovida sozinha e a página se adapta à origem. **Este é o fosso competitivo** — automação que humano não escala.

## Fontes
- [Conversion Rate Optimization with AI 2026 — Landingi](https://landingi.com/blog/conversion-rate-optimization-with-ai/)
- [Unbounce Smart Traffic / CRO](https://unbounce.com/)
- [Self-Improving A/B Testing Agent — MindStudio](https://www.mindstudio.ai/blog/self-improving-ab-testing-agent-landing-pages-ad-copy)
- [Best Landing Page Builder 2026 — Convertri](https://www.convertri.com/best-landing-page-builder) (dynamic text replacement)
- Multi-armed bandit / Thompson Sampling (algoritmo, sem dependência externa).
