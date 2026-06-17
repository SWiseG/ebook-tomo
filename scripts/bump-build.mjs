// Gera/incrementa o número de build para produção.
// Formato: AAAA.MM.DD.NN — NN é um contador global que começa em 01 e incrementa a cada build.
// Rode em toda publicação para prd (ex.: `node scripts/bump-build.mjs`) e commite os build.txt.
// Escreve em dois lugares: raiz (fonte da verdade) e public/ do painel (servido em /build.txt).
import { readFileSync, writeFileSync, existsSync, mkdirSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { dirname, join } from 'node:path';

const root = join(dirname(fileURLToPath(import.meta.url)), '..');
const rootFile = join(root, 'build.txt');
const publicFile = join(root, 'src', 'Ebook.Admin', 'public', 'build.txt');

let next = 1;
if (existsSync(rootFile)) {
  const lastSeg = readFileSync(rootFile, 'utf8').trim().split('.').pop();
  const n = Number.parseInt(lastSeg ?? '', 10);
  if (Number.isFinite(n)) {
    next = n + 1;
  }
}

const now = new Date();
const y = now.getFullYear();
const m = String(now.getMonth() + 1).padStart(2, '0');
const d = String(now.getDate()).padStart(2, '0');
const nn = String(next).padStart(2, '0');
const version = `${y}.${m}.${d}.${nn}`;

writeFileSync(rootFile, version + '\n');
mkdirSync(dirname(publicFile), { recursive: true });
writeFileSync(publicFile, version + '\n');
console.log('build:', version);
