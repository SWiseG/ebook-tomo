Defina a DIREÇÃO DE ARTE das imagens de um e-book — a MESMA será usada na capa, no PDF e na landing
page, então precisa ser coerente.

Nicho: {{niche}}
Título: "{{title}}"
Categoria: {{category}}

Devolva SOMENTE JSON, em INGLÊS (vai para geradores de imagem):
- "mood": 2-4 adjetivos de emoção/tom (ex.: "confident, trustworthy, calm").
- "imageStyle": estilo fotográfico/ilustrativo (ex.: "clean documentary photography, natural light").
- "subjectGuidance": SUJEITOS/cenas (docs/16 §8) — pessoa REAL com emoção autêntica (não modelo
  sorrindo pro lado), contexto familiar (casa, não estúdio), luz natural quente, estilo "lifestyle
  photography" (não "studio shot"), resultado tangível do nicho; sem texto/logos. Específico ao avatar.

{
  "mood": "confident, trustworthy, calm",
  "imageStyle": "clean documentary photography, natural light",
  "subjectGuidance": "real people managing money, tidy desks, notebooks"
}

Estilo APRENDIDO do nicho (realimente se houver; ignore se "(nenhum)"): {{learned}}
