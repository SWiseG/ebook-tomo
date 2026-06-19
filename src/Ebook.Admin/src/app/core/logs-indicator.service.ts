import { HttpClient } from '@angular/common/http';
import { inject, Injectable, signal } from '@angular/core';

interface LogResponse {
  lastId: number;
  entries: Array<{ level: string }>;
}

@Injectable({ providedIn: 'root' })
export class LogsIndicatorService {
  private readonly http = inject(HttpClient);

  readonly recentErrors = signal(0);
  private lastVisitedId = 0;
  private pollTimer?: ReturnType<typeof setInterval>;

  constructor() {
    this.poll();
    this.pollTimer = setInterval(() => this.poll(), 15_000);
  }

  /** Chamado ao entrar na tela de logs — reseta o contador. */
  markVisited(lastId: number): void {
    this.lastVisitedId = lastId;
    this.recentErrors.set(0);
  }

  private poll(): void {
    this.http
      .get<LogResponse>(`/api/v1/logs?afterId=${this.lastVisitedId}&limit=500`)
      .subscribe({
        next: (r) => {
          const errors = r.entries.filter(
            (e) => e.level === 'ERROR' || e.level === 'FATAL',
          ).length;
          this.recentErrors.set(errors);
        },
        error: () => {},
      });
  }
}
