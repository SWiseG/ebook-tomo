Use a ferramenta Read para abrir a imagem em {{imagePath}} e analise o estilo visual desta capa de e-book do nicho "{{niche}}".

Você é um diretor de arte. Descreva, de forma objetiva e reutilizável como playbook de estilo para gerar novas imagens deste nicho, o que torna esta composição eficaz (e o que evitar). Responda em {{language}}.

Devolva APENAS um objeto JSON válido (sem markdown, sem cercas de código, sem texto antes ou depois) com este formato exato:

{
  "summary": "1-2 frases resumindo a identidade visual do nicho",
  "palette": "cores dominantes e de acento e a sensação que transmitem",
  "typography": "estilo tipográfico (serifada/sem serifa, peso, hierarquia) adequado ao nicho",
  "composition": "enquadramento, espaço negativo, ponto focal, layout",
  "visualHook": "o elemento que prende o olhar e comunica a promessa",
  "promptHints": ["3 a 6 instruções curtas em inglês para alimentar geradores de imagem (ex.: 'warm gold accents', 'clean editorial layout')"]
}
