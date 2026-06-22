export interface LoginResult {
  token: string;
  expiresAtUtc: string;
}

export interface DashboardSummary {
  productsActive: number;
  productsInPipeline: number;
  nichesCandidate: number;
  jobsFailed: number;
  jobsPending: number;
  aiCallsToday: number;
  aiCacheHitRateToday: number;
  visits30d: number;
  checkoutClicks30d: number;
  sales30d: number;
  revenue30d: number;
  conversionRate30d: number;
}

export interface JobItem {
  id: string;
  type: string;
  status: 'Pending' | 'Running' | 'Succeeded' | 'Dead';
  attempts: number;
  idempotencyKey: string;
  createdAtUtc: string;
  scheduledAtUtc: string;
  finishedAtUtc: string | null;
  lastError: string | null;
}

export interface JobsPage {
  total: number;
  page: number;
  size: number;
  items: JobItem[];
}

export type NicheStatus = 'Candidate' | 'Selected' | 'Active' | 'Discarded';

export interface NicheItem {
  id: string;
  slug: string;
  name: string;
  status: NicheStatus;
  score: number;
  cycleNumber: number;
  discoveredAtUtc: string;
}

export type ProductStatus =
  | 'Pipeline'
  | 'AwaitingApproval'
  | 'Reworking'
  | 'Publishing'
  | 'Published'
  | 'Synchronized'
  | 'Unsynchronized'
  | 'Live'
  | 'Iterating'
  | 'Retired';

export type ProductStage = 'Outline' | 'Writing' | 'Review' | 'Pdf' | 'Lp' | 'Publishing' | 'Live';

export type PublicationPlatform = 'Kiwify' | 'Hotmart';

export interface ProductItem {
  id: string;
  slug: string;
  title: string;
  status: ProductStatus;
  stage: ProductStage;
  price: number;
  currency: string;
  publicationPlatform: string | null;
  createdAtUtc: string;
}

export interface ProductDetail extends ProductItem {
  nicheId: string;
  qualityTier: string;
  lpUrl: string | null;
  checkoutUrl: string | null;
  kiwifyProductId: string | null;
  salesCopyJson: string;
  description: string | null;
  emailLanguage: string | null;
  category: string | null;
  publishedAtUtc: string | null;
}

export interface OutlineChapter {
  n: number;
  title: string;
  goal: string;
  keyPoints: string[];
  targetWords: number;
}

export interface Outline {
  title: string;
  subtitle: string | null;
  promise: string;
  tone: string;
  chapters: OutlineChapter[];
}

export interface SocialPostItem {
  id: string;
  day: number;
  network: string;
  postType: string;
  caption: string;
  hashtags: string;
  status: string;
  mediaPath: string | null;
  scheduledAtUtc: string;
  approvedAtUtc: string | null;
  publishedAtUtc: string | null;
  externalId: string | null;
}

/** Canal social de um nicho (1 por nicho). Não traz o token (só sinaliza presença). */
export interface Channel {
  id: string;
  nicheId: string;
  nicheName: string;
  name: string;
  platform: string;
  connected: boolean;
  pageId: string | null;
  igUserId: string | null;
  hasToken: boolean;
  publicMediaBaseUrl: string | null;
  tokenExpiresAtUtc: string | null;
  createdAtUtc: string;
}

export interface Funnel {
  visits: number;
  checkoutClicks: number;
  sales: number;
  revenue: number;
  conversionRate: number;
}

export interface ChannelMetric {
  channel: string;
  visits: number;
  checkoutClicks: number;
  sales: number;
  revenue: number;
}

export interface ProductMetrics {
  total: Funnel;
  byChannel: ChannelMetric[];
}

export interface OptimizationRun {
  id: string;
  cycleNumber: number;
  executedAtUtc: string;
  status: string;
  decisionCount: number;
}

/** Laboratório de LP: um passo do caminho percorrido pela landing page (modal de detalhes). */
export interface LpTraceStep {
  stage: string;
  actor: string;
  detail: string;
  result: string;
}

export interface LpTrace {
  nicheName: string;
  category: string;
  template: string;
  paletteBackground: string;
  paletteAccent: string;
  headingFont: string;
  bodyFont: string;
  title: string;
  feedbackUsed: boolean;
  steps: LpTraceStep[];
}

export interface GenerateTestLpResult {
  html: string;
  trace: LpTrace;
}

export type OptimizationDecisionKind = 'Scale' | 'Keep' | 'Iterate' | 'Kill';

export interface OptimizationDecision {
  id: string;
  productId: string;
  productTitle: string;
  decision: OptimizationDecisionKind;
  status: 'Proposed' | 'Approved' | 'Executed' | 'Vetoed';
  rationale: string;
  actionsJson: string;
}

/** Correspondência do produto na Kiwify (id + URL de checkout), resolvida via API pública. */
export interface KiwifyMatch {
  kiwifyProductId: string;
  checkoutUrl: string;
  name: string;
}

export type SettingMap = Record<string, string>;

/** Push do hub SignalR quando um job muda de estado. */
export interface RealtimeJobChanged {
  id: string;
  type: string;
  status: JobItem['status'];
  attempts: number;
  productId: string | null;
  lastError: string | null;
}

/** Push do hub SignalR quando um produto sofre uma transição de domínio. */
export interface RealtimeProductChanged {
  productId: string;
  event: string;
}

// E14-08 — telemetria do Media Gateway
export interface MediaProviderStat {
  provider: string;
  generatedToday: number;
  generatedThisMonth: number;
  cacheHitsToday: number;
  dailyLimit: number;
  totalBytesToday: number;
  avgDurationMsToday: number;
}

export interface MediaTelemetry {
  providers: MediaProviderStat[];
  cacheHitsToday: number;
  cacheEntriesTotal: number;
  cacheSizeBytes: number;
}

// Fase 3 — telemetria unificada de fontes externas (texto + imagem)
export interface SourceStat {
  provider: string;
  kind: string; // "Texto" | "Imagem"
  generatedToday: number;
  generatedThisMonth: number;
  tokensToday: number;
  bytesToday: number;
  avgDurationMsToday: number;
}

export interface SourcesTelemetry {
  sources: SourceStat[];
  cacheHitsToday: number;
  mediaCacheEntriesTotal: number;
  mediaCacheSizeBytes: number;
}

// Fase 3B — proveniência do PDF (quem fez texto/imagens)
export interface ProvenanceEntry {
  purpose: string;
  provider: string;
  cacheHit: boolean;
  tokens: number;
  bytes: number;
  atUtc: string;
}

export interface ProductProvenance {
  text: ProvenanceEntry[];
  images: ProvenanceEntry[];
  textCount: number;
  imageCount: number;
  totalTokens: number;
  totalBytes: number;
}

// Fase 7 — auditoria de conversão por IA
export interface AuditItem {
  item: string;
  pass: boolean;
  note: string;
}

export interface ConversionAudit {
  verdict: string; // pass | warn | fail
  score: number;
  summary: string;
  items: AuditItem[];
}
