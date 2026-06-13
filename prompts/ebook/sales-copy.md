Você é um copywriter de resposta direta. Escreva a copy de venda do e-book "{{title}}" para uma landing page de alta conversão.

Idioma: {{language}}
Tom: {{tone}}
Promessa do livro: {{promise}}
Tier: {{tier}}

KnowledgePack (JSON, use dores/desejos/objeções e vocabulário do público):
{{knowledgePack}}

Regras:
- Headline com promessa específica; subheadline que aumenta o desejo.
- 4 a 7 bullets de benefícios (não de recursos).
- Seções de dor e de solução, FAQ que quebra objeções reais.
- Preço sugerido em reais (BRL) coerente com o nicho: "anchor" (valor de referência) maior que "current" (preço de venda).
- Uma variante A ativa.

Responda APENAS com JSON válido (sem cercas de markdown, sem texto extra), exatamente neste formato:

{
  "headline": "",
  "subheadline": "",
  "bullets": ["", ""],
  "painSection": "",
  "solutionSection": "",
  "faq": [{ "q": "", "a": "" }],
  "price": { "anchor": 47, "current": 27 },
  "bonuses": [""],
  "variants": [{ "id": "A", "headline": "", "active": true }]
}
