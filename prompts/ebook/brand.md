Defina a DIREÇÃO DE ARTE das imagens de um e-book — a MESMA será usada na capa, no PDF e na landing
page, então precisa ser coerente.

Nicho: {{niche}}
Título: "{{title}}"
Categoria: {{category}}

Devolva SOMENTE JSON, em INGLÊS (vai para geradores de imagem):
- "mood": 2-4 adjetivos de emoção/tom (ex.: "confident, trustworthy, calm").
- "imageStyle": estilo fotográfico/ilustrativo (ex.: "clean documentary photography, natural light").
- "subjectGuidance": que SUJEITOS/cenas aparecem (pessoas em ação, objetos, ambiente do nicho),
  realista e específico; sem texto/logos.

{
  "mood": "confident, trustworthy, calm",
  "imageStyle": "clean documentary photography, natural light",
  "subjectGuidance": "real people managing money, tidy desks, notebooks"
}
