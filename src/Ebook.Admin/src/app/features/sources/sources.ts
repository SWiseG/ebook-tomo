import { DecimalPipe } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { ButtonModule } from 'primeng/button';
import { TooltipModule } from 'primeng/tooltip';
import { AgGridAngular } from 'ag-grid-angular';
import { ColDef, GridReadyEvent, ICellRendererParams } from 'ag-grid-community';
import { tomoAgTheme } from '../../shared/ag-grid/tomo-ag-theme';
import { SourcesTelemetry, SourceStat } from '../../core/api.types';
import { NotificationService } from '../../core/notification.service';
import { Loading } from '../../shared/loading';

@Component({
  selector: 'app-sources',
  imports: [DecimalPipe, FormsModule, TranslocoDirective, AgGridAngular, ButtonModule, TooltipModule, Loading],
  templateUrl: './sources.html',
  styleUrl: './sources.scss',
})
export class Sources {
  private readonly http = inject(HttpClient);
  private readonly notify = inject(NotificationService);
  private readonly t = inject(TranslocoService);

  readonly data = signal<SourcesTelemetry | null>(null);
  readonly loading = signal(false);

  quickFilter = '';

  readonly theme = tomoAgTheme;
  gridApi?: import('ag-grid-community').GridApi<SourceStat>;

  readonly defaultColDef: ColDef = {
    sortable: true,
    resizable: true,
    suppressMovable: true,
    suppressHeaderMenuButton: true,
    filter: true,
  };

  readonly colDefs: ColDef<SourceStat>[] = this.buildCols();

  constructor() { this.load(); }

  onGridReady(e: GridReadyEvent<SourceStat>): void { this.gridApi = e.api; }

  onSearch(): void {
    this.gridApi?.setGridOption('quickFilterText', this.quickFilter);
  }

  load(): void {
    this.loading.set(true);
    this.http.get<SourcesTelemetry>('/api/v1/sources/telemetry').subscribe({
      next: (d) => { this.data.set(d); this.loading.set(false); },
      error: () => { this.notify.error(this.t.translate('sources.loadError')); this.loading.set(false); },
    });
  }

  private buildCols(): ColDef<SourceStat>[] {
    const t = this.t;
    return [
      {
        headerName: t.translate('sources.col.provider'),
        field: 'provider',
        flex: 1,
        minWidth: 120,
        cellRenderer: (p: ICellRendererParams<SourceStat>) => `<strong>${p.value ?? ''}</strong>`,
      },
      {
        headerName: t.translate('sources.col.kind'),
        field: 'kind',
        width: 120,
        cellRenderer: (p: ICellRendererParams<SourceStat>) => {
          const k = (p.value as string) ?? '';
          const sev = k === 'Texto' ? 'info' : 'success';
          return `<span class="tomo-badge tomo-badge--${sev}">${k}</span>`;
        },
      },
      {
        headerName: t.translate('sources.col.today'),
        field: 'generatedToday',
        width: 110,
        valueFormatter: (p) => (p.value != null ? new Intl.NumberFormat('pt-BR').format(p.value as number) : '—'),
      },
      {
        headerName: t.translate('sources.col.month'),
        field: 'generatedThisMonth',
        width: 120,
        valueFormatter: (p) => (p.value != null ? new Intl.NumberFormat('pt-BR').format(p.value as number) : '—'),
      },
      {
        headerName: t.translate('sources.col.volume'),
        width: 160,
        sortable: false,
        valueGetter: (p) => (p.data ? this.volumeLabel(p.data) : '—'),
      },
      {
        headerName: t.translate('sources.col.avgMs'),
        field: 'avgDurationMsToday',
        width: 120,
        valueFormatter: (p) =>
          p.value && (p.value as number) > 0
            ? new Intl.NumberFormat('pt-BR').format(p.value as number) + ' ms'
            : '—',
      },
    ];
  }

  volumeLabel(s: SourceStat): string {
    return s.kind === 'Texto'
      ? `${new Intl.NumberFormat('pt-BR').format(s.tokensToday)} tokens`
      : this.formatBytes(s.bytesToday);
  }

  formatBytes(bytes: number): string {
    if (!bytes || bytes === 0) return '0 B';
    const k = 1024;
    const sizes = ['B', 'KB', 'MB', 'GB'];
    const i = Math.floor(Math.log(bytes) / Math.log(k));
    return `${parseFloat((bytes / Math.pow(k, i)).toFixed(1))} ${sizes[i]}`;
  }
}
