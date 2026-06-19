import { DecimalPipe } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { ButtonModule } from 'primeng/button';
import { TooltipModule } from 'primeng/tooltip';
import { AgGridAngular } from 'ag-grid-angular';
import { ColDef, GridReadyEvent, ICellRendererParams } from 'ag-grid-community';
import { tomoAgTheme } from '../../shared/ag-grid/tomo-ag-theme';
import { MediaTelemetry as MediaTelemetryData, MediaProviderStat } from '../../core/api.types';
import { NotificationService } from '../../core/notification.service';
import { Loading } from '../../shared/loading';

type Severity = 'success' | 'info' | 'warn' | 'danger' | 'secondary' | undefined;

@Component({
  selector: 'app-media-telemetry',
  imports: [DecimalPipe, TranslocoDirective, AgGridAngular, ButtonModule, TooltipModule, Loading],
  templateUrl: './media-telemetry.html',
  styleUrl: './media-telemetry.scss',
})
export class MediaTelemetry {
  private readonly http = inject(HttpClient);
  private readonly notify = inject(NotificationService);
  private readonly t = inject(TranslocoService);

  readonly data = signal<MediaTelemetryData | null>(null);
  readonly loading = signal(false);

  // AG Grid
  readonly theme = tomoAgTheme;

  readonly defaultColDef: ColDef = {
    sortable: true,
    resizable: true,
    suppressMovable: true,
    suppressHeaderMenuButton: true,
  };

  readonly colDefs: ColDef<MediaProviderStat>[] = this.buildCols();

  constructor() { this.load(); }

  onGridReady(_e: GridReadyEvent<MediaProviderStat>): void {}

  private buildCols(): ColDef<MediaProviderStat>[] {
    const t = this.t;
    return [
      {
        headerName: t.translate('media.col.provider'),
        field: 'provider',
        flex: 1,
        minWidth: 100,
        cellRenderer: (p: ICellRendererParams<MediaProviderStat>) =>
          `<strong>${p.value ?? ''}</strong>`,
      },
      {
        headerName: t.translate('media.col.today'),
        width: 130,
        valueGetter: (p) => p.data ? this.quotaLabel(p.data) : '—',
        tooltipValueGetter: (p) =>
          p.data?.dailyLimit && p.data.dailyLimit > 0
            ? t.translate('media.quotaOf', { limit: p.data.dailyLimit })
            : '',
      },
      {
        headerName: t.translate('media.col.month'),
        field: 'generatedThisMonth',
        width: 140,
        valueFormatter: (p) => (p.value != null ? new Intl.NumberFormat('pt-BR').format(p.value as number) : '—'),
      },
      {
        headerName: t.translate('media.col.avgMs'),
        field: 'avgDurationMsToday',
        width: 120,
        valueFormatter: (p) =>
          p.value && (p.value as number) > 0
            ? new Intl.NumberFormat('pt-BR').format(p.value as number) + ' ms'
            : '—',
      },
      {
        headerName: t.translate('media.col.bytes'),
        field: 'totalBytesToday',
        width: 120,
        valueFormatter: (p) => this.formatBytes(p.value as number),
      },
      {
        headerName: t.translate('media.col.status'),
        width: 140,
        sortable: false,
        cellRenderer: (p: ICellRendererParams<MediaProviderStat>) => {
          const stat = p.data!;
          const sev = this.providerSeverity(stat);
          const label = this.providerStatusLabel(stat);
          return `<span class="tomo-badge tomo-badge--${sev ?? 'default'}">${label}</span>`;
        },
      },
    ];
  }

  load(): void {
    this.loading.set(true);
    this.http.get<MediaTelemetryData>('/api/v1/media/telemetry').subscribe({
      next: (d) => { this.data.set(d); this.loading.set(false); },
      error: () => { this.notify.error(this.t.translate('media.loadError')); this.loading.set(false); },
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
    if (stat.generatedToday > 0) return this.t.translate('media.status.active');
    return this.t.translate('media.status.idle');
  }

  formatBytes(bytes: number): string {
    if (!bytes || bytes === 0) return '0 B';
    const k = 1024;
    const sizes = ['B', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return `${parseFloat((bytes / Math.pow(k, i)).toFixed(1))} ${sizes[i]}`;
  }

  quotaLabel(stat: MediaProviderStat): string {
    return stat.dailyLimit > 0 ? `${stat.generatedToday} / ${stat.dailyLimit}` : `${stat.generatedToday}`;
  }
}
