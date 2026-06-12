# EBOOK — Plataforma SaaS Autônoma de E-books

Descoberta de nichos → geração de e-book/PDF → landing page → Kiwify → divulgação social → analytics → otimização de ROI, em ciclos autônomos de 30 dias com custo de infraestrutura ~US$ 5/mês.

📚 **Planejamento completo**: [docs/README.md](docs/README.md) · Roadmap: [docs/03-roadmap-mvp.md](docs/03-roadmap-mvp.md)

## Status

✅ **Fase 0 — Fundação** concluída: Clean Architecture (.NET 10), SQLite+Outbox+Jobs+Quartz, AI Gateway com cache (assinatura Claude Pro via CLI), painel Angular 21 (login/dashboard/jobs), Docker, CI/CD.
🔜 **Fase 1 — Pipeline de Conteúdo**: Trend Discovery, Knowledge Enrichment, Ebook Generator, PDF.

## Rodando em dev

```bash
# API (http://localhost:5161 — login: admin / admin)
dotnet run --project src/Ebook.Api --launch-profile http

# Painel (http://localhost:4200, proxy para a API)
cd src/Ebook.Admin && npm install && npm start

# Testes
dotnet test Ebook.slnx
```

Pré-requisitos: .NET 10 SDK, Node 22+, Claude Code CLI autenticado (assinatura Pro).

## Produção

Guia completo em [docs/08-implantacao.md](docs/08-implantacao.md) (VPS + Docker Compose + nginx + Litestream). Imagem publicada no GHCR pelo workflow [deploy.yml](.github/workflows/deploy.yml).
