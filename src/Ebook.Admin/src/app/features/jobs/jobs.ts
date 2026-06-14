import { HttpClient } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { ButtonModule } from 'primeng/button';
import { SelectButtonModule } from 'primeng/selectbutton';
import { JobItem, JobsPage } from '../../core/api.types';
import { NotificationService } from '../../core/notification.service';
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
  imports: [DatePipe, FormsModule, TableModule, TagModule, ButtonModule, SelectButtonModule, Loading],
  templateUrl: './jobs.html',
  styleUrl: './jobs.scss',
})
export class Jobs {
  private readonly http = inject(HttpClient);
  private readonly notify = inject(NotificationService);

  readonly page = signal<JobsPage | null>(null);

  status = '';
  readonly statusOptions = [
    { label: 'Todos', value: '' },
    { label: 'Pendentes', value: 'Pending' },
    { label: 'Rodando', value: 'Running' },
    { label: 'Sucesso', value: 'Succeeded' },
    { label: 'Dead', value: 'Dead' },
  ];

  constructor() {
    this.load();
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
        this.notify.success('Job reenfileirado', job.type);
        this.load();
      },
      error: () => this.notify.error('Falha ao reprocessar o job.'),
    });
  }
}
