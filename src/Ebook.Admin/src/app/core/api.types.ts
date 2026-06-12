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
