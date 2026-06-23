# docs/14 — Sistema de Capas 2.0 (geração por IA)

> Status: **em implementação**. Decisões tomadas (2026-06-22): capa **2:3 / 1600×2400**; capa
> **inteira gerada por IA** (com texto) como caminho primário + fallback Skia determinístico;
> **embarcar fontes display/condensadas**; **paleta por produto gerada por IA** (compartilhada por
> PDF, LP e capa). Complementa o docs/13 (design system do PDF).

## 1. Diagnóstico: onde estamos vs. os exemplos

**Capa atual** (`SkiaImageComposer.RenderCover`): fundo (foto Pexels escurecida a 205-alpha — quase
opaca, mata a imagem) **ou** gradiente chapado + faixa accent + título + subtítulo + marca. Layout
único, alinhado à esquerda, 1 fonte, 1 tamanho.

**Exemplos (`docs/cover-examples/`, feitos no Gamma)** têm 6 camadas que não temos:

| Camada | Exemplos | Hoje |
|---|---|---|
| Fundo ilustrado com **cena + sujeito humano** | ✅ | ❌ foto escurecida/gradiente |
| **Tipografia display** (condensada, black, hierarquia eyebrow→título→sub) | ✅ | ❌ 1 fonte/peso |
| **Caixas de benefício** com ícones | ✅ 3-4 por capa | ❌ |
| **Selos de confiança** ("CURSO COMPLETO", "CIÊNCIA BASEADA EM EVIDÊNCIAS") | ✅ | ❌ |
| **Bolhas de estatística** ("95% Mobile", "240M+") | ✅ | ❌ |
| **Rodapé de autor/copyright** | ✅ | ❌ marca solta |

Achados de código:
- Existe `prompts/media/cover.md` (fundo abstrato, sem texto) **que nunca é usado** — o
  `CoverJobHandler` chama `RenderCover` direto com foto Pexels.
- Só **6 fontes** embarcadas (Inter, Manrope, Lora, Merriweather, Fraunces, Playfair), regular/bold
  — nenhuma display/condensada.
- Paleta é **1 fixa por categoria** (8 no total), keyada por **nicho** — sem variação por produto.
- `NichePalette` (background/accent/onDark/heading/body) já é a fonte compartilhada por PDF + LP +
  capa (via `PaletteConfig`). A coerência pedida já tem o "encaixe" — falta gerar variedade.

## 2. Arquitetura alvo

```
[1] IA Paleta por produto ──► PaletteConfig(produto)  ─┐ (compartilhado)
                                                        ├─► PDF, LP e Capa leem a MESMA paleta
[2] IA Plano de capa  ──► spec estruturada (layout,    │
     copy de benefícios, selos, cena, fontes)          │
                                                        ▼
[3] Geração da capa:
     PRIMÁRIO  → IA gera a capa INTEIRA com texto (modelo capaz de tipografia)
     FALLBACK  → IA ilustra o fundo (sem texto) + Skia compõe título/caixas/selos
                                                        ▼
[4] IA Visão (Claude) confere legibilidade do título → se ruim, cai no fallback
                                                        ▼
[5] Resize p/ 1600×2400 (2:3) + deriva banner 300×250 (Kiwify) + mockup 3D
```

Modelos de imagem erram texto. Como a capa inteira é por IA, o caminho primário roteia para um
**modelo capaz de texto** (Gemini/Imagen na cadeia atual; Flux-text/Ideogram se adicionarmos), com o
prompt carregando **título exato entre aspas + hexes da paleta + estilo de fonte**. O **fallback
determinístico** (Skia) é inegociável numa plataforma autônoma. O passo [4] (visão da IA) valida.

## 3. Pacotes de trabalho

| WP | Entrega | Toca |
|---|---|---|
| **WP-1** | `IPaletteResolver` unificado **por produto** (produto → nicho → catálogo). | PdfJobHandler, LpJobHandler, CoverJobHandler, GenerateTestLp |
| **WP-2** | IA de paleta: prompt `ebook/palette.md` → JSON `NichePalette` por produto. | nova etapa no Review/Cover |
| **WP-3** | Embarcar ~5 fontes display/condensadas; ampliar pares por nicho. | assets, NicheStyleCatalog |
| **WP-4** | IA diretor de capa: prompt `media/cover-plan.md` → spec (layout, copy, selo, cena). | novo `CoverPlanDto` |
| **WP-5** | Caminho full-AI: builder de prompt → roteamento p/ modelo de texto → imagem 2:3 + proveniência. | MediaGateway, MediaBrief |
| **WP-6** | Skia 2.0 (fallback + rede de segurança): caixas, selos, bolhas, scrims, 2-3 layouts. | SkiaImageComposer |
| **WP-7** | Dimensões **1600×2400**; derivar banner **300×250** (vitrine Kiwify) + ajustar mockup. | SkiaImageComposer, CoverJobHandler |
| **WP-8** | IA de **visão** confere o título; reprovou → fallback. Wire + testes. | CoverJobHandler, ClaudeCliClient |

