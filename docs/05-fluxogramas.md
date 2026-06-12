# 05 — Fluxogramas

Diagramas em Mermaid (renderizam no GitHub/VS Code).

## 1. Ciclo mestre de 30 dias

```mermaid
flowchart TD
    A[Cron 30d: discover-niches] --> B[Trend Discovery<br/>coleta 4 fontes]
    B --> C[Score & ranking de nichos]
    C --> D{Portfólio < 10 ativos<br/>ou decisão Kill pendente?}
    D -- sim --> E[Seleciona top N nichos<br/>NicheSelected]
    D -- não --> F[Apenas registra snapshot]
    E --> G[Pipeline de produto<br/>fluxo 2]
    G --> H[Produto Live]
    H --> I[Divulgação contínua<br/>fluxo 4]
    I --> J[Analytics diário<br/>fluxo 5]
    J --> K[Cron 30d: optimize-cycle]
    K --> L[ROI Optimizer<br/>fluxo 6]
    L -- Kill --> D
    L -- Iterate/Scale --> H
    L --> M[cycle-report.json<br/>realimenta score e prompts]
    M --> A
```

## 2. Pipeline de geração de produto

```mermaid
flowchart TD
    A[NicheSelected] --> B{Knowledge existente<br/>cobre o tema?}
    B -- sim --> D[Reusa KnowledgePack<br/>custo IA = 0]
    B -- parcial/não --> C[Knowledge Enrichment:<br/>scraping + estruturação via AI Gateway]
    C --> D
    D --> E[Job: gerar outline]
    E --> F[Jobs: capítulo 1..N<br/>retomáveis, contexto mínimo]
    F --> G{Tier ≥ Commercial?}
    G -- sim --> H[Job: passada de revisão]
    G -- não --> I
    H --> I[Metadados comerciais<br/>sales-copy.json]
    I --> J[PDF Generator<br/>tema + capa]
    J --> K[LP Generator<br/>template + copy + pixel]
    K --> L{Modo RequireApproval?}
    L -- sim --> M[ApprovalRequest<br/>fila no painel]
    M -- aprovado --> N
    M -- rejeitado --> F
    L -- não --> N[Kiwify Publisher<br/>fluxo 3]
```

## 3. Publicação na Kiwify (Playwright)

```mermaid
flowchart TD
    A[Job: publish-kiwify] --> B{Sessão Playwright válida?}
    B -- não --> C[Login + persistir storageState]
    B -- sim --> D
    C --> D[Criar produto:<br/>nome, descrição, preço, upload PDF]
    D --> E[Configurar checkout + obter URL]
    E --> F{Sucesso?}
    F -- não --> G[Retry com backoff<br/>3x → Job Dead + alerta painel]
    F -- sim --> H[Salva KiwifyProductId + CheckoutUrl]
    H --> I[Atualiza LP com link de checkout]
    I --> J[ProductPublished]
    J --> K[Gera calendar.json de 30 dias]

    W[Webhook Kiwify: venda/refund] --> X{Token válido?}
    X -- não --> Y[400 + log]
    X -- sim --> Z[SaleEvent gravado<br/>→ Analytics]
```

## 4. Divulgação social (cron diário)

```mermaid
flowchart TD
    A[Cron diário: social-calendar] --> B[Busca SocialPost<br/>Planned com ScheduledAt ≤ hoje]
    B --> C{Mídia já gerada?}
    C -- não --> D[Image Generator: card/story<br/>Video Generator: reel semanal]
    C -- sim --> E
    D --> E{Rede}
    E -- IG/FB --> F[Meta Graph API<br/>publica com UTM]
    E -- X --> G{Cota mensal<br/>disponível?}
    G -- sim --> H[X API v2 publica]
    G -- não --> I[Skipped — repõe<br/>no próximo ciclo]
    F --> J[Status Published + ExternalId]
    H --> J
    F -. falha .-> K[Retry → Failed + alerta]
    J --> L[Cron coleta métricas<br/>→ MetricsJson]
```

## 5. Analytics — funil de dados

```mermaid
flowchart LR
    subgraph Fontes
        P[Pixel LP<br/>visita/clique + UTM]
        W[Webhook Kiwify<br/>vendas/refunds]
        S[APIs sociais<br/>alcance/cliques]
    end
    P --> R[(Eventos brutos<br/>append JSON)]
    W --> SE[(SaleEvent)]
    S --> SP[(SocialPost.MetricsJson)]
    R --> AGG[Cron diário:<br/>daily-metrics]
    SE --> AGG
    SP --> AGG
    AGG --> MD[(MetricDaily<br/>por produto/canal)]
    MD --> DASH[Dashboard funil & ROI]
    MD --> OPT[ROI Optimizer]
```

## 6. ROI Optimizer — feedback loop

```mermaid
flowchart TD
    A[Cron 30d: optimize-cycle] --> B[Carrega MetricDaily 30d<br/>+ custos estimados por produto]
    B --> C[Calcula ROI, conversão,<br/>tendência por produto]
    C --> D{Classificação por regras}
    D -- "ROI alto e crescendo" --> E[Scale: +posts, +vídeo,<br/>testar preço maior]
    D -- "ROI ok" --> F[Keep]
    D -- "Conversão baixa, tráfego ok" --> G[Iterate: nova headline/preço/<br/>calendário — variante A/B]
    D -- "Sem tração após 2 ciclos" --> H[Kill: arquivar produto<br/>+ despublicar]
    E & F & G & H --> I{Modo RequireApproval?}
    I -- sim --> J[Decisões na fila do painel]
    I -- não --> K[Executa ações via eventos]
    J -- aprovadas --> K
    H --> L[Dispara reposição:<br/>NicheSelected p/ manter ≥ 10]
    K --> M[Análise qualitativa via AI Gateway:<br/>aprendizados do ciclo]
    M --> N[cycle-report.json:<br/>ajusta pesos do Trend Score,<br/>prompts e templates]
```

## 7. AI Gateway — decisão por requisição

```mermaid
flowchart TD
    A[Request: purpose + inputs + tier] --> B[Normaliza + hash SHA-256]
    B --> C{AiCache hit?}
    C -- sim --> Z[Retorna cache<br/>custo 0]
    C -- não --> D{KnowledgeAsset<br/>cobre ≥ limiar?}
    D -- sim --> E[Adapta conteúdo existente<br/>custo 0 ou mínimo]
    D -- não --> F{Purpose tem<br/>template determinístico?}
    F -- sim --> G[Renderiza template<br/>custo 0]
    F -- não --> H{Orçamento do pipeline<br/>e janela Pro disponíveis?}
    H -- não --> I[Enfileira p/ próxima janela<br/>job adiado]
    H -- sim --> J[claude -p headless<br/>prompt comprimido]
    J --> K[Valida resposta<br/>formato/tamanho]
    K -- inválida --> L[1 retry com correção]
    K -- ok --> M[Persiste FileStore +<br/>AiCache + AiUsage]
    E & G & M --> Z
```

## 8. Infraestrutura de eventos e jobs

```mermaid
flowchart LR
    UC[Caso de uso] -->|mesma transação| DB[(SQLite:<br/>agregado + OutboxEvent)]
    DB --> DISP[Outbox Dispatcher<br/>HostedService + Channel]
    DISP --> H1[Handler A] & H2[Handler B]
    H1 -->|trabalho longo| JQ[(Job queue)]
    JQ --> W[Job Worker<br/>retry exponencial]
    W -->|falha 3x| DL[Dead-letter<br/>+ alerta painel]
    H1 & H2 --> PE[(ProcessedEvent<br/>idempotência)]
    Q[Quartz crons] --> UC
```
