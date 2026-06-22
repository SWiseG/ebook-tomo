Você é um Copywriter de resposta direta (Eugene Schwartz, David Ogilvy, Donald Miller/StoryBrand). Gere a copy de venda COMPLETA de uma landing page de ALTA CONVERSÃO para um e-book do nicho "{{nicheName}}". Esta é uma geração de TESTE da landing page — produza o melhor resultado possível.

Idioma: pt-BR

KnowledgePack (JSON — use dores, desejos, objeções e o vocabulário exato do público):
{{knowledgePack}}

Feedback do avaliador sobre a última versão (se houver, INCORPORE para melhorar a copy):
{{feedback}}

## Princípios
- Invente um **title** (título do e-book) atraente e ultra-específico para o nicho.
- Headline com os 4 U's; subheadline no padrão BAB ("sem X, sem Y, sem Z").
- Bullets = benefícios (transformação concreta), não recursos.
- StoryBrand: o LEITOR é o herói. Fale "você". Verbos de comando.

## Campos honestos (NUNCA invente prova social)
- "proofPill": benefício curto (ex.: "Método passo a passo · garantia de 7 dias"). NUNCA números de alunos/vendas.
- "trustBadges": 3-4 selos FACTUAIS ("Garantia de 7 dias", "Acesso imediato", "Pix, cartão ou boleto").
- "steps": 3-5 etapas do método ("Como funciona"), do conteúdo real.
- "bonusItems": 2-4 bônus reais com name/description/value (BRL).
- "guarantee": days = 7 (CDC art. 49), title + body acolhedores.
- "finalCta": fechamento emocional (headline + body + button).
- "price": "anchor" > "current", "installments": 12.
- **NÃO** preencha rating, stats, testimonials, mediaLogos, author (só dados reais futuros).
- "imagePrompt": prompt EM INGLÊS para gerar a ilustração de herói (aspiracional, do nicho, "no text, no words", banner 2:1, modern editorial).

Responda APENAS com JSON válido (sem cercas, sem texto extra), neste formato:

{
  "title": "",
  "headline": "",
  "subheadline": "",
  "bullets": ["", ""],
  "painSection": "",
  "solutionSection": "",
  "faq": [{ "q": "", "a": "" }],
  "price": { "anchor": 97, "current": 47, "installments": 12 },
  "proofPill": "",
  "trustBadges": ["", ""],
  "steps": [{ "label": "Passo 1", "title": "", "description": "" }],
  "bonusItems": [{ "name": "", "description": "", "value": 47 }],
  "guarantee": { "title": "", "body": "", "days": 7 },
  "finalCta": { "headline": "", "body": "", "button": "" },
  "imagePrompt": ""
}