Variedade por design: paleta por produto × fontes ampliadas × layout pela IA × cena pela IA.

## 4. Sequência de implementação

1. **WP-1 + WP-7** (fundação): paleta unificada por produto + dimensões 2:3.
2. **WP-3** (fontes): variedade tipográfica imediata (PDF/LP/capa).
3. **WP-2** (paleta IA): variedade de cor coerente nos 3 artefatos.
4. **WP-6** (Skia 2.0): eleva a capa mesmo sem full-AI — vira o fallback seguro.
5. **WP-4 + WP-5** (full-AI): o salto dos exemplos Gamma.
6. **WP-8** (visão/QA): fecha o loop de qualidade autônoma.

## 5. Riscos & mitigação

- **Texto embaralhado no full-AI** → modelo de texto + fallback Skia + QA por visão (WP-8).
- **Sem chave do modelo de texto** → cai no Skia 2.0 (capa forte sozinha).
- **Paleta por produto** muda 4 resolvers → unificar em `IPaletteResolver` primeiro (WP-1).
- **+2-4 MB de TTF** no Docker → aceitável; fontes lazy no FontRegistry.
- **Cache de imagem** por `SHA256(purpose+prompt+wh)` → mudar prompt/dimensão = cache miss limpo.

## 6. Progresso — ✅ TODOS OS WPs CONCLUÍDOS

- [x] **WP-1** — `IPaletteResolver` por produto (produto→nicho→catálogo); PDF, LP, capa e LP Lab unificados. 4 testes.
- [x] **WP-2** — `PaletteDirector` (`ebook/palette.md`): paleta por produto via IA, validada (cores #RRGGBB + fontes do set) e persistida; cai no catálogo na falha. 3 testes.
- [x] **WP-3** — 6 fontes display embarcadas (Anton, Archivo Black, Bebas Neue, Fjalla One, Barlow Condensed ×2); `NichePalette.DisplayFont` + `Display`; capa/cards usam display. Verificado visualmente.
- [x] **WP-4** — `CoverDirector` (`media/cover-plan.md`): eyebrow, subtítulo, 3-4 benefícios+ícone, selo e a CENA do fundo. Wired no `CoverJobHandler`. 2 testes.
- [x] **WP-5** — caminho full-AI (`media/cover-full.md` + `MediaKind.CoverWithText` que exclui bancos e prefere Gemini + `FitCover` 1600×2400). **Gated** por `cover.aiFullCover` (default OFF). 1 teste de roteamento.
- [x] **WP-6** — Skia 2.0: scrims de gradiente (ilustração visível), eyebrow tracked, título display, **caixas de benefício** (disco accent + check desenhado), **selo circular**, rodapé de autor. Verificado visualmente (gradiente E sobre foto).
- [x] **WP-7** — capa **1600×2400 (2:3)**. (Banner 300×250 da vitrine Kiwify = upload manual já existente; opcional, adiar.)
- [x] **WP-8** — `ICoverQa` + `ClaudeVisionCoverQa` (Claude CLI `--allowedTools Read`, `media/cover-qa.md`): confere título legível/correto na capa full-AI; reprovou → composição Skia. 1 teste de veredito.

## 7. Fluxo final no `CoverJobHandler`

```
1. PaletteDirector.Ensure  → paleta por IA persistida (WP-2) — PDF/LP/capa herdam
2. CoverDirector.Plan      → eyebrow/benefícios/selo/cena (WP-4)
3. ResolveBackground       → ilustração da cena (Media Gateway free-first) ou foto de banco
4. TryFullAiCover (gated)  → capa inteira por IA (WP-5) → QA de visão (WP-8) → aceita ou…
5. …fallback Skia 2.0      → composição rica determinística (WP-6), SEMPRE legível
```

**Default seguro:** com `cover.aiFullCover` OFF (ou sem modelo de texto), a saída é a composição
Skia 2.0 — já no nível dos exemplos. Ligue o full-AI em produção (Railway) com Gemini configurado;
o QA de visão protege contra texto embaralhado, caindo no Skia quando reprova.

> **Não verificável localmente:** o caminho full-AI (WP-5) e o QA de visão (WP-8) exigem modelo
> generativo de texto + Claude CLI reais (produção). A composição Skia (WP-6), a paleta e o plano por
> IA foram verificados (render visual + testes). 212 testes no total.
