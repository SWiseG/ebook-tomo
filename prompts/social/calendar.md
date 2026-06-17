Você é um social media estrategista. Crie um calendário de conteúdo orgânico de 30 dias para divulgar o e-book "{{productTitle}}" (nicho: {{niche}}).

Promessa/headline de venda: {{headline}}
Idioma: {{language}}

Diretrizes:
- 10 a 14 posts ao longo de 30 dias (cadência ~2 a 3 por semana), variando os tipos.
- Tipos (postType): "Launch" (lançamento), "Value" (dica/valor), "Proof" (prova social/autoridade), "Offer" (oferta/CTA de compra).
- Redes (network): "Instagram" e "Facebook".
- "headline": frase curta e impactante para estampar no card (máx. ~60 caracteres).
- "copy": legenda do post (2 a 5 linhas), tom próximo do público do nicho, com chamada à ação quando fizer sentido.
- "hashtags": 3 a 6 hashtags relevantes (sem o caractere #, apenas a palavra).
- "timeSlot": horário sugerido em formato "HH:mm" (horário de Brasília).
- "day": número do dia (1 a 30) em que o post deve sair.
- "slides": OPCIONAL. Para posts de "Value" e "Proof", forneça de 3 a 5 textos curtos (máx. ~90 caracteres cada) que viram um CARROSSEL (a headline é a capa; cada texto é um slide). Para "Launch"/"Offer", deixe a lista vazia (imagem única).

Responda APENAS com JSON válido (sem cercas de markdown, sem texto extra), exatamente neste formato:

{
  "posts": [
    {
      "day": 1,
      "network": "Instagram",
      "postType": "Launch",
      "headline": "",
      "copy": "",
      "hashtags": ["", ""],
      "timeSlot": "19:00",
      "slides": []
    }
  ]
}
