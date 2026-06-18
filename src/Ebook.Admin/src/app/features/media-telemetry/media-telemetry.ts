import { DecimalPipe } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { ButtonModule } from 'primeng/button';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { TooltipModule } from 'primeng/tooltip';
import { MediaTelemetry as MediaTelemetryData, MediaProviderStat } from '../../core/api.types';
import { NotificationService } from '../../core/notification.service';
import { Loading } from '../../shared/loading';

type Severity = 'success' | 'info' | 'warn' | 'danger' | 'secondary' | undefined;

@Component({
  selector: 'app-media-telemetry',
  imports: [DecimalPipe, TranslocoDirective, TableModule, TagModule, ButtonModule, TooltipModule, Loading],
  templateUrl: './media-telemetry.html',
  styleUrl: './media-telemetry.scss',
})
export class MediaTelemetry {
  private readonly http = inject(HttpClient);
  private readonly notify = inject(NotificationService);
  private readonly t = inject(TranslocoService);

  readonly data = signal<MediaTelemetryData | null>(null);
  readonly loading = signal(false);

  constructor() {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.http.get<MediaTelemetryData>('/api/v1/media/telemetry').subscribe({
      next: (d) => { this.data.set(d); this.loading.set(false); },
      error: () => {
        this.notify.error(this.t.translate('media.loadError'));
        this.loading.set(false);
      },
    });
  }

  providerSeverity(stat: MediaProviderStat): Severity {
    if (stat.dailyLimit > 0 && stat.generatedToday >= stat.dailyLimit) return 'danger';
    if (stat.dailyLimit > 0 && stat.generatedToday >= stat.dailyLimit * 0.8) return 'warn';
    if (stat.generatedToday > 0) return 'success';
    return 'secondary';
  }

  providerStatusLabel(stat: MediaProviderStat): string {
    if (stat.dailyLimit > 0 && stat.generatedToday >= stat.dailyLimit)
      return this.t.translate('media.status.quotaExhausted');
    if (stat.generatedToday > 0)
      return this.t.translate('media.status.active');
    return this.t.translate('media.status.idle');
  }

  formatBytes(bytes: number): string {
    if (bytes === 0) return '0 B';
    const k = 1024;
    const sizes = ['B', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return `${parseFloat((bytes / Math.pow(k, i)).toFixed(1))} ${sizes[i]}`;
  }

  quotaLabel(stat: MediaProviderStat): string {
    return stat.dailyLimit > 0
      ? `${stat.generatedToday} / ${stat.dailyLimit}`
      : `${stat.generatedToday}`;
  }
}
