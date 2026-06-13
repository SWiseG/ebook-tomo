import { HttpClient } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { Router } from '@angular/router';
import { NicheItem, NicheStatus } from '../../core/api.types';

const CHIP: Record<NicheStatus, string> = {
  Candidate: 'chip--info',
  Selected: 'chip--primary',
  Active: 'chip--success',
  Discarded: 'chip',
};

@Component({
  selector: 'app-niches',
  imports: [DatePipe, DecimalPipe],
  templateUrl: './niches.html',
  styleUrl: './niches.scss',
})
export class Niches {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);

  readonly niches = signal<NicheItem[] | null>(null);
  readonly statusFilter = signal<string>('');
  readonly notice = signal<string | null>(null);
  readonly error = signal<string | null>(null);
  readonly busy = signal<string | null>(null);

  readonly statuses = ['', 'Candidate', 'Selected', 'Active', 'Discarded'];

  constructor() {
    this.load();
  }

  chip(status: NicheStatus): string {
    return CHIP[status];
  }

  load(): void {
    const status = this.statusFilter();
    const query = status ? `?status=${status}` : '';
    this.http.get<NicheItem[]>(`/api/v1/niches${query}`).subscribe({
      next: (data) => this.niches.set(data),
      error: () => this.error.set('Falha ao carregar nichos.'),
    });
  }

  filter(status: string): void {
    this.statusFilter.set(status);
    this.load();
  }

  discover(): void {
    this.notice.set(null);
    this.http.post('/api/v1/niches/discover', {}).subscribe({
      next: () => this.notice.set('Descoberta enfileirada — atualize em instantes.'),
      error: () => this.error.set('Não foi possível disparar a descoberta.'),
    });
  }

  approve(n: NicheItem): void {
    this.act(n.id, this.http.post(`/api/v1/niches/${n.id}/approve`, {}));
  }

  discard(n: NicheItem): void {
    this.act(n.id, this.http.post(`/api/v1/niches/${n.id}/discard`, {}));
  }

  generate(n: NicheItem): void {
    this.busy.set(n.id);
    this.http
      .post<{ productId: string; slug: string }>('/api/v1/products', { nicheId: n.id })
      .subscribe({
        next: (r) => void this.router.navigate(['/products', r.productId]),
        error: () => {
          this.error.set('Falha ao gerar produto.');
          this.busy.set(null);
        },
      });
  }

  private act(id: string, request: ReturnType<HttpClient['post']>): void {
    this.busy.set(id);
    request.subscribe({
      next: () => {
        this.busy.set(null);
        this.load();
      },
      error: () => {
        this.error.set('Ação falhou (transição inválida?).');
        this.busy.set(null);
      },
    });
  }
}
