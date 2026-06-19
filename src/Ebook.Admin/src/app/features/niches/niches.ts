import { HttpClient } from '@angular/common/http';
import { Component, computed, effect, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { ButtonModule } from 'primeng/button';
import { SelectButtonModule } from 'primeng/selectbutton';
import { ConfirmationService } from 'primeng/api';
import { AgGridAngular } from 'ag-grid-angular';
import { ColDef, GridApi, GridReadyEvent, ICellRendererParams } from 'ag-grid-community';
import { tomoAgTheme } from '../../shared/ag-grid/tomo-ag-theme';
import { NicheItem, NicheStatus } from '../../core/api.types';
import { LanguageService } from '../../core/language.service';
import { NotificationService } from '../../core/notification.service';
import { Loading } from '../../shared/loading';

type Severity = 'success' | 'info' | 'warn' | 'danger' | 'secondary' | 'contrast' | undefined;

const SEVERITY: Record<NicheStatus, Severity> = {
  Candidate: 'info',
  Selected: undefined,
  Active: 'success',
  Discarded: 'secondary',
};

@Component({
  selector: 'app-niches',
  imports: [
    FormsModule,
    TranslocoDirective,
    AgGridAngular,
    ButtonModule,
    SelectButtonModule,
    Loading,
  ],
  templateUrl: './niches.html',
  styleUrl: './niches.scss',
})
export class Niches {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly notify = inject(NotificationService);
  private readonly confirm = inject(ConfirmationService);
  private readonly t = inject(TranslocoService);
  private readonly language = inject(LanguageService);

  readonly niches = signal<NicheItem[] | null>(null);
  readonly busy = signal<string | null>(null);

  status = '';
  readonly statusOptions = computed(() => {
    this.language.current();
    return [
      { label: this.t.translate('niches.filter.all'), value: '' },
      { label: this.t.translate('niches.filter.candidate'), value: 'Candidate' },
      { label: this.t.translate('niches.filter.selected'), value: 'Selected' },
      { label: this.t.translate('niches.filter.active'), value: 'Active' },
      { label: this.t.translate('niches.filter.discarded'), value: 'Discarded' },
    ];
  });

  quickFilter = '';

  // AG Grid
  readonly theme = tomoAgTheme;
  gridApi?: GridApi<NicheItem>;

  readonly defaultColDef: ColDef = {
    sortable: true,
    resizable: true,
    suppressMovable: true,
    suppressHeaderMenuButton: true,
    filter: true,
  };

  readonly colDefs: ColDef<NicheItem>[] = this.buildCols();

  readonly gridOptions = {
    context: {
      approve: (n: NicheItem) => this.approve(n),
      generate: (n: NicheItem) => this.generate(n),
      discard: (n: NicheItem) => this.discard(n),
      isBusy: (id: string) => this.busy() === id,
    },
  };

  constructor() {
    this.load();
    // Quando busy muda, refaz as células de ação para atualizar estado disabled/loading.
    effect(() => {
      this.busy();
      this.gridApi?.refreshCells({ columns: ['actions'], force: true });
    });
  }

  onGridReady(e: GridReadyEvent<NicheItem>): void {
    this.gridApi = e.api;
  }

  onSearch(): void {
    this.gridApi?.setGridOption('quickFilterText', this.quickFilter);
  }

  private buildCols(): ColDef<NicheItem>[] {
    const t = this.t;
    const fmt = (d: string) =>
      new Date(d).toLocaleDateString('pt-BR', { day: '2-digit', month: '2-digit', year: '2-digit' });

    return [
      {
        headerName: t.translate('niches.col.niche'),
        flex: 2,
        minWidth: 160,
        valueGetter: (p) => p.data?.name ?? '',
        cellRenderer: (params: ICellRendererParams<NicheItem>) =>
          `<strong>${params.data!.name}</strong>`,
      },
      {
        headerName: t.translate('niches.col.score'),
        field: 'score',
        width: 90,
        valueFormatter: (p) => (p.value != null ? Number(p.value).toFixed(2) : '—'),
      },
      {
        headerName: t.translate('niches.col.cycle'),
        field: 'cycleNumber',
        width: 80,
      },
      {
        headerName: t.translate('niches.col.status'),
        field: 'status',
        width: 120,
        cellRenderer: (params: ICellRendererParams<NicheItem>) => {
          const sev = SEVERITY[params.value as NicheStatus];
          return `<span class="tomo-badge tomo-badge--${sev ?? 'default'}">${t.translate('status.niche.' + params.value)}</span>`;
        },
      },
      {
        headerName: t.translate('niches.col.discovered'),
        field: 'discoveredAtUtc',
        width: 110,
        valueFormatter: (p) => (p.value ? fmt(p.value as string) : '—'),
      },
      {
        colId: 'actions',
        headerName: '',
        width: 220,
        sortable: false,
        resizable: false,
        cellRenderer: (params: ICellRendererParams<NicheItem>) => {
          const ctx = params.context as {
            approve: (n: NicheItem) => void;
            generate: (n: NicheItem) => void;
            discard: (n: NicheItem) => void;
            isBusy: (id: string) => boolean;
          };
          const n = params.data!;
          const busy = ctx.isBusy(n.id);
          const div = document.createElement('div');
          div.className = 'tomo-row-actions';

          if (n.status === 'Candidate') {
            const btnApprove = document.createElement('button');
            btnApprove.className = 'tomo-row-btn';
            btnApprove.textContent = t.translate('niches.approve');
            btnApprove.disabled = busy;
            btnApprove.addEventListener('click', (e) => { e.stopPropagation(); ctx.approve(n); });
            div.appendChild(btnApprove);
          }

          const btnGen = document.createElement('button');
          btnGen.className = 'tomo-row-btn tomo-row-btn--primary';
          btnGen.disabled = busy;
          btnGen.innerHTML = busy
            ? `<span class="pi pi-spinner pi-spin"></span> ${t.translate('niches.generate')}`
            : `<span class="pi pi-sparkles"></span> ${t.translate('niches.generate')}`;
          btnGen.addEventListener('click', (e) => { e.stopPropagation(); ctx.generate(n); });
          div.appendChild(btnGen);

          if (n.status !== 'Discarded' && n.status !== 'Active') {
            const btnDiscard = document.createElement('button');
            btnDiscard.className = 'tomo-row-btn tomo-row-btn--danger';
            btnDiscard.disabled = busy;
            btnDiscard.innerHTML = '<span class="pi pi-trash"></span>';
            btnDiscard.addEventListener('click', (e) => { e.stopPropagation(); ctx.discard(n); });
            div.appendChild(btnDiscard);
          }

          return div;
        },
      },
    ];
  }

  severity(status: NicheStatus): Severity { return SEVERITY[status]; }

  load(): void {
    const query = this.status ? `?status=${this.status}` : '';
    this.http.get<NicheItem[]>(`/api/v1/niches${query}`).subscribe({
      next: (data) => this.niches.set(data),
      error: () => this.notify.error(this.t.translate('niches.loadError')),
    });
  }

  discover(): void {
    this.http.post('/api/v1/niches/discover', {}).subscribe({
      next: () => this.notify.success(
        this.t.translate('niches.discoverQueued'),
        this.t.translate('niches.discoverQueuedDetail'),
      ),
      error: () => this.notify.error(this.t.translate('niches.discoverError')),
    });
  }

  approve(n: NicheItem): void {
    this.act(n.id, this.http.post(`/api/v1/niches/${n.id}/approve`, {}), this.t.translate('niches.approved'));
  }

  discard(n: NicheItem): void {
    this.confirm.confirm({
      header: this.t.translate('niches.discardHeader'),
      message: this.t.translate('niches.discardMessage', { name: n.name }),
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: this.t.translate('niches.discardConfirm'),
      rejectLabel: this.t.translate('common.cancel'),
      acceptButtonStyleClass: 'p-button-danger',
      accept: () => this.act(n.id, this.http.post(`/api/v1/niches/${n.id}/discard`, {}), this.t.translate('niches.discarded')),
    });
  }

  generate(n: NicheItem): void {
    this.busy.set(n.id);
    this.http.post<{ productId: string; slug: string }>('/api/v1/products', { nicheId: n.id }).subscribe({
      next: (r) => void this.router.navigate(['/products', r.productId]),
      error: () => { this.notify.error(this.t.translate('niches.generateError')); this.busy.set(null); },
    });
  }

  private act(id: string, request: ReturnType<HttpClient['post']>, ok: string): void {
    this.busy.set(id);
    request.subscribe({
      next: () => { this.busy.set(null); this.notify.success(ok); this.load(); },
      error: () => {
        this.busy.set(null);
        this.notify.error(this.t.translate('common.actionFailed'), this.t.translate('common.actionFailedDetail'));
      },
    });
  }
}
