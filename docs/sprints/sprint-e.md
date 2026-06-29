# Sprint E — Limpeza & otimização (deletar, consolidar, consertar)

> **Meta da sprint:** reduzir entropia antes de empilhar as Sprints A–D. Esta é uma avaliação honesta do estado
> atual (auditada no código em 2026-06-28). **Conclusão geral: o código está limpo** — quase nenhum `TODO/FIXME/HACK`,
> arquitetura coerente. A dívida real é **documentação desatualizada** e **algumas camadas que pedem consolidação**.
> Não há "monte de código morto" para deletar; cuidado com a tentação de remover o que parece redundante mas tem papel.
>
> **Regra de ouro desta sprint:** antes de deletar/reescrever qualquer coisa, **abra o arquivo e confirme** que ele
> faz o que o nome sugere. Se o que você encontrar contradisser esta avaliação, atualize este doc em vez de seguir cego.

---

## Categoria 1 — Doc drift (corrigir já; esforço **low**, risco **nenhum**)

A documentação divergiu do código. Isso confunde tanto humanos quanto o Claude do futuro (eu já caí nisso ao
escrever o docs/19 a partir do docs/11). Corrigir é barato e evita decisões erradas.

| # | Problema | Evidência | Ação |
|---|---|---|---|
| E1 | **Dois docs numerados 12** | `docs/12-pdf-recursos-visuais.md` **e** `docs/12-landing-page-roadmap.md` | Renumerar um deles (ex.: LP roadmap → 17, que está vago) e atualizar links/README. |
| E2 | **Doc 17 inexistente** mas referenciado | docs citam "docs/17 P1/P2/P3" no código (`LandingPage.cs`) | Criar/realocar o doc 17 ou corrigir as referências para o doc certo. |
| E3 | **README e docs/06 dizem "ASP.NET Core 8" / "Ebook.sln"** | CLAUDE.md diz **.NET 10** e **Ebook.slnx** | Atualizar README.md (raiz e docs) + docs/06 para .NET 10 / `.slnx`. |
| E4 | **docs/06 com namespaces fantasma** | doc lista `EbookGeneration/PdfGeneration/LandingPages`; código usa `Content/`, `Content/Lp`, `Video/`, etc. | Reescrever docs/06 a partir da árvore real (`src/Ebook.*`). |
| E5 | **docs/11 marca Frente D/auditoria como pendentes** | código já tem `PdfJobHandler.InjectIllustrationsAsync`, `ComposeInfographics`, `ConversionAudit` | Atualizar docs/11 (Parte 3) marcando D/infográficos/auditoria como ✅; manter só o gate (Sprint B2) como pendente. |
| E6 | **docs/README "Status"** desatualizado | diz "M1 concluído / próximo M2" e pipeline ASP.NET 8 | Revisar o status executivo com a realidade atual do portfólio. |

> Entregável: um PR só de docs. Sem tocar em código. Fecha a maior fonte de confusão.

---

## Categoria 2 — Consolidação estrutural (avaliar; esforço **medium**, risco **médio**)

Itens que **parecem** redundantes. **Não delete sem mapear o papel de cada um** — provavelmente há separação
intencional (catálogo estático × resolver persistido × diretor que gera). O objetivo é *decidir conscientemente*:
manter com justificativa documentada, ou fundir.

### E7 — Camadas de paleta/estilo/marca
Arquivos: `NicheStyleCatalog`, `PaletteCatalog`, `PaletteResolver`, `PaletteDirector`, `NichePalette`, `BrandKit`,
`BrandResolver`, `BrandDirector` (em `Application/Content/Images`).
- **Hipótese de papel:** catálogo = defaults estáticos por nicho; resolver = lê a paleta/brand persistida (gerada por IA);
  director = gera+persiste via IA antes de resolver. Se confirmado, **não é redundância — é uma cadeia**.
- **Ação:** documentar essa cadeia em um cabeçalho/README curto na pasta `Images/`. Só fundir se duas camadas
  comprovadamente fizerem a mesma coisa. **Verificar antes.**

### E8 — `PdfTheme` como caminho legado
- O `QuestPdfRenderer` declara que `PdfTheme`/`Style.For(theme)` é **fallback de compatibilidade** (usado quando não
  há paleta, ex.: testes); o caminho real é sempre `Style.From(palette)`.
- **Ação:** confirmar se algum fluxo de produção ainda depende de `PdfTheme`. Se só os testes usam, **simplificar**:
  manter o enum só onde necessário ou injetar uma paleta de teste, reduzindo dois sistemas de cor a um.
