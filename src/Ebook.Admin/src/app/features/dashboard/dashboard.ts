import { HttpClient } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { DecimalPipe, PercentPipe } from '@angular/common';
import { DashboardSummary } from '../../core/api.types';

@Component({
  selector: 'app-dashboard',
  imports: [DecimalPipe, PercentPipe],
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.scss',
})
export class Dashboard {
  private readonly http = inject(HttpClient);

  readonly summary = signal<DashboardSummary | null>(null);
  readonly error = signal<string | null>(null);

  constructor() {
    this.http.get<DashboardSummary>('/api/v1/dashboard/summary').subscribe({
      next: (data) => this.summary.set(data),
      error: () => this.error.set('Falha ao carregar o resumo.'),
    });
  }
}
