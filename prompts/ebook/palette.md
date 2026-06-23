Você é diretor(a) de arte editorial. Defina a IDENTIDADE VISUAL de um e-book — a MESMA paleta
será usada na capa, no PDF e na landing page, então precisa ser coerente e profissional.

Nicho: {{niche}}
Título: "{{title}}"
Categoria editorial: {{category}}

Base de referência da categoria (ponto de partida — você DEVE variar para dar identidade própria,
mantendo a psicologia de cor do nicho): fundo {{baseBackground}}, destaque {{baseAccent}}.

Regras:
- Cores em HEX (#RRGGBB). Aplique regra 60-30-10: `background` é a cor dominante (fundo escuro e
  rico, para o título claro ter contraste na capa); `accent` é o destaque (~10%, vibrante, contrasta
  com o fundo); `onDark` é um quase-branco legível sobre o fundo.
- Respeite a PSICOLOGIA da categoria (ex.: finanças → azul/verde de confiança + dourado; saúde →
  verdes; marketing → grafite + laranja/vermelho energético; autoajuda → terrosos quentes).
- Garanta contraste AA entre `onDark` e `background`, e entre `accent` e `background`.
- Fontes: escolha APENAS desta lista exata (case-sensitive):
  Inter, Manrope, Lora, Merriweather, Fraunces, Playfair Display, Anton, Archivo Black, Bebas Neue,
  Fjalla One, Barlow Condensed.
  - `displayFont`: fonte de IMPACTO da capa — display/condensada/black (ex.: Anton, Archivo Black,
    Bebas Neue, Fjalla One, Barlow Condensed, ou Playfair Display para nichos elegantes).
  - `headingFont`: títulos do PDF — refinada e legível (ex.: Manrope, Fraunces, Merriweather, Lora).
  - `bodyFont`: corpo de leitura — confortável (ex.: Inter, Lora, Merriweather).
  - displayFont ≠ bodyFont. Combine display+body com bom contraste tipográfico.

Responda SOMENTE com JSON, sem texto fora dele:

{
  "background": "#0E2A47",
  "accent": "#E0B978",
  "onDark": "#F5F8FC",
  "headingFont": "Manrope",
  "bodyFont": "Merriweather",
  "displayFont": "Archivo Black"
}
