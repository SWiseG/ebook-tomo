import { HttpClient } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { DecimalPipe, PercentPipe } from '@angular/common';
import { RouterLink } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { DashboardSummary } from '../../core/api.types';
import { NotificationService } from '../../core/notification.service';
import { Loading } from '../../shared/loading';

@Component({
  selector: 'app-dashboard',
  imports: [DecimalPipe, PercentPipe, RouterLink, ButtonModule, Loading],
  templateUrl: './dashboard.html',
  styleUrl: './dashboard.scss',
})
export class Dashboard {
  private readonly http = inject(HttpClient);
  private readonly notify = inject(NotificationService);

  readonly summary = signal<DashboardSummary | null>(null);
  readonly error = signal<string | null>(null);

  constructor() {
    this.http.get<DashboardSummary>('/api/v1/dashboard/summary').subscribe({
      next: (data) => this.summary.set(data),
      error: () => this.error.set('Falha ao carregar o resumo.'),
    });
  }

  discover(): void {
    this.http.post('/api/v1/niches/discover', {}).subscribe({
      next: () =>
        this.notify.success('Descoberta enfileirada', 'Os nichos aparecerão em instantes.'),
      error: () => this.notify.error('Falha ao disparar a descoberta.'),
    });
  }
}
