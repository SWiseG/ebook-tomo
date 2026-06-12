import { HttpClient } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { JobItem, JobsPage } from '../../core/api.types';

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

  constructor() {
    this.load();
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
