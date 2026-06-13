import { HttpClient } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { JobItem, JobsPage } from '../../core/api.types';

const CHIP: Record<JobItem['status'], string> = {
  Pending: 'chip--info',
  Running: 'chip--primary',
  Succeeded: 'chip--success',
  Dead: 'chip--danger',
};

@Component({
  selector: 'app-jobs',
  imports: [DatePipe],
  templateUrl: './jobs.html',
  styleUrl: './jobs.scss',
})
export class Jobs {
  private readonly http = inject(HttpClient);

  readonly page = signal<JobsPage | null>(null);
  readonly statusFilter = signal<string>('');
  readonly statuses = ['', 'Pending', 'Running', 'Succeeded', 'Dead'];

  constructor() {
    this.load();
  }

  chip(status: JobItem['status']): string {
    return CHIP[status];
  }

  load(): void {
    const status = this.statusFilter();
    const query = status ? `&status=${status}` : '';
    this.http
      .get<JobsPage>(`/api/v1/jobs?page=1&size=50${query}`)
      .subscribe((data) => this.page.set(data));
  }

  filter(status: string): void {
    this.statusFilter.set(status);
    this.load();
  }

  retry(job: JobItem): void {
    this.http.post(`/api/v1/jobs/${job.id}/retry`, {}).subscribe(() => this.load());
  }
}
