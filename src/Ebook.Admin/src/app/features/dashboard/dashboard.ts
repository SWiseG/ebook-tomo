import { HttpClient } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { CurrencyPipe, DecimalPipe, PercentPipe } from '@angular/common';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { debounceTime, merge } from 'rxjs';
import { RouterLink } from '@angular/router';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { ButtonModule } from 'primeng/button';
import { DashboardSummary } from '../../core/api.types';
import { NotificationService } from '../../core/notification.service';
import { RealtimeService } from '../../core/realtime.service';
import { Loading } from '../../shared/loading';

@Component({
  selector: 'app-dashboard',
  imports: [CurrencyPipe, DecimalPipe, PercentPipe, RouterLink, TranslocoDirective, ButtonModule, Loading],
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.scss',
})
export class Dashboard {
  private readonly http = inject(HttpClient);
  private readonly notify = inject(NotificationService);
  private readonly realtime = inject(RealtimeService);
  private readonly t = inject(TranslocoService);

  readonly summary = signal<DashboardSummary | null>(null);
  readonly error = signal<string | null>(null);

  constructor() {
    this.load();

    // KPIs refletem jobs + produtos: recarrega quando qualquer um muda.
    merge(this.realtime.jobChanged$, this.realtime.productChanged$)
      .pipe(debounceTime(800), takeUntilDestroyed())
      .subscribe(() => this.load());
  }

  private load(): void {
    this.http.get<DashboardSummary>('/api/v1/dashboard/summary').subscribe({
      next: (data) => this.summary.set(data),
      error: () => this.error.set(this.t.translate('dashboard.loadError')),
    });
  }

  discover(): void {
    this.http.post('/api/v1/niches/discover', {}).subscribe({
      next: () =>
        this.notify.success(
          this.t.translate('dashboard.discoverQueued'),
          this.t.translate('dashboard.discoverQueuedDetail'),
        ),
      error: () => this.notify.error(this.t.translate('dashboard.discoverError')),
    });
  }
}
