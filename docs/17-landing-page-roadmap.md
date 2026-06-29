# 12 — Roadmap de Landing Pages de alta conversão

> Plano para evoluir o gerador de landing pages (E06) do nível "página de oferta
> simples" para o padrão das LPs de info-produto que realmente convertem no Brasil.
> Baseado em: (a) auditoria do nosso framework atual; (b) dissecação de 4 LPs de
> referência em `docs/lp-examples/`; (c) tendências de conversão 2026.
> Criado em 2026-06-16.

---

## 1. Objetivo

Hoje geramos uma LP funcional, mas "magra" em persuasão. As referências mostram um
padrão denso e previsível de blocos e gatilhos que esperamos que aumente a conversão
(visita → checkout). A meta é levar o `LandingPageBuilder` a produzir páginas com a
mesma espinha dorsal persuasiva das referências — **de forma 100% automática, por nicho**,
mantendo a arquitetura (copy via IA → modelo → builder HTML auto-contido estático).

---

## 2. O que já temos (auditoria)

Arquivos-chave:
- `src/Ebook.Application/Content/Lp/LandingPage.cs` — `LandingPageBuilder` (função pura)
  com 2 templates: **Aurora** (herói escuro com gradiente, sans moderno) e **Editorial**
  (claro, serifado, ar de revista). `LpTemplateSelector.ForNiche` escolhe por hash FNV-1a do slug.
- `src/Ebook.Application/Content/Lp/LpContent.cs` — `LpCopyDto` (modelo de copy).
- `src/Ebook.Application/Content/Images/NichePalette.cs` — `NicheStyleCatalog`: 8 categorias
  de nicho (Finance, Health, SelfHelp, Marketing, Tech, Fiction, Education, General), cada uma
  com cores (Background/Accent/OnDark) + par tipográfico (Heading/Body), psicologia das cores + 60-30-10.
- `src/Ebook.Application/Content/LpJobHandler.cs` — orquestra: lê `sales-copy.json` (copy gerada
  pela IA, purpose `ebook.sales-copy`), lê a capa, resolve a paleta (config por nicho ou catálogo),
  injeta `checkoutUrl`/`pixelUrl`, renderiza e publica o bundle HTML; gate de aprovação.

### Seções que o builder gera hoje
`Hero` (headline + sub + 1 CTA + capa) → `Pain` (texto) → `Solution` (texto + bullets ✓) →
`Offer` (título + preço com âncora riscada + bônus + CTA) → `FAQ` (`<dl>`) → `Footer` + pixel/UTM.

### Pontos fortes
- HTML auto-contido (CSS inline, capa em data URI) servido estático em `/lp/{slug}` — rápido, simples.
- Paleta e tipografia **por nicho** já existem e são profissionais.
- Pixel próprio + propagação de UTM para os CTAs (integra com analytics E11).
- Âncora de preço (riscado) e bônus já modelados.

### Limitações (o que falta vs. referências)
Sem: prova social (rating/contagem/depoimentos), autoridade do autor, selos de confiança,
faixa de mídia ("citado em"), garantia/reversão de risco, parcelamento ("12x"), empilhamento
de bônus com valores, escassez/urgência (contador, vagas), CTAs repetidos, CTA fixo no mobile,
FAQ interativa, animações, OG/Twitter/JSON-LD, rodapé legal (CNPJ/disclaimers). Só 1 CTA e 1 imagem.

---

## 3. Análise das 4 referências (`docs/lp-examples/`)

As 4 cobrem exatamente nossos nichos: **SERENO** (Finanças), **VÍNCULO** (Relacionamento),
**LEVITA** (Saúde/emagrecimento), **NEXORA** (Marketing). Todas seguem o mesmo esqueleto.

### 3.1 Esqueleto comum (14 blocos, de cima p/ baixo)
1. **Barra de anúncio fixa** — desconto + contador + escassez ("Lote fundador · 68% OFF · encerra em…").
2. **Nav fixa** — logo + âncoras (O Método, Resultados, Planos, Dúvidas) + CTA.
3. **Hero** — pill de prova social ("+42.000 brasileiros…") → headline com **palavras destacadas**
   → subheadline no padrão "sem X, sem Y, sem Z" → CTA primário (+ secundário "ver como funciona")
   → rating "4.9/5 · N avaliações" → selos (Garantia, Dados criptografados, Aprovado por…) →
   visual (produto / autor / dashboard) + card de estatística ("+R$ 1.847 economia média").