- **Risco:** quebrar testes que dependem do tema. Fazer com a suíte verde a cada passo.

### E9 — `PdfThemeSelector` vs `LpTemplateSelector` vs `NicheStyleCatalog.Classify`
- Três seletores que mapeiam nicho → estilo, todos ancorados em `NicheCategory`. Coerentes, mas espalhados.
- **Ação:** verificar se faz sentido centralizar a classificação num único ponto (já é `NicheStyleCatalog.Classify`)
  e os seletores só consumirem. Provavelmente já é assim — **confirmar e documentar**, não refatorar por refatorar.

---

## Categoria 3 — Relevância de features hoje (decisão de produto; esforço **low** decidir)

O projeto está cedo (poucos produtos ativos). Algumas capacidades podem estar **à frente** da necessidade — não são
"erradas", mas custam manutenção e atenção. Decidir conscientemente: manter ligado, manter desligado (mothball), ou remover.

### E10 — Módulo de Vídeo/Reels (`Video/`, `GenerateVideoJobHandler`, `VideoSchedulerJob`)
- **Estado:** completo e integrado (Piper TTS + FFmpeg), mas **desligado por padrão** (`video.enabled=false`).
- **Avaliação:** enquanto o foco é PDF/LP/Cover e a conversão ainda não está provada, gerar Reels é esforço lateral.
  Está corretamente *gated*, então **não atrapalha**. Custo de manutenção: dependências pesadas (FFmpeg/Piper) no container.
- **Recomendação:** **manter desligado (mothball)**, não remover — o TTS/FFmpeg serão **reaproveitados no audiobook (Sprint D2)**.
  Documentar que é opcional. Reavaliar quando a aquisição por social virar prioridade.

### E11 — Distribuição social (`Social/`, calendário, Meta/X)
- **Avaliação:** faz sentido estrategicamente, mas só gera valor com produtos convertendo. Verificar se está consumindo
  cota/atenção sem retorno hoje.
- **Recomendação:** manter, porém **priorizar conversão (Sprint C) antes** de investir mais em distribuição. Confirmar que está gated/seguro.

> **Importante:** estes são itens de **decisão**, não de deleção automática. Levar a decisão ao dono do produto.

---

## Categoria 4 — Higiene contínua (esforço **low**, risco **baixo**)

| # | Item | Ação |
|---|---|---|
| E12 | Prompts órfãos | Cruzar `prompts/**/*.md` com os `PromptTemplate`/`RenderAsync` usados no código; remover/anotar os não referenciados (ex.: confirmar uso de `prompts/dev/echo.md` — provável dev-only). |
| E13 | `ArtifactType` crescente | Sprints A/B/D adicionam `Epub/Docx/Audiobook/WebReader`. Garantir migrations + retrocompatibilidade (enum como string) e limpar tipos sem uso, se houver. |
| E14 | Cobertura de testes dos caminhos novos | A suíte é grande (67 arquivos de teste) e **nenhum chama IA/rede**. Manter esse padrão ao adicionar exporters/loop de conversão. |
| E15 | Settings acumulando | `SettingKeys` tem dezenas de chaves. Ao adicionar as novas (tournamentSize, gateMinScore, variantCount, smartTraffic...), revisar chaves obsoletas. |
| E16 | `dotnet build -warnaserror` | É a régua. Toda limpeza deve terminar com build + `dotnet test Ebook.slnx` verdes. |

---

## O que **não** fazer
- **Não** deletar as camadas de paleta/brand sem confirmar a cadeia (E7) — risco de quebrar a identidade visual unificada (docs/15).
- **Não** remover o módulo de vídeo — ele alimenta o audiobook (D2) e está inofensivo desligado.
- **Não** "refatorar por estética". Cada mudança precisa de motivo (bug, duplicação real, ou simplificação que some testes verdes).

## Ordem sugerida e fechamento
```
E1–E6 doc drift (PR só de docs)  →  E7–E9 consolidação (com testes verdes a cada passo)  →  E10–E11 decisões de produto  →  E12–E16 higiene
```
**Ao final da Sprint E:** documentação fiel ao código, sistemas de estilo com papéis claros (fundidos ou justificados),
features lateralizadas conscientemente, e base limpa para as Sprints A–D empilharem sem aumentar a entropia.

> **Sugestão de cadência:** rodar a **Categoria 1 (doc drift) ANTES da Sprint A** — é barata e impede que o próximo
> executor repita meu erro de planejar a partir de docs desatualizados. O resto da Sprint E pode ser intercalado.
