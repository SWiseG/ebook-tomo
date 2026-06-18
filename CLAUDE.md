# EBOOK — Plataforma autônoma de e-books

Monolito modular ASP.NET Core (.NET 10) + Angular 21, SQLite + FileStore JSON, event-driven in-process. Planejamento completo em `docs/` (ler `docs/README.md` primeiro). Roadmap por fases em `docs/03-roadmap-mvp.md` — **Fase 0 concluída**.

## Comandos

```bash
dotnet build Ebook.slnx -warnaserror          # build (solution é .slnx, não .sln)
dotnet test Ebook.slnx                        # 40+ testes; nenhum chama IA ou rede real
dotnet run --project src/Ebook.Api --launch-profile http   # API em http://localhost:5161 (login dev: admin/admin)
# Docs interativas (só Development): http://localhost:5161/scalar/v1 (OpenAPI em /openapi/v1.json)
dotnet dotnet-ef migrations add <Nome> --project src/Ebook.Infrastructure --startup-project src/Ebook.Api --output-dir Persistence/Migrations
dotnet run --project src/Ebook.Api -- hash-password <senha>  # gera hash PBKDF2 p/ AdminAuth
cd src/Ebook.Admin && npm start               # painel com proxy para :5161
cd src/Ebook.Admin && npm run build
```

Após criar migration, **rebuild antes de rodar** (`Migrate()` usa o assembly compilado).

## Arquitetura (regras inegociáveis)

- Dependências: `Api → Infrastructure → Application → Domain`. Domain/Application não referenciam pacotes de infra.
- **Nenhum módulo chama IA diretamente** — sempre `IAiGateway` (Application/Ai). Cadeia: cache → knowledge → template → Claude CLI (assinatura Pro). Toda resposta nova é cacheada por hash; respeite isso ao criar novos purposes.
- Prompts **nunca** hardcoded em C# — arquivos em `/prompts/{area}/{nome}.md` com placeholders `{{var}}`, carregados via `IPromptLibrary`.
- Domain Events → Outbox (mesma transação via `IUnitOfWork.SaveChangesAsync`) → `OutboxDispatcher` → `IDomainEventHandler<T>`. Handlers idempotentes (reentrega at-least-once).
- Trabalho longo (IA, Playwright, render) vira job: `IJobQueue.EnqueueAsync` com `IdempotencyKey` natural (ex.: `chapter:{productId}:{n}`); implementar `IJobHandler` (registro automático por scan no `AddApplication`).
- Casos de uso: `ICommand<T>`/`IQuery<T>` + handler (1 arquivo), despachados por `IDispatcher`. Retornar `Result<T>` (exceção só para bug/infra).
- SQLite guarda estado/índice/métricas; conteúdo (capítulos, packs, posts) vai para `IFileStore` (`/data/content`), caminho+hash indexados no banco.
- Transições de estado de `Product`/`Niche` só por métodos do agregado (emitem eventos). Nunca setar Status direto.
- Datas sempre UTC via `IClock`. Enums persistidos como string. Código/identificadores em inglês; conteúdo gerado e UI em pt-BR.
- **Geração de conteúdo** (e-book, PDF, capa, imagem, LP, fontes, cores, copy) segue o **padrão editorial inegociável** em `docs/11-padrao-editorial.md` (ex.: nunca Times/Arial; fontes/cores por nicho; ≥1 imagem a cada 2-3 páginas; PAS por capítulo; micro-CTA no fim de cada capítulo).

## Testes

- Sem mock framework: fakes manuais (ver `FakeFinalResolver`, `CountingHandler`).
- Infra: `TestHost.Build()` (SQLite in-memory + FileStore temp). Workers expõem `ProcessPendingOnceAsync`/`ProcessNextAsync` para testes sem loop.
- Jamais chamar Claude CLI/rede em teste.

## Dev local (Windows)

`appsettings.Development.json` aponta `Ai:ClaudeCommand` para o claude.exe da extensão VS Code (caminho versionado — atualizar se a extensão atualizar) e `Ai:PromptsPath` para `../../prompts`. Em produção (Docker) o CLI é instalado via npm e autenticado pelo volume `/home/app/.claude`.
