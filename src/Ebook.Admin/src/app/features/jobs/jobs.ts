import { HttpClient } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { debounceTime } from 'rxjs';
import { FormsModule } from '@angular/forms';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { ButtonModule } from 'primeng/button';
import { SelectButtonModule } from 'primeng/selectbutton';
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
    DatePipe,
    FormsModule,
    TranslocoDirective,
    TableModule,
    TagModule,
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
  // Recalcula os rótulos ao trocar de idioma (depende de `language.current()`).
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

  constructor() {
    this.load();

    // Atualização ao vivo: cada mudança de job recarrega a lista (debounced p/ rajadas).
    this.realtime.jobChanged$
      .pipe(debounceTime(600), takeUntilDestroyed())
      .subscribe(() => this.load());
  }

  severity(status: JobItem['status']): Severity {
    return SEVERITY[status];
  }

  load(): void {
    const query = this.status ? `&status=${this.status}` : '';
    this.http
      .get<JobsPage>(`/api/v1/jobs?page=1&size=50${query}`)
      .subscribe((data) => this.page.set(data));
  }

  retry(job: JobItem): void {
    this.http.post(`/api/v1/jobs/${job.id}/retry`, {}).subscribe({
      next: () => {
        this.notify.success(this.t.translate('jobs.retried'), job.type);
        this.load();
      },
      error: () => this.notify.error(this.t.translate('jobs.retryError')),
    });
  }
}