4. **Faixa de autoridade/mídia** — "Citado e recomendado por" + logos (carrossel).
5. **Dor (PAS)** — dores relacionáveis em cards/checklist, com palavras emocionais destacadas.
6. **Método/Solução** — 3–4 pilares ou linha do tempo por dias (Dia 1, Dia 2–7, Dia 8+).
7. **Autoridade do autor** — bio + credenciais + números (forte em VÍNCULO/NEXORA).
8. **Resultados/Estatísticas** — cards de métrica grandes (R$, %, nº de alunos).
9. **Depoimentos** — nome + papel/transformação + **selo de resultado específico** (3–6 deles).
10. **Oferta/Preço** — empilhamento de bônus com valores → "valor cheio" (âncora) → preço com
    desconto → **parcelamento (12x)** + "menos de R$ 1/dia"; oferta única **ou** 3 planos (chamariz);
    micro-selos (pagamento seguro, Pix/boleto/cartão, acesso imediato); escassez ("142 vagas").
11. **Garantia** — reversão de risco nomeada ("Garantia incondicional de 30 dias", "o risco é nosso").
12. **FAQ** — 5–6 perguntas que quebram objeções.
13. **CTA final** — fechamento emocional + contador + CTA.
14. **Rodapé** — logo + legal (CNPJ, disclaimer) + Privacidade/Termos/Contato.

### 3.2 Diferenças por nicho (tom + ênfase)
| LP | Nicho | Tom | Destaques específicos |
|----|-------|-----|----------------------|
| **SERENO** | Finanças | Calmo, "paz financeira", confiança | 3 planos (chamariz no meio), selos CFP/Open Finance/AES-256, estat. "R$ 1.847" |
| **VÍNCULO** | Relacionamento | Íntimo, emocional, confidencial | Forte bio da autora (psicóloga, CRP, EFT/Gottman), oferta única, disclaimer "não substitui terapia" |
| **LEVITA** | Saúde | Aspiracional, "leveza", urgência | Selo ANVISA/100% Natural, linha do tempo por fases, garantia 90 dias, "oferta relâmpago" |
| **NEXORA** | Marketing | Ambicioso, prova de renda | **Dashboard "ao vivo"** + notificações de venda (FOMO), história do fundador, bônus stack R$13.985→12x R$97 |

### 3.3 Tokens de design observados
- **Fundo**: off-white quente (ex.: SERENO `#FCFBF8`) — não o herói escuro que usamos por padrão.
- **Acentos vibrantes** por tema (ex.: laranja `#FE7B02`, vermelho `#FE3F21`, rosa `#F858BC`).
- Tipografia: display/sans forte no título + corpo legível (Google Fonts) — alinhado ao que já temos.
- Imagens: hero autêntico (foto de produto / autor / dashboard), não stock genérico.
- Componentes: pills, cards com sombra suave, badges de selo, contadores, sticky bars.

### 3.4 Inventário de gatilhos de persuasão (recorrentes)
Contadores (×3: topo, preço, final) · escassez (vagas/kits/lote) · prova social (rating+contagem,
"+N", logos de mídia, depoimentos com resultado, ticker de vendas ao vivo) · autoridade (bio,
credenciais, imprensa) · reversão de risco (garantia nomeada, X dias) · ancoragem de preço
(riscado + soma de bônus) · parcelamento ("12x", "menos de R$1/dia") · bônus empilhados com valor ·
palavras destacadas · pré-objeção "sem X, sem Y" · especificidade (números exatos) · enquadramento
de transformação (antes→depois) · FOMO ("daqui a 21 dias você vai…") · compliance (ANVISA/CFP/disclaimers).

---

