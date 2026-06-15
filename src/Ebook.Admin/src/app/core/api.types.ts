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
  | 'Live'
  | 'Iterating'
  | 'Retired';

export type ProductStage = 'Outline' | 'Writing' | 'Review' | 'Pdf' | 'Lp' | 'Publishing' | 'Live';

export interface ProductItem {
  id: string;
  slug: string;
  title: string;
  status: ProductStatus;
  stage: ProductStage;
  price: number;
  currency: string;
  createdAtUtc: string;
}

export interface ProductDetail extends ProductItem {
  nicheId: string;
  qualityTier: string;
  lpUrl: string | null;
  checkoutUrl: string | null;
  kiwifyProductId: string | null;
  salesCopyJson: string;
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
  status: string;
  scheduledAtUtc: string;
  publishedAtUtc: string | null;
  externalId: string | null;
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

export type SettingMap = Record<string, string>;
