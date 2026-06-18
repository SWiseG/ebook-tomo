# 12 — PDF mais bonito + repositórios de imagens e SVGs

> Pesquisa aplicada: como deixar o PDF dramaticamente mais bonito com a nossa stack (QuestPDF + SkiaSharp) e quais repositórios gratuitos de imagens/SVGs/ícones podemos **consumir programaticamente**. Complementa [11-padrao-editorial.md](11-padrao-editorial.md) (o padrão) e [10-geracao-ia-midia.md](10-geracao-ia-midia.md) (Media Gateway). Ferramentas por segmento ao final.

## Já implementado nesta leva (2026-06-18)
- **SVG vetorial no PDF**: `IconRegistry` (Infra/Content) carrega ícones Lucide (ISC) de `src/Ebook.Api/assets/icons/`, recolore `currentColor` pela cor do nicho e injeta nas **caixas de destaque** (Insight → `lightbulb`, Estudo de caso → `trending-up`) via `IContainer.Svg(...)`. 157 testes OK.

---

## Parte 1 — Tornar o PDF mais bonito (QuestPDF, recursos confirmados)

QuestPDF (2026.6.0) é muito mais capaz do que usamos hoje. Recursos que destravam beleza editorial:

### 1. SVG vetorial — `IContainer.Svg()` ✅ (já usando)
- `container.Svg(svgText)` ou `container.Svg(path)`; preload com `SvgImage.FromFile(path)` para reuso.
- Dimensionar: `container.Width(16).Svg(...).FitArea()`.
- **Recolorir**: SVGs Lucide usam `stroke="currentColor"` → substituir por hex do nicho antes de renderizar (feito no `IconRegistry`).
- **Próximos usos**: divisores de seção vetoriais, ícone por capítulo no sumário e na abertura, ornamentos de capa, selos ("bônus", "garantia").
- Melhorias 2025 do QuestPDF: strokes finos não viram hairline, base64 via `href`, cores `#RGBA`/`transparent`.
- Limitação: texto dentro de SVG depende de fonte; ícones (sem texto) renderizam sem problema.
- Fonte: [SVG Support | QuestPDF](https://www.questpdf.com/api-reference/image/svg.html)

### 2. Layers — fundo + conteúdo + marca d'água
- `.Layers(l => { l.Layer()…(fundo); l.PrimaryLayer()…(conteúdo); l.Layer()…(overlay) })`.
- **Aberturas de capítulo decorativas**: número gigante translúcido no fundo + título por cima (padrão Z).
- **Marca d'água** discreta da marca; **capa** full-bleed (imagem no fundo + título sobreposto).
- Fonte: [Layers | QuestPDF](https://www.questpdf.com/api-reference/layers.html)

### 3. Fundos e gradientes
- `Background(hex)` e `BackgroundLinearGradient(ângulo, cores)` → seções tonais (60-30-10), páginas de oferta/CTA com gradiente do nicho. Já usamos `Background` nas caixas; faltam gradientes nas aberturas/CTA.

### 4. Decoration (cabeçalho/rodapé persistentes)
- `Decoration` repete header/footer com paginação — já temos header/rodapé simples; dá para evoluir (título corrente + nº de capítulo + linha accent).

### 5. MultiColumn (colunas estilo revista)
- Fluxo em colunas para seções específicas (ex.: FAQ, glossário) → ar editorial.

### 6. Drop cap (capitular) — emulável
- QuestPDF não tem capitular nativa; emular com `Row`: 1ª letra grande (ex.: 48pt, cor do nicho, fonte display) em `ConstantItem` + restante do parágrafo em `RelativeItem`. Aplicar no 1º parágrafo de cada capítulo (regra dos 3 segundos).

### 7. Página de abertura de capítulo (recomendado)
- Página dedicada por capítulo: número grande (display), título, ícone do nicho (SVG), linha accent — antes do corpo. Eleva drasticamente a percepção de qualidade.

> **Prioridade de impacto/esforço:** (a) ícones SVG ✅ → (b) drop cap + abertura de capítulo decorativa (Layers+SVG+gradiente) → (c) divisores de seção SVG → (d) imagens no corpo (Frente D, via E14).

---

## Parte 2 — Repositórios de IMAGENS (fotos) por API

| Fonte | Limite free | Licença | Nota |
|---|---|---|---|
| **Pexels** | 200/h · 20.000/mês | uso comercial, sem atribuição | **primário** (já temos `PexelsPhotoProvider`); fotos + vídeos |
| **Unsplash** | 50/h | uso comercial, sem atribuição | maior qualidade editorial |
| **Pixabay** | generoso (free) | uso comercial | fotos + **vetores** + ilustrações |
| Openverse / Wikimedia | aberto | CC/dominio público | histórico, científico |
| NASA / Met Museum / Smithsonian | aberto | dominio público / CC0 | ciência, arte |

- **Estratégia**: manter Pexels como provider primário; adicionar Unsplash e Pixabay como fallback (mais cota agregada). As `queries` por seção/nicho vêm do prompt (Frente B já prevê) e dos *photo seeds* do `NicheStyleCatalog` (Parte 4).
- Fontes: [Pexels API](https://www.pexels.com/api/documentation/) · [Pixabay API](https://pixabay.com/api/docs/) · comparativo [Free Image APIs 2026](https://blog.laozhang.ai/en/posts/free-image-api)

---

## Parte 3 — Repositórios de SVG/ÍCONES/ILUSTRAÇÕES consumíveis

### Ícones (vetor, perfeitos para PDF — via CDN estático)
| Set | URL estática (jsDelivr/unpkg) | Licença |
|---|---|---|
| **Lucide** ✅ | `cdn.jsdelivr.net/npm/lucide-static@latest/icons/[nome].svg` | ISC |
| **Tabler** | `cdn.jsdelivr.net/npm/@tabler/icons@latest/icons/outline/[nome].svg` | MIT |
| **Phosphor** | `cdn.jsdelivr.net/npm/@phosphor-icons/core@latest/assets/regular/[nome].svg` | MIT |
| **Heroicons** | `cdn.jsdelivr.net/npm/heroicons@latest/24/outline/[nome].svg` | MIT |

- Padrão: usam `currentColor` → recolorir por nicho (como o `IconRegistry`).

### Ilustrações (cenas — corpo do e-book / capa)
| Fonte | Licença | Consumo |
|---|---|---|
| **unDraw** | MIT, sem atribuição | sem API oficial; baixar SVG por tema e curar/bundle |
| **Humaaans** | CC0 | figuras humanas modulares (diversidade) |
| **Open Peeps** | CC0 | personagens desenhados à mão |
| **SVGRepo** | maioria CC0 (300k+) | enorme catálogo de ícones+ilustrações |
| Storyset | atribuição exigida | evitar no automático (exige crédito) |

- Fontes: [unDraw](https://undraw.co/) · [Lucide static](https://lucide.dev/guide/static/) · [CC0 illustrations](https://allsvgicons.com/cc0-illustrations/)

### Estratégia de consumo (2 modos, espelhando fontes)
1. **Bundle curado** (como fizemos com fontes e ícones): conjunto pequeno por nicho versionado em `assets/`. Determinístico, offline, zero risco em runtime. **Recomendado para ícones e um pacote base de ilustrações.**
2. **Fetch + cache** via o **Media Gateway (E14)**: um `IIllustrationProvider`/`IIconProvider` baixa do CDN sob demanda e cacheia (content-addressable, igual ao cache de mídia). Útil para variedade sem inchar o repo.

---

## Parte 4 — Ferramentas por SEGMENTO (e-book mais inteligente)

O `NicheStyleCatalog` já mapeia **cor + fonte** por nicho. Estender para um **perfil visual completo por segmento**:

```
NicheVisualProfile(NicheCategory) → {
  Palette (cores emocionais + fontes)        ✅ existe
  IconSet  (nomes Lucide/Tabler temáticos)   → ex.: Finanças: trending-up, piggy-bank, coins, target
  IllustrationStyle (flat corporate / humano / minimal)
  PhotoSeeds (queries Pexels/Unsplash)       → ex.: Saúde: "healthy food", "running sunrise"
  ChapterIcon default                         → ícone de abertura de capítulo
}
```

- **Finanças**: ícones de gráfico/moeda; foto "office/city/calculadora"; ilustração corporate flat; tom azul/dourado.
- **Saúde/Bem-estar**: ícones folha/coração/maçã; foto "natureza/treino/comida saudável"; ilustração humana calorosa; verde.
- **Marketing**: ícones megafone/foguete/alvo; foto "notebook/equipe"; ilustração bold; charcoal/laranja.
- **Self-help**: ícones bússola/montanha/lâmpada; foto "amanhecer/caminhada"; ilustração humana; terracota.
- **Tech**: ícones cpu/código/rede; foto "abstrato/circuito"; ilustração geométrica; índigo.

Essas seleções alimentam: abertura de capítulo (ícone), caixas (ícone), corpo (ilustração/foto), capa (estilo), LP e social — **identidade coerente por segmento, ponta a ponta**.

---

## Roadmap de aplicação (próximos passos sugeridos)
1. **Drop cap + abertura de capítulo decorativa** (Layers + SVG do nicho + gradiente) — maior salto percebido, só QuestPDF.
2. **Divisores de seção SVG** + ícone no sumário por capítulo.
3. **`NicheVisualProfile`**: estender o catálogo com IconSet/PhotoSeeds/IllustrationStyle por nicho.
4. **Imagens no corpo (Frente D)** via Media Gateway (E14): fotos (Pexels/Unsplash/Pixabay) + ilustrações (bundle unDraw/Humaaans) + geração IA, 1 a cada 2–3 páginas.
5. **Fetch+cache de ícones/ilustrações** como provider do E14 (variedade sem inchar o repo).