## 4. Tendências 2026 (corroboração externa)
- **Hero decide em 10–20s**: clareza acima de tudo; headline = o quê + para quem + benefício.
- **Headline por benefício converte +27%** vs. por funcionalidade.
- **"Wall of Love"**: todas as provas visíveis de uma vez (texto + foto + vídeo + nome/local reais).
- **Seção "Como funciona"** obrigatória (faz o próximo passo parecer fácil).
- **Personalização dinâmica** (dynamic text replacement por origem do anúncio/UTM).
- **Visual autêntico** (produto/autor reais) > stock; layouts limpos e focados em 1 ação.
- **Menos fricção** no caminho até o checkout.

Fontes: [involve.me — trends](https://www.involve.me/blog/landing-page-trends), [Zoho LandingPage](https://www.zoho.com/landingpage/landing-page-design-trends.html), [Neel Networks 2026](https://www.neelnetworks.com/blog/high-converting-landing-page-design-2026/), [digitalapplied — stats 2026](https://www.digitalapplied.com/blog/landing-page-statistics-2026-conversion-data-points).

---

## 5. Gap analysis (nós × referências/2026)
| Elemento | Temos? | Prioridade |
|----------|:------:|:----------:|
| Prova social no hero (rating + contagem) | ❌ | P0 |
| Depoimentos com resultado | ❌ | P0 |
| Garantia / reversão de risco | ❌ | P0 |
| Parcelamento ("12x") + "menos de R$1/dia" | ❌ | P0 |
| Empilhamento de bônus com valores | ⚠️ (bônus sem valor) | P0 |
| Faixa de autoridade / mídia | ❌ | P1 |
| Bio/autoridade do autor | ❌ | P1 |
| Escassez + contador (urgência) | ❌ | P1 |
| Selos de confiança (segurança/cert.) | ❌ | P1 |
| Estatísticas/resultados (cards de métrica) | ❌ | P1 |
| CTAs repetidos + CTA fixo mobile | ⚠️ (2 CTAs) | P1 |
| Linha do tempo "Como funciona" | ⚠️ (bullets) | P1 |
| FAQ interativa (accordion) | ⚠️ (`<dl>`) | P2 |
| Animações de scroll-reveal | ❌ | P2 |
| OG/Twitter + JSON-LD (Product/FAQ/Rating) | ❌ | P2 |
| Rodapé legal + disclaimers por nicho | ❌ | P2 |
| Fundo off-white quente / acentos vibrantes | ⚠️ (herói escuro) | P2 |
| Novos templates além de Aurora/Editorial | ⚠️ (2) | P3 |
| A/B de headline/CTA (via otimizador E12) | ❌ | P3 |

---

## 6. Roadmap por fases

### Fase 1 — Modelo de copy + seções persuasivas essenciais (P0) — maior impacto ✅ FEITO (2026-06-16)
**Objetivo:** o builder passa a renderizar os blocos que mais movem conversão.

> **Entregue:** `LpCopyDto` estendido (+ sub-DTOs); `LandingPageBuilder` reescrito com
> ProofPill, HeroProof (rating/selos), MediaBar, StepsTimeline ("Como funciona"), StatsBlock,
> Testimonials, AuthorBlock, BonusStack (com valor + soma), PriceBlock (parcelamento 12x),
> TrustRow, GuaranteeBlock, FinalCtaSection — cada bloco condicional (omite sem dados).
> Prompt `ebook/sales-copy` estendido com guardrails de **honestidade** (não inventa
> rating/depoimentos/imprensa/stats/autor — só dados reais). 170 testes verdes.
> **Pendente desta fase:** rating/testimonials/stats/mediaLogos/author só renderizam quando
> houver dados REAIS (futuro: alimentar via webhooks Kiwify / reviews).

1. **Estender `LpCopyDto`** (`LpContent.cs`) com campos opcionais (a copy pode vir parcial; builder com fallback):
   - `ProofPill` (string), `Rating` (`{ value, count }`), `TrustBadges` (string[])
   - `MediaLogos` (string[] — nomes; render como texto/badge sem imagem externa)
   - `Stats` (`{ value, label }[]`), `Testimonials` (`{ quote, name, role, result }[]`)
   - `Author` (`{ name, title, credentials, bio, highlights[] }`)
   - `Guarantee` (`{ title, body, days }`), `Bonuses` → `{ name, description, value }[]` (com valor!)
   - `Price` → acrescentar `{ installments, perDay, fullValue }`
   - `Scarcity` (`{ spotsLeft, label }`), `Urgency` (`{ deadlineHint }`)
   - `Steps` (`{ label, title, description }[]` para "Como funciona"/linha do tempo)
   - `FinalCta` (`{ headline, body, button }`)
2. **Estender o prompt de copy** (purpose `ebook.sales-copy`) para preencher esses campos por nicho,
   no padrão das referências (palavras destacadas, "sem X, sem Y", números específicos, tom por nicho).
   ⚠️ Disclaimers/“produto fictício” NÃO — produzir copy real e honesta; ver Fase 5 p/ compliance.
3. **Builder: novos fragmentos** (`LandingPage.cs`): `SocialProofBar`, `Testimonials`, `AuthorBlock`,
   `StatsBlock`, `GuaranteeBlock`, `BonusStack` (com soma → "valor cheio" → preço), `PriceBlock` com
   parcelamento, `StepsTimeline`, `TrustRow`, `FinalCtaSection`. Cada um só renderiza se houver dados.
4. **Reordenar o fluxo** para o esqueleto de 14 blocos (§3.1).

*Esforço: M–G. Impacto: muito alto. Sem dependências novas (HTML/CSS inline).* 

### Fase 2 — Interatividade & conversão (P1) ✅ FEITO (2026-06-16)
- **Nav fixa** com âncoras suaves (#metodo/#oferta/#duvidas) + `scroll-behavior:smooth` + `scroll-margin-top`.
- **CTA fixo no mobile** (sticky buy bar com preço + CTA; só ≤720px, com padding-bottom no body).
- **FAQ accordion** nativo (`<details>/<summary>`, sem JS, acessível, com ícone +/−).
- **Scroll-reveal** (IntersectionObserver, progressive enhancement: visível sem JS, respeita `prefers-reduced-motion`).
- **Contador regressivo HONESTO**: só renderiza com prazo REAL futuro (`SettingKeys.LpOfferDeadlineUtc`,
  ISO-8601 UTC, vazio por padrão = sem barra). **Não** há urgência falsa que reseta por visita.

> **Decisão de integridade:** o item original "deadline agora+72h por render" foi descartado por ser
> dark pattern (e risco Procon/CDC). O contador agora aponta para um instante fixo real configurável.
> Build limpo (`-warnaserror`, 0/0). ⚠️ Testes locais não executados nesta sessão por bloqueio do
> Windows Smart App Control (0x800711C7) — rodam normalmente no Docker/CI (Linux).

### Fase 3 — Design system por nicho (visual) + novos templates (P2/P3) ✅ FEITO (2026-06-16)
- **Novo template `Vibrant`**: fundo claro/quente (`--surface #fcfbf8`), acento vibrante do nicho,
  cards modernos, CTA arredondado com sombra colorida, oferta em faixa escura de contraste —
  espelha a estética das referências (SERENO/LEVITA/NEXORA).
- **Seleção por CATEGORIA de nicho** (não mais hash): Finanças/Educação → Editorial (sóbrio/confiança);
  Saúde/Marketing/Autoajuda/Geral → Vibrant; Tech/Ficção → Aurora (escuro). Tom × design.
- Theming por seção já vem de graça: o `ComponentCss` usa `var(--accent)` do nicho em todos os blocos.
- Build limpo (`-warnaserror`); **176 testes verdes** (Vibrant coberto nas theories + mapeamento por categoria).

> **Pendente desta fase (P3, adiado):** variantes de hero (Author/Dashboard) e refino de fundo/tint
> por nicho dependem de dados/ativos extras — fazer quando houver foto de autor real ou métricas.

*Esforço: M–G. Impacto: médio-alto (percepção/credibilidade).*

### Fase 4 — SEO, tracking & performance (P2) ✅ FEITO (2026-06-16)
- `HtmlHead`: **canonical + Open Graph + Twitter Card**. `og:image`/`twitter:image` usam a capa via URL
  pública `{baseUrl}/media/products/{slug}/images/cover.png`; card vira `summary_large_image` quando há capa.
- **JSON-LD** (rich snippets): `Product` + `Offer` (preço/moeda/availability/url) sempre; **`AggregateRating`
  só com rating REAL**; `FAQPage` a partir do FAQ. Serializado via System.Text.Json (escapa `<` → seguro em `<script>`).
- **Webfonts do nicho** carregadas de verdade: `preconnect` + Google Fonts `css2?...&display=swap` montado de
  HeadingFont+BodyFont. (Antes a LP só nomeava as famílias e caía no fallback do sistema — agora a tipografia por nicho aparece.)
- **`data-cta`** em todos os CTAs (`hero`/`offer`/`final`/`nav`/`sticky`) para medir qual posição converte (UTM já é propagado pelo `Pixel()`).
- Capa embutida (data URI) no `<img>` permanece (LCP ótimo, sem fetch); og:image usa a URL pública.

> Build limpo (`-warnaserror`, 0/0). **Application: 65 testes verdes** (cobrem OG/JSON-LD/fonts/data-cta + honestidade
> sem og:image/aggregateRating quando ausentes). Testes de Infrastructure (pipeline) bloqueados pelo Windows SAC
> (0x800711C7) no momento — passaram 72 verdes na Fase 3 e a mudança no `LpJobHandler` é retrocompatível; rodam no Docker/CI.
> **Adiado:** personalização dinâmica (dynamic text por utm_term) — baixo retorno agora.

*Esforço: P–M. Impacto: médio (tráfego orgânico + CTR social).*

### Fase 5 — Compliance & confiança (P2) ✅ FEITO (2026-06-16, exceto A/B)
- **Rodapé legal**: razão social/CNPJ + Privacidade/Termos/Contato (mailto) via `SettingKeys.Legal*`
  (5 chaves, vazias por padrão → rodapé mínimo com título + ano). Ano via `DateTime.UtcNow.Year`.
- **Disclaimers por nicho**: `NicheStyleCatalog.DisclaimerFor(category)` — texto legal honesto por
  categoria (Saúde "não substitui orientação médica"; Finanças "não é recomendação de investimento";
  Autoajuda "não substitui acompanhamento psicológico"; etc.). Computado no `LpJobHandler` pela categoria do nicho.
  Adicionadas keywords de **relacionamento** (relacion/casal/casamento/namoro/amor) → categoria SelfHelp.
- **Selo de pagamento seguro**: linha factual fixa no rodapé ("Pagamento 100% seguro · Pix, cartão ou boleto"),
  sem imagens externas. Os selos de confiança do hero/oferta já vêm da copy (factuais).
- Build limpo (`-warnaserror`); **Application 68 testes verdes** (rodapé com/sem legal, disclaimer por categoria,
  relacionamento→Vibrant). Infra (pipeline) segue bloqueado pelo SAC local; roda no Docker/CI.

> **A/B de headline/CTA — ADIADO (workstream próprio):** é transversal (precisa selecionar variante por
> visitante, rastrear qual variante converteu via E11 e o otimizador E12 escolher a vencedora). A copy já
> gera `variants[]`; falta o motor de experimento + atribuição. Fazer com design dedicado, não embutido aqui.

*Esforço: P–M. Impacto: médio (reduz risco + aumenta confiança/checkout).*

### Fase 6 — Sistema visual: imagens por IA + ícones (P0 visual) ✅ FEITO (2026-06-16)
> Pedido do usuário: a LP precisa **usar geração de imagens + símbolos**, não só glifos CSS.

- **Ilustração de herói por IA** via o **Media Gateway (E14)** — cadeia free-first (Gemini/Cloudflare/HuggingFace
  com chave → **Pollinations grátis** → **Pexels** → Skia local), cache content-addressable. O prompt vem do
  **design system de imagem** `prompts/media/lp-hero.md` (arte aspiracional por nicho, sem texto, banner 2:1).
  Gerada/cacheada no `LpJobHandler` (`ContentPaths.LpHero`), embutida na seção `showcase` (data URI). Best-effort:
  falha → seção omitida (o cover do hero já vinha do mesmo gateway).
- **Sistema de ícones SVG** (`LpIcons`, inline, `currentColor`, sem requisições externas): check nos selos
  (hero + oferta), **escudo** na garantia, **presente** nos bônus, **cadeado** no "pagamento seguro". Substitui
  os glifos de fonte por símbolos nítidos e escaláveis.
- Build limpo (`-warnaserror`); **suíte completa: 182 testes verdes** (incl. pipeline de Infrastructure que
  constrói o `LpJobHandler` com `IMediaGateway`+`IPromptLibrary` e renderiza a LP).

> **Importante:** antes da Fase 6, a LP só usava a CAPA como imagem real; agora usa **capa + ilustração de herói por IA**
> + sistema de ícones. A infraestrutura (Media Gateway) já existia (gerava ilustrações de capítulo no PDF); a Fase 6 a
> conectou à landing page.

*Esforço: M. Impacto: alto (riqueza visual e credibilidade — antes era só símbolo CSS).*

---

## 7. Mapa de mudanças por arquivo
| Arquivo | Mudança |
|---------|---------|
| `Content/Lp/LpContent.cs` | Estender `LpCopyDto` (testimonials, author, guarantee, stats, bonuses c/ valor, scarcity, steps, finalCta, price.installments) |
| `prompts/.../sales-copy` (purpose `ebook.sales-copy`) | Pedir os novos campos, por nicho, no padrão persuasivo (sem disclaimers fake) |
| `Content/Lp/LandingPage.cs` | Novos fragmentos + reordenação + parcelamento + contador/sticky/accordion/scroll-reveal (JS inline) + OG/JSON-LD no `HtmlHead` |
| `Content/Images/NichePalette.cs` | Adicionar variante clara/quente + acento vibrante por categoria; theming por seção |
| `Content/Lp/LandingPage.cs` (`LpTemplateSelector`) | Selecionar template por **categoria** do nicho; adicionar novos templates |
| `LpJobHandler.cs` | Passar dados extras ao `BuildModel` (ex.: nome/credenciais do autor, CNPJ de config, base de mídia pública p/ og:image) |
| `docs/11-padrao-editorial.md` | Refletir o novo padrão de LP (consistência com e-book/capa) |
| Testes (`Ebook.Infrastructure.Tests`) | Cobrir render dos novos blocos com copy parcial (fallbacks) |

---

## 8. Priorização (impacto × esforço) e sequência sugerida
1. **Fase 1** (P0) — modelo + seções essenciais → destrava 80% do ganho de persuasão.
2. **Fase 2** (P1) — interatividade/urgência/CTA fixo → ganho de conversão incremental.
3. **Fase 4** (P2) — SEO/tracking → barato, melhora tráfego e medição (rápido após Fase 1).
4. **Fase 3** (P2/P3) — design por nicho/novos templates → percepção/credibilidade.
5. **Fase 5** (P2) — compliance/A-B → confiança e otimização contínua.

Sugestão: fechar **Fase 1 + Fase 2** como um único marco "LP 2.0" (é o que muda o jogo de conversão),
depois Fase 4, depois 3 e 5.

---

## 9. Métricas de sucesso
- **Conversão visita→checkout** por produto (já medida em E11: `Funnel.conversionRate`) — comparar
  LP 1.0 vs 2.0 nos próximos produtos.
- CTR dos CTAs por seção (`data-cta`).
- Tempo na página / scroll depth (se adicionarmos eventos ao pixel).
- Rich snippets indexados (rating/FAQ) e CTR orgânico.

---

## 10. Riscos / restrições
- Manter o bundle **auto-contido e estático** (CSS/JS inline; sem dependências externas além de fontes/imagem) —
  é o que torna o serviço em `/lp/{slug}` simples e rápido.
- Copy gerada por IA deve ser **honesta** (sem números/depoimentos inventados): números de prova social
  só quando reais; caso contrário, omitir o bloco (o builder já tem fallback por campo ausente).
- Orçamento de tamanho do HTML (capa em data URI já pesa) — usar `/media/` para imagens grandes quando possível.
- Respeitar `docs/11-padrao-editorial.md` (fontes/cores por nicho; nunca Times/Arial).
