# HANDOFF — estado atual e como continuar

> Resumo de continuidade para retomar o trabalho em outra máquina.
> **Sem segredos aqui** (repo público): tokens/senhas/credenciais ficam no Railway (env vars)
> e na máquina local (`~/.claude`). Veja "Segredos" abaixo.
> Atualizado: 2026-06-16.

## Onde tudo está rodando

- **Produção:** https://app.tomolibrary.com.br (Cloudflare proxy → Railway) — **LIVE**
- **URL Railway:** https://ebook-tomo-production.up.railway.app
- **Repo:** https://github.com/SWiseG/ebook-tomo (branch `main`, auto-deploy no push)
- **Stack:** ASP.NET Core .NET 10 + Angular 21 + SQLite/FileStore, monolito modular event-driven.
  Ver `CLAUDE.md` (regras de arquitetura) e `docs/README.md` (planejamento).

## Núcleo do produto: COMPLETO

Todos os épicos P0 (E00–E13) implementados. Pipeline autônomo ponta a ponta:
descoberta de nicho → ebook (outline→capítulos→revisão→PDF→capa) → landing page →
publicação Kiwify → calendário social → analytics/pixel → otimizador de ROI (30 dias).
Vídeo/Reels (E10) e seams Kiwify(Playwright)/Meta(Graph) existem mas com **gates desligados
por padrão** (`Kiwify__AutoPublish`, `social.autoPublish`, `video.enabled`).

## Feito nas últimas sessões

1. **Deploy Railway** + domínio `app.tomolibrary.com.br` (Cloudflare). Gotcha do custom
   domain: a CNAME aponta para um alvo gerado pelo Railway (ex.: `xcn4qnsa.up.railway.app`)
   + um registro TXT `_railway-verify.app`. O botão "Connect" do Cloudflare no painel Railway
   cria os dois automaticamente. SSL mode = **Full** (não Full-strict).
2. **Ícones** corrigidos (primeicons servidos de `public/fonts/` via `/fonts/`).
3. **Claude CLI autenticado** no container e **persistente**: o ENTRYPOINT do Dockerfile injeta
   `CLAUDE_CREDENTIALS_JSON` (env var) em `/data/.claude/.credentials.json` **só se o arquivo não
   existir** (preserva tokens renovados). `/data` é volume persistente.
4. **Ebook "Virada Financeira" gerado** (produto `c066c272-8d48-435e-8d83-20605bfae0e1`,
   slug `financas-pessoais`). Está em status **Publishing**, parado no **gate manual do Kiwify**.
5. **Tempo real (SignalR)** — LIVE. Ver seção dedicada abaixo.

## Tempo real (SignalR) — workstream 1/5 das melhorias de plataforma

Atualização ao vivo do painel (jobs, produtos, dashboard, detalhe), sino de notificações,
ponto de status de conexão e o botão hambúrguer mobile que faltava.

- Abstração `IRealtimeNotifier` + DTOs em `Application/Common/Realtime` (sem SignalR vazando).
- `ProductRealtimeHandler` reemite 7 eventos de produto via Outbox; `JobWorker` emite mudanças de job.
- `TomoHub` + `SignalRRealtimeNotifier` na Api. JWT chega via **query string `access_token`**
  (WebSocket não manda header) — configurado em `JwtBearerEvents.OnMessageReceived` para path `/hubs`.
- `NullRealtimeNotifier` é o padrão (registrado em `AddApplication`); a Api sobrescreve com SignalR.
- Front: `@microsoft/signalr` + `core/realtime.service.ts`; `proxy.conf.json` roteia `/hubs` com `ws:true`.
- Validado em prod: negotiate 401 sem token, 200 com token (WebSockets + SSE fallback) via Cloudflare.

## Próximos workstreams de plataforma (decisões já travadas com o usuário)

Ordem/decisões definidas; falta implementar 2–5:

2. **i18n** com **Transloco** (runtime, troca ao vivo). Idiomas: **pt-BR + Inglês + Espanhol**.
   Extrair strings dos templates → arquivos de tradução + seletor de idioma no header.
3. **Multi-usuário com papéis** (**admin / editor / leitor**). Hoje é admin único via
   `AdminAuthOptions` (config + hash). Precisa: entidade `User` no Domain + migration + CRUD +
   papéis/autorização + tela de gestão. Substitui o login de config.
4. **Polimento mobile** — tabelas (jobs/produtos) viram cards no mobile, alvos de toque maiores,
   detail pages reflowadas.
5. **Onboarding** — reescrever a página `/tutorial` ("Como usar") cobrindo o fluxo real
   (nicho → ebook → aprovação → LP → Kiwify) + tooltips contextuais.

## Marco de negócio pendente (M1): 1º produto à venda

O "Virada Financeira" está em **Publishing**. Para fechar M1, no painel (Detalhe do produto):
publicar manualmente na Kiwify e preencher **id do produto Kiwify + URL de checkout**
(`POST /api/v1/products/{id}/publish`), **ou** ativar `Kiwify__AutoPublish=true` no Railway.

## Como rodar localmente

```bash
dotnet build Ebook.slnx -warnaserror          # solution é .slnx
dotnet test Ebook.slnx                         # 126 testes; nenhum chama IA/rede
dotnet run --project src/Ebook.Api --launch-profile http   # API :5161 (login dev admin/admin)
cd src/Ebook.Admin && npm install && npm start  # painel com proxy p/ :5161 (inclui /hubs ws)
cd src/Ebook.Admin && npm run build
```

Dev local (Windows) usa `appsettings.Development.json` (gitignored) apontando `Ai:ClaudeCommand`
para o claude.exe da extensão VS Code. Em produção o CLI vem do npm e autentica pelo volume.

## Segredos (NÃO estão no repo)

Ficam **só** no Railway (Variables) e na sua máquina/gerenciador de senhas:
- Admin do painel (usuário/senha de produção), `Jwt__Secret`.
- `CLAUDE_CREDENTIALS_JSON` (copiado de `~/.claude/.credentials.json` da máquina logada no Claude Pro).
- Tokens: Railway (account), Cloudflare (DNS), GitHub. Kiwify/Meta quando for ativar integrações.
- IDs do projeto/serviço/volume Railway e zona Cloudflare: pegar no respectivo dashboard.

Para continuar em outra máquina: `git clone`, instalar .NET 10 SDK + Node, `npm install`,
configurar `appsettings.Development.json` local e logar no Claude (`claude login`) se for usar IA local.
