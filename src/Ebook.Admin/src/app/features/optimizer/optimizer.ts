import { HttpClient } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { ButtonModule } from 'primeng/button';
import { OptimizationDecision, OptimizationRun } from '../../core/api.types';
import { NotificationService } from '../../core/notification.service';
import { Loading } from '../../shared/loading';

type Severity = 'success' | 'info' | 'warn' | 'danger' | 'secondary' | 'contrast' | undefined;

@Component({
  selector: 'app-optimizer',
  imports: [DatePipe, TranslocoDirective, TableModule, TagModule, ButtonModule, Loading],
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

  constructor() {
    this.loadRuns();
  }

  loadRuns(): void {
    this.http.get<OptimizationRun[]>('/api/v1/optimizer/runs').subscribe({
      next: (list) => {
        this.runs.set(list);
        if (list.length && !this.selected()) {
          this.select(list[0]);
        }
      },
      error: () => this.notify.error(this.t.translate('optimizer.loadError')),
    });
  }

  run(): void {
    this.busy.set(true);
    this.http.post('/api/v1/optimizer/run', {}).subscribe({
      next: () => {
        this.notify.success(
          this.t.translate('optimizer.cycleRun'),
          this.t.translate('optimizer.cycleRunDetail'),
        );
        this.selected.set(null);
        this.loadRuns();
        this.busy.set(false);
      },
      error: () => {
        this.notify.error(this.t.translate('optimizer.runError'));
        this.busy.set(false);
      },
    });
  }

  select(runItem: OptimizationRun): void {
    this.selected.set(runItem);
    this.http
      .get<OptimizationDecision[]>(`/api/v1/optimizer/runs/${runItem.id}/decisions`)
      .subscribe({ next: (list) => this.decisions.set(list), error: () => this.decisions.set([]) });
  }

  approve(d: OptimizationDecision): void {
    this.act(
      d,
      this.http.post(`/api/v1/optimizer/decisions/${d.id}/approve`, {}),
      this.t.translate('optimizer.approved'),
    );
  }

  veto(d: OptimizationDecision): void {
    this.act(
      d,
      this.http.post(`/api/v1/optimizer/decisions/${d.id}/veto`, {}),
      this.t.translate('optimizer.vetoed'),
    );
  }

  decisionSeverity(decision: string): Severity {
    switch (decision) {
      case 'Scale':
        return 'success';
      case 'Kill':
        return 'danger';
      case 'Iterate':
        return 'warn';
      default:
        return 'secondary';
    }
  }

  statusSeverity(status: string): Severity {
    switch (status) {
      case 'Executed':
        return 'success';
      case 'Approved':
        return 'info';
      case 'Vetoed':
        return 'danger';
      default:
        return 'secondary';
    }
  }

  private act(d: OptimizationDecision, request: ReturnType<HttpClient['post']>, ok: string): void {
    this.busy.set(true);
    request.subscribe({
      next: () => {
        this.notify.success(ok);
        const run = this.selected();
        if (run) {
          this.select(run);
        }
        this.loadRuns();
        this.busy.set(false);
      },
      error: () => {
        this.notify.error(
          this.t.translate('common.actionFailed'),
          this.t.translate('common.actionFailedDetail'),
        );
        this.busy.set(false);
      },
    });
  }
}
