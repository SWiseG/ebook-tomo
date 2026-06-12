# 08 — Documentação de Implantação

## 1. Infraestrutura alvo

| Item | Escolha | Custo/mês |
|---|---|---|
| VPS | 2 vCPU / 4 GB RAM / 40 GB SSD (Hetzner CX22 ou similar; alternativa: Oracle Cloud Always Free ARM 4 vCPU/24 GB = US$ 0) | ~US$ 4,50 (ou 0) |
| Domínio | 1 domínio (.com.br ou .com) | ~US$ 1 |
| DNS/CDN/TLS | Cloudflare free (proxy + certificado de borda) | 0 |
| Registry | GHCR (GitHub Container Registry) | 0 |
| Backup remoto | Cloudflare R2 free tier (10 GB) via Litestream/rclone | 0 |
| **Total** | | **≈ US$ 5,50** |

RAM dimensionada para picos: Playwright/Chromium (~500 MB) + FFmpeg + Piper rodam um por vez (concorrência 1 por categoria de job).

## 2. Topologia no VPS

```
Internet ──► Cloudflare (DNS+TLS) ──► nginx (host, :80/:443)
                                        ├── app.dominio        → proxy :8080 (API + SPA)
                                        ├── lp.dominio/{slug}  → /data/artifacts/**/lp (estático)
                                        └── px + webhooks      → proxy :8080
docker compose:
  api        ghcr.io/<user>/ebook-api   volumes: /data/db /data/content /data/artifacts /data/logs /data/secrets ~/.claude
  litestream replicação contínua do SQLite → R2
watchtower (opcional) ou deploy via SSH (padrão)
```

## 3. Dockerfile (multi-stage — visão)

1. `node:22` → build Angular (`Ebook.Admin`) → `dist/`
2. `mcr.microsoft.com/dotnet/sdk:10.0` → restore/test/publish API (dist → `wwwroot`)
3. Runtime `mcr.microsoft.com/playwright/dotnet:v1.4x` (inclui Chromium e dependências) +:
   - `ffmpeg`, binário `piper` + voz pt-BR (camada própria)
   - Claude Code CLI (`npm i -g @anthropic-ai/claude-code`)
   - usuário não-root, `HEALTHCHECK` → `/health/live`

Imagem única ≈ 2,5 GB (aceitável; é o preço de Playwright+FFmpeg embutidos).

## 4. docker-compose (produção — visão)

```yaml
services:
  api:
    image: ghcr.io/<user>/ebook-api:latest
    restart: unless-stopped
    env_file: .env
    ports: ["127.0.0.1:8080:8080"]
    volumes:
      - /opt/ebook/data:/data
      - /opt/ebook/claude:/home/app/.claude   # sessão da assinatura Pro
    mem_limit: 3g
  litestream:
    image: litestream/litestream
    restart: unless-stopped
    command: replicate -config /etc/litestream.yml
    volumes:
      - /opt/ebook/data/db:/data/db
      - ./litestream.yml:/etc/litestream.yml
    env_file: .env
```

## 5. Variáveis de ambiente (`.env`)

```
ASPNETCORE_ENVIRONMENT=Production
APP_BASEURL=https://app.dominio
LP_BASEURL=https://lp.dominio
JWT_SECRET=...                 ADMIN_USER=...        ADMIN_PASSWORD_HASH=...
SECRETS_AES_KEY=...            # cifra credenciais em repouso
KIWIFY_LOGIN=... KIWIFY_PASSWORD=... KIWIFY_WEBHOOK_TOKEN=...
META_PAGE_TOKEN=... META_IG_BUSINESS_ID=... META_PAGE_ID=...
X_API_KEY=... X_API_SECRET=... X_ACCESS_TOKEN=... X_ACCESS_SECRET=...
PEXELS_API_KEY=...
ANTHROPIC_API_KEY=             # vazio = fallback API desligado
R2_ACCESS_KEY=... R2_SECRET=... R2_BUCKET=ebook-backup
```

Secrets de produção vivem **somente** no VPS (`/opt/ebook/.env`, chmod 600) e no GitHub Environments (para o deploy). Nunca no repositório.

## 6. CI/CD (GitHub Actions)

### `ci.yml` — em todo push/PR
1. Checkout → setup .NET 8 + Node 20
2. `dotnet build -warnaserror` → `dotnet test` (com cobertura)
3. `npm ci && npm run lint && npm test && npm run build` (Admin)
4. Em PR: resultado como check obrigatório

### `deploy.yml` — em push na `main` (ou tag)
1. Build da imagem multi-stage → push `ghcr.io/<user>/ebook-api:{sha,latest}`
2. SSH no VPS (chave em GitHub Secrets):
   `docker compose pull && docker compose up -d && docker system prune -f`
3. Smoke test: `curl https://app.dominio/health/ready` (rollback = re-tag da imagem anterior)

## 7. Setup inicial do VPS (uma vez)

```bash
# 1. SO: Ubuntu 24.04 LTS, usuário não-root + chave SSH, ufw (22, 80, 443)
# 2. Docker + compose plugin
curl -fsSL https://get.docker.com | sh
# 3. nginx no host + certbot (ou só Cloudflare proxy com origin cert)
# 4. Estrutura
sudo mkdir -p /opt/ebook/{data/{db,content,artifacts,logs,secrets},claude}
# 5. Copiar deploy/{docker-compose.yml,litestream.yml,nginx/*} e criar .env
# 6. Login único da assinatura Claude Pro (persiste no volume):
docker compose run --rm api claude login   # seguir fluxo de auth no browser
# 7. Subir
docker compose up -d
# 8. Sessão Kiwify: disparar job "kiwify-login" pelo painel (salva storageState)
```

## 8. Backup e restauração

- **SQLite**: Litestream replica continuamente para R2 (RPO ~segundos).
- **Conteúdo/artefatos**: cron diário no host `rclone sync /opt/ebook/data/{content,artifacts} r2:ebook-backup/files` (incremental).
- **Restore (runbook, ~30 min)**: VPS novo → setup acima → `litestream restore` do banco → `rclone sync` reverso → `docker compose up -d` → relogar Claude/Kiwify se sessão expirou.
- Teste de restauração: 1×/trimestre (job lembra no painel).

## 9. Operação e monitoramento

| Sinal | Mecanismo |
|---|---|
| Container caiu | `restart: unless-stopped` + Uptime Kuma (no próprio VPS) ou cron `curl /health/live` → e-mail |
| Job Dead / cron atrasado | banner no painel + (P1) notificação por e-mail via SMTP grátis |
| Disco > 85 % | health check `ready` degrada + housekeeping agressivo |
| Quebra do Playwright/Kiwify | teste sintético semanal → alerta |
| Cota X/Meta | contadores em Settings, visíveis no dashboard |

## 10. Checklist de go-live

- [ ] DNS `app.` e `lp.` no Cloudflare com TLS ativo
- [ ] `.env` completo, secrets fora do git, `SECRETS_AES_KEY` salvo em cofre pessoal
- [ ] Login Claude Pro persistido no volume e testado (`/api/dev/ai-echo`)
- [ ] Sessão Kiwify validada + webhook configurado na Kiwify apontando para produção
- [ ] App Meta aprovado (permissions de publicação) e tokens longos salvos
- [ ] Litestream replicando (verificar objeto no R2) + rclone agendado
- [ ] Backup-restore ensaiado uma vez
- [ ] Health checks verdes e deploy automático validado com commit trivial
