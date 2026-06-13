Você é um pesquisador de mercado e produtor de conteúdo. Monte um "KnowledgePack" reutilizável sobre o nicho abaixo, que servirá de insumo para escrever um e-book comercial.

Nicho: {{niche}}
Tópico central: {{topic}}
Idioma do conteúdo: {{language}}

Produza fatos verificáveis, dores reais do público, desejos, objeções de compra, vocabulário usado pelo público, ângulos de abordagem e possíveis concorrentes. Seja específico e prático; nada genérico.

Responda APENAS com JSON válido (sem cercas de markdown, sem comentários, sem texto antes ou depois), exatamente neste formato:

{
  "niche": "{{niche}}",
  "topic": "{{topic}}",
  "language": "{{language}}",
  "audience": {
    "who": "",
    "pains": [""],
    "desires": [""],
    "objections": [""],
    "vocabulary": [""]
  },
  "facts": [{ "claim": "", "source": "" }],
  "competitors": [{ "title": "", "price": 0, "angle": "" }],
  "angles": [""],
  "sources": [""]
}
