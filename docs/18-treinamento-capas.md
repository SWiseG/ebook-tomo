# 18 — Treinamento de capas (geração IA) e correções de layout

Rotina de treinamento: geramos capas reais para 3 nichos preset (finanças, saúde, tech) usando a
cadeia gratuita de imagem (Cloudflare Flux → HuggingFace → Pollinations) e analisamos os padrões do
gerador. Saída local em `training-covers/` (no `.gitignore`).

## Achado central

- **Fundo IA (sem texto): bom.** Os geradores gratuitos entregam cena editorial utilizável.
- **Capa INTEIRA pela IA (texto incluído): inviável no Flux gratuito.** Títulos/subtítulos saem
  ininteligíveis ("RNDa EXTR 30 Diáas"), selos embolados, blocos duplicados. → o caminho full-AI
  (`MediaKind.CoverWithText`) deve ficar **OFF** para provedores gratuitos; só liga com modelo pago
  capaz de tipografia (Nano Banana Pro / Imagen) **e** com o QA de visão (`ICoverQa`) ativo.
- **Capa do nosso pipeline (Skia sobre fundo IA): o caminho certo.** É o que ship.

## Correções aplicadas (SkiaImageComposer.RenderCover + cover-bg.md)

1. **Legibilidade do subtítulo** — antes em `accent` sobre foto clara (sumia). Agora `onDark` (branco)
   + drop shadow no título e subtítulo, e scrim do topo mais profundo (até `0.50h`, com piso de alfa
   no meio) cobrindo o bloco título+subtítulo inteiro.
2. **"Vazio" no meio (fundos abstratos/escuros, ex. tech)** — vignette radial suave dá profundidade e
   foca o centro; reforçado pelo prompt pedindo foco central.
3. **Quebra de título** — `AutoFitTitle`: reduz o corpo (132→78) até caber em ≤2 linhas e escolhe a
   quebra equilibrada (`BalanceTwoLines`), evitando linha órfã.
4. **Prompt composition-aware** (`prompts/media/cover-bg.md`) — topo escuro/limpo para o título,
   sujeito no centro/baixo, meio sem ser chapado.

## Como reproduzir

Harness temporário (não versionado) lê chaves de env (`CF_KEY`/`CF_ACCT`/`HF_KEY`) e grava em
`TRAIN_OUT`. Para cada nicho: gera fundo via `IMediaResolver` real → compõe com
`SkiaImageComposer.RenderCover` → salva PNG. Apagar o harness após a análise (faz rede; não é teste).

## Higgsfield

Integração `HiggsfieldImageResolver` pronta (Soul text2image, `platform.higgsfield.ai`, auth
`Key {id}:{secret}`, submit→poll→download). Bloqueada só por créditos. Com saldo, vira candidata a 1º
da cadeia para fundo (fotorrealismo lifestyle) e habilita SoulId (persona/autor recorrente por produto).
