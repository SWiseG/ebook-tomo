# ── 1. Build do painel Angular ─────────────────────────────────────────────
FROM node:22-alpine AS admin-build
WORKDIR /app
COPY src/Ebook.Admin/package*.json ./
RUN npm ci
COPY src/Ebook.Admin/ ./
RUN npm run build

# ── 2. Build da API .NET ───────────────────────────────────────────────────
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS api-build
WORKDIR /src
COPY . .
RUN dotnet restore src/Ebook.Api
RUN dotnet publish src/Ebook.Api -c Release -o /out --no-restore

# ── 3. Runtime: ASP.NET + Node (Claude Code CLI da assinatura Pro) ─────────
# E10 (vídeo): ffmpeg incluído abaixo. Piper TTS = baixar o binário + voz pt-BR e apontar
#   Video__PiperPath / Video__PiperVoicePath.
# E07 (Kiwify/Playwright): instalar o Chromium do Playwright para ligar a automação, p.ex.:
#   RUN pwsh /app/playwright.ps1 install --with-deps chromium
#   (ou trocar a base por mcr.microsoft.com/playwright/dotnet na versão correspondente do NuGet).
FROM mcr.microsoft.com/dotnet/aspnet:10.0
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl ca-certificates gnupg \
    && curl -fsSL https://deb.nodesource.com/setup_22.x | bash - \
    && apt-get install -y --no-install-recommends nodejs fontconfig fonts-liberation ffmpeg \
    && npm install -g @anthropic-ai/claude-code \
    && apt-get purge -y gnupg \
    && rm -rf /var/lib/apt/lists/*

WORKDIR /app
COPY --from=api-build /out .
COPY --from=admin-build /app/dist/ebook-admin/browser ./wwwroot
COPY prompts ./prompts

ENV ASPNETCORE_URLS=http://+:8080 \
    Data__RootPath=/data \
    Ai__PromptsPath=/app/prompts \
    HOME=/home/app

RUN mkdir -p /data /home/app && chown -R app:app /data /home/app /app
USER app

VOLUME ["/data"]
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=5s --start-period=20s \
    CMD curl -fsS http://localhost:8080/health/live || exit 1

ENTRYPOINT ["dotnet", "Ebook.Api.dll"]
