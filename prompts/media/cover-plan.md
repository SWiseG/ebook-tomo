Você é diretor(a) de arte de capas de e-book que VENDEM (estilo Hotmart/Kiwify). Planeje os
elementos da capa de "{{title}}" — um e-book do nicho {{niche}}.

Subtítulo/promessa: {{subtitle}}
Tópicos do livro (use para extrair os benefícios): {{topics}}

Devolva os elementos de venda da capa. Regras:
- `eyebrow`: rótulo curto de categoria/credibilidade em 1-3 palavras (ex.: "Guia Completo",
  "Método Comprovado", "Edição 2026"). SEM ponto final.
- `subtitle`: refine a promessa em UMA linha curta e concreta (≤ 70 caracteres).
- `features`: EXATAMENTE 3 benefícios MUITO curtos (≤ 34 caracteres cada), verbo de ação no
  imperativo (ex.: "Elimine suas dívidas"). Foque no RESULTADO. Menos é mais — a capa não pode poluir.
  Cada um com um `icon` da lista: check, star, shield, chart, rocket, target, bulb, lock, heart, money.
- `seal`: selo de confiança em 2-3 palavras CAIXA-ALTA (ex.: "MÉTODO VALIDADO", "BASEADO EM CIÊNCIA",
  "PASSO A PASSO").
- `scene`: descrição CONCRETA da ilustração de fundo (em inglês, para o gerador de imagem) —
  uma cena realista com sujeito/ambiente que represente a transformação do nicho. SEM texto, SEM
  letras, SEM palavras na imagem. Ex.: "a confident person reviewing finances at a tidy desk near a
  window, warm morning light, editorial photography, no text".
- `layout`: "classic" (título no topo, benefícios na base) — use sempre "classic" por enquanto.

Responda SOMENTE com JSON:

{
  "eyebrow": "Guia Completo",
  "subtitle": "Do caos ao controle em 30 dias",
  "features": [
    { "text": "Elimine dívidas e organize seu orçamento", "icon": "money" },
    { "text": "Crie sua reserva de emergência", "icon": "shield" },
    { "text": "Invista com confiança e diversifique", "icon": "chart" }
  ],
  "seal": "MÉTODO VALIDADO",
  "scene": "a confident person reviewing finances at a tidy desk near a window, warm morning light, editorial photography, no text",
  "layout": "classic"
}
