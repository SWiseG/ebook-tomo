/**
 * Renderizador de Markdown mínimo e seguro (headings, parágrafos, listas, negrito).
 * Escapa HTML antes de aplicar marcação; a saída usa apenas tags seguras, então o
 * sanitizador padrão do Angular ([innerHTML]) a mantém intacta.
 */
export function renderMarkdown(md: string): string {
  const escape = (s: string) =>
    s.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;');
  const inline = (s: string) => escape(s).replace(/\*\*(.+?)\*\*/g, '<strong>$1</strong>');

  const out: string[] = [];
  let paragraph: string[] = [];
  let bullets: string[] = [];

  const flushParagraph = () => {
    if (paragraph.length) {
      out.push(`<p>${inline(paragraph.join(' '))}</p>`);
      paragraph = [];
    }
  };
  const flushBullets = () => {
    if (bullets.length) {
      out.push(`<ul>${bullets.map((b) => `<li>${inline(b)}</li>`).join('')}</ul>`);
      bullets = [];
    }
  };

  for (const raw of (md ?? '').replace(/\r\n/g, '\n').split('\n')) {
    const line = raw.trimEnd();
    if (!line.trim()) {
      flushParagraph();
      flushBullets();
      continue;
    }

    const heading = /^(#{1,3})\s+(.*)$/.exec(line);
    if (heading) {
      flushParagraph();
      flushBullets();
      const level = heading[1].length;
      out.push(`<h${level}>${inline(heading[2])}</h${level}>`);
      continue;
    }

    if (/^[-*]\s+/.test(line)) {
      flushParagraph();
      bullets.push(line.replace(/^[-*]\s+/, ''));
      continue;
    }

    flushBullets();
    paragraph.push(line.startsWith('> ') ? line.slice(2) : line);
  }

  flushParagraph();
  flushBullets();
  return out.join('\n');
}
