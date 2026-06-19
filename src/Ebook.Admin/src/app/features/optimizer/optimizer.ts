import { HttpClient } from '@angular/common/http';
import { Component, effect, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { ButtonModule } from 'primeng/button';
import { AgGridAngular } from 'ag-grid-angular';
import { ColDef, GridApi, GridReadyEvent, ICellRendererParams } from 'ag-grid-community';
import { tomoAgTheme } from '../../shared/ag-grid/tomo-ag-theme';
import { OptimizationDecision, OptimizationRun } from '../../core/api.types';
import { NotificationService } from '../../core/notification.service';
import { Loading } from '../../shared/loading';

type Severity = 'success' | 'info' | 'warn' | 'danger' | 'secondary' | 'contrast' | undefined;

@Component({
  selector: 'app-optimizer',
  imports: [DatePipe, FormsModule, TranslocoDirective, AgGridAngular, ButtonModule, Loading],
  templateUrl: './optimizer.html',
  styleUrl: './optimizer.scss',
})
export class Optimizer {
  private readonly http = inject(HttpClient);
  private readonly notify = inject(NotificationService);
  private readonly t = inject(TranslocoService);

  readonly runs = signal<OptimizationRun[] | null>(null);
  readonly selected = signal<OptimizationRun | null>(null);
  readonly decisions = signal<OptimizationDecision[]>([]);
  readonly busy = signal(false);

  quickFilter = '';

  // AG Grid
  readonly theme = tomoAgTheme;
  gridApi?: GridApi<OptimizationDecision>;

  readonly defaultColDef: ColDef = {
    sortable: true,
    resizable: true,
    suppressMovable: true,
    suppressHeaderMenuButton: true,
    filter: true,
  };

  readonly colDefs: ColDef<OptimizationDecision>[] = this.buildCols();

  readonly gridOptions = {
    context: {
      approve: (d: OptimizationDecision) => this.approve(d),
      veto: (d: OptimizationDecision) => this.veto(d),
      isBusy: () => this.busy(),
    },
  };

  constructor() {
    this.loadRuns();
    effect(() => {
      this.busy();
      this.gridApi?.refreshCells({ columns: ['actions'], force: true });
    });
  }

  onGridReady(e: GridReadyEvent<OptimizationDecision>): void { this.gridApi = e.api; }

  onSearch(): void {
    this.gridApi?.setGridOption('quickFilterText', this.quickFilter);
  }

  private buildCols(): ColDef<OptimizationDecision>[] {
    const t = this.t;
    return [
      {
        headerName: t.translate('optimizer.col.product'),
        flex: 2,
        minWidth: 150,
        valueGetter: (p) => p.data?.productTitle ?? '',
        cellRenderer: (params: ICellRendererParams<OptimizationDecision>) =>
          `<span><strong>${params.data!.productTitle}</strong><div class="mono" style="font-size:0.78rem;color:var(--p-text-muted-color);white-space:normal">${params.data!.rationale}</div></span>`,
      },
      {
        headerName: t.translate('optimizer.col.decision'),
        field: 'decision',
        width: 110,
        cellRenderer: (params: ICellRendererParams<OptimizationDecision>) => {
          const sev = this.decisionSeverity(params.value as string);
          return `<span class="tomo-badge tomo-badge--${sev ?? 'default'}">${t.translate('status.decision.' + params.value)}</span>`;
        },
      },
      {
        headerName: t.translate('optimizer.col.status'),
        field: 'status',
        width: 120,
        cellRenderer: (params: ICellRendererParams<OptimizationDecision>) => {
          const sev = this.statusSeverity(params.value as string);
          return `<span class="tomo-badge tomo-badge--${sev ?? 'default'}">${t.translate('status.decisionStatus.' + params.value)}</span>`;
        },
      },
      {
        colId: 'actions',
        headerName: '',
        width: 130,
        sortable: false,
        resizable: false,
        cellRenderer: (params: ICellRendererParams<OptimizationDecision>) => {
          if (params.data?.status !== 'Proposed') return '';
          const ctx = params.context as {
            approve: (d: OptimizationDecision) => void;
            veto: (d: OptimizationDecision) => void;
            isBusy: () => boolean;
          };
          const busy = ctx.isBusy();
          const div = document.createElement('div');
          div.className = 'tomo-row-actions';

          const btnApprove = document.createElement('button');
          btnApprove.className = 'tomo-row-btn tomo-row-btn--primary';
          btnApprove.disabled = busy;
          btnApprove.textContent = t.translate('optimizer.approve');
          btnApprove.addEventListener('click', (e) => { e.stopPropagation(); ctx.approve(params.data!); });
          div.appendChild(btnApprove);

          const btnVeto = document.createElement('button');
          btnVeto.className = 'tomo-row-btn tomo-row-btn--danger';
          btnVeto.disabled = busy;
          btnVeto.innerHTML = '<span class="pi pi-times"></span>';
          btnVeto.addEventListener('click', (e) => { e.stopPropagation(); ctx.veto(params.data!); });
          div.appendChild(btnVeto);

          return div;
        },
      },
    ];
  }

  loadRuns(): void {
    this.http.get<OptimizationRun[]>('/api/v1/optimizer/runs').subscribe({
      next: (list) => {
        this.runs.set(list);
        if (list.length && !this.selected()) this.select(list[0]);
      },
      error: () => this.notify.error(this.t.translate('optimizer.loadError')),
    });
  }

  run(): void {
    this.busy.set(true);
    this.http.post('/api/v1/optimizer/run', {}).subscribe({
      next: () => {
        this.notify.success(this.t.translate('optimizer.cycleRun'), this.t.translate('optimizer.cycleRunDetail'));
        this.selected.set(null);
        this.loadRuns();
        this.busy.set(false);
      },
      error: () => { this.notify.error(this.t.translate('optimizer.runError')); this.busy.set(false); },
    });
  }

  select(runItem: OptimizationRun): void {
    this.selected.set(runItem);
    this.http.get<OptimizationDecision[]>(`/api/v1/optimizer/runs/${runItem.id}/decisions`).subscribe({
      next: (list) => this.decisions.set(list),
      error: () => this.decisions.set([]),
    });
  }

  approve(d: OptimizationDecision): void {
    this.act(d, this.http.post(`/api/v1/optimizer/decisions/${d.id}/approve`, {}), this.t.translate('optimizer.approved'));
  }

  veto(d: OptimizationDecision): void {
    this.act(d, this.http.post(`/api/v1/optimizer/decisions/${d.id}/veto`, {}), this.t.translate('optimizer.vetoed'));
  }

  decisionSeverity(decision: string): Severity {
    switch (decision) {
      case 'Scale': return 'success';
      case 'Kill': return 'danger';
      case 'Iterate': return 'warn';
      default: return 'secondary';
    }
  }

  statusSeverity(status: string): Severity {
    switch (status) {
      case 'Executed': return 'success';
      case 'Approved': return 'info';
      case 'Vetoed': return 'danger';
      default: return 'secondary';
    }
  }

  private act(d: OptimizationDecision, request: ReturnType<HttpClient['post']>, ok: string): void {
    this.busy.set(true);
    request.subscribe({
      next: () => {
        this.notify.success(ok);
        const run = this.selected();
        if (run) this.select(run);
        this.loadRuns();
        this.busy.set(false);
      },
      error: () => {
        this.notify.error(this.t.translate('common.actionFailed'), this.t.translate('common.actionFailedDetail'));
        this.busy.set(false);
      },
    });
  }
}
