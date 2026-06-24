import { HttpClient } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { debounceTime } from 'rxjs';
import { FormsModule } from '@angular/forms';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { ButtonModule } from 'primeng/button';
import { SelectButtonModule } from 'primeng/selectbutton';
import { AgGridAngular } from 'ag-grid-angular';
import { ColDef, GridApi, GridReadyEvent, ICellRendererParams } from 'ag-grid-community';
import { tomoAgTheme } from '../../shared/ag-grid/tomo-ag-theme';
import { JobItem, JobsPage } from '../../core/api.types';
import { LanguageService } from '../../core/language.service';
import { NotificationService } from '../../core/notification.service';
import { RealtimeService } from '../../core/realtime.service';
import { Loading } from '../../shared/loading';

type Severity = 'success' | 'info' | 'warn' | 'danger' | 'secondary' | 'contrast' | undefined;

const SEVERITY: Record<JobItem['status'], Severity> = {
  Pending: 'info',
  Running: 'warn',
  Succeeded: 'success',
  Dead: 'danger',
};

@Component({
  selector: 'app-jobs',
  imports: [
    FormsModule,
    TranslocoDirective,
    AgGridAngular,
    ButtonModule,
    SelectButtonModule,
    Loading,
  ],
  templateUrl: './jobs.html',
  styleUrl: './jobs.scss',
})
export class Jobs {
  private readonly http = inject(HttpClient);
  private readonly notify = inject(NotificationService);
  private readonly realtime = inject(RealtimeService);
  private readonly t = inject(TranslocoService);
  private readonly language = inject(LanguageService);

  readonly page = signal<JobsPage | null>(null);

  status = '';
  readonly statusOptions = computed(() => {
    this.language.current();
    return [
      { label: this.t.translate('jobs.filter.all'), value: '' },
      { label: this.t.translate('jobs.filter.pending'), value: 'Pending' },
      { label: this.t.translate('jobs.filter.running'), value: 'Running' },
      { label: this.t.translate('jobs.filter.succeeded'), value: 'Succeeded' },
      { label: this.t.translate('jobs.filter.dead'), value: 'Dead' },
    ];
  });

  quickFilter = '';

  // AG Grid
  readonly theme = tomoAgTheme;
  gridApi?: GridApi<JobItem>;

  readonly defaultColDef: ColDef = {
    sortable: true,
    resizable: true,
    suppressMovable: true,
    suppressHeaderMenuButton: true,
    filter: true,
  };

  readonly colDefs: ColDef<JobItem>[] = this.buildCols();

  readonly gridOptions = {
    context: { retry: (job: JobItem) => this.retry(job) },
  };

  constructor() {
    this.load();
    this.realtime.jobChanged$
      .pipe(debounceTime(600), takeUntilDestroyed())
      .subscribe(() => this.load());
  }

  onGridReady(e: GridReadyEvent<JobItem>): void { this.gridApi = e.api; }

  onSearch(): void {
    this.gridApi?.setGridOption('quickFilterText', this.quickFilter);
  }

  private buildCols(): ColDef<JobItem>[] {
    const t = this.t;
    const fmt = (d: string) =>
      new Date(d).toLocaleString('pt-BR', { day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit' });

    return [
      {
        headerName: t.translate('jobs.col.type'),
        field: 'type',
        flex: 1,
        minWidth: 140,
        cellStyle: { fontFamily: 'JetBrains Mono, monospace', fontSize: '0.82rem' },
      },
      {
        headerName: t.translate('jobs.col.status'),
        field: 'status',
        width: 120,
        cellRenderer: (params: ICellRendererParams<JobItem>) => {
          const sev = SEVERITY[params.value as JobItem['status']];
          return `<span class="tomo-badge tomo-badge--${sev ?? 'default'}">${t.translate('status.job.' + params.value)}</span>`;
        },
      },
      {
        headerName: t.translate('jobs.col.attempts'),
        field: 'attempts',
        width: 90,
      },
      {
        headerName: t.translate('jobs.col.created'),
        field: 'createdAtUtc',
        width: 115,
        valueFormatter: (p) => (p.value ? fmt(p.value as string) : '—'),
      },
      {
        headerName: t.translate('jobs.col.error'),
        field: 'lastError',
        flex: 1,
        minWidth: 120,
        cellStyle: { fontSize: '0.8rem', color: 'var(--p-text-muted-color)' },
        valueFormatter: (p) => p.value ?? '—',
      },
      {
        headerName: '',
        width: 115,
        sortable: false,
        resizable: false,
        cellRenderer: (params: ICellRendererParams<JobItem>) => {
          const s = params.data?.status;
          if (s !== 'Dead' && s !== 'Succeeded') return '';
          const ctx = params.context as { retry: (j: JobItem) => void };
          const btn = document.createElement('button');
          btn.className = s === 'Dead' ? 'tomo-row-btn tomo-row-btn--danger' : 'tomo-row-btn';
          btn.innerHTML = `<span class="pi pi-refresh"></span> ${t.translate('jobs.retry')}`;
          btn.addEventListener('click', (e) => { e.stopPropagation(); ctx.retry(params.data!); });
          return btn;
        },
      },
    ];
  }

  severity(status: JobItem['status']): Severity { return SEVERITY[status]; }

  load(): void {
    const query = this.status ? `&status=${this.status}` : '';
    this.http.get<JobsPage>(`/api/v1/jobs?page=1&size=50${query}`).subscribe((data) => this.page.set(data));
  }

  retry(job: JobItem): void {
    this.http.post(`/api/v1/jobs/${job.id}/retry`, {}).subscribe({
      next: () => { this.notify.success(this.t.translate('jobs.retried'), job.type); this.load(); },
      error: () => this.notify.error(this.t.translate('jobs.retryError')),
    });
  }
}
