import { HttpClient } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { DecimalPipe, PercentPipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { DashboardSummary } from '../../core/api.types';

@Component({
  selector: 'app-dashboard',
  imports: [DecimalPipe, PercentPipe, RouterLink],
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.scss',
})
export class Dashboard {
  private readonly http = inject(HttpClient);

  readonly summary = signal<DashboardSummary | null>(null);
  readonly error = signal<string | null>(null);
  readonly notice = signal<string | null>(null);

  constructor() {
    this.load();
  }

  load(): void {
    this.http.get<DashboardSummary>('/api/v1/dashboard/summary').subscribe({
      next: (data) => this.summary.set(data),
      error: () => this.error.set('Falha ao carregar o resumo.'),
    });
  }

  discover(): void {
    this.notice.set(null);
    this.http.post('/api/v1/niches/discover', {}).subscribe({
      next: () => this.notice.set('Descoberta enfileirada — os nichos aparecerão em instantes.'),
      error: () => this.error.set('Não foi possível disparar a descoberta.'),
    });
  }
}
