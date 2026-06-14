import { HttpClient } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { ButtonModule } from 'primeng/button';
import { SelectButtonModule } from 'primeng/selectbutton';
import { MessageService } from 'primeng/api';
import { NicheItem, NicheStatus } from '../../core/api.types';

type Severity = 'success' | 'info' | 'warn' | 'danger' | 'secondary' | 'contrast' | undefined;

const SEVERITY: Record<NicheStatus, Severity> = {
  Candidate: 'info',
  Selected: undefined, // cor primária
  Active: 'success',
  Discarded: 'secondary',
};

@Component({
  selector: 'app-niches',
  imports: [DatePipe, DecimalPipe, FormsModule, TableModule, TagModule, ButtonModule, SelectButtonModule],
  templateUrl: './niches.html',
  styleUrl: './niches.scss',
})
export class Niches {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly messages = inject(MessageService);

  readonly niches = signal<NicheItem[] | null>(null);
  readonly busy = signal<string | null>(null);

  status = '';
  readonly statusOptions = [
    { label: 'Todos', value: '' },
    { label: 'Candidatos', value: 'Candidate' },
    { label: 'Selecionados', value: 'Selected' },
    { label: 'Ativos', value: 'Active' },
    { label: 'Descartados', value: 'Discarded' },
  ];

  constructor() {
    this.load();
  }

  severity(status: NicheStatus): Severity {
    return SEVERITY[status];
  }

  load(): void {
    const query = this.status ? `?status=${this.status}` : '';
    this.http.get<NicheItem[]>(`/api/v1/niches${query}`).subscribe({
      next: (data) => this.niches.set(data),
      error: () => this.messages.add({ severity: 'error', summary: 'Falha ao carregar nichos.' }),
    });
  }

  discover(): void {
    this.http.post('/api/v1/niches/discover', {}).subscribe({
      next: () =>
        this.messages.add({
          severity: 'success',
          summary: 'Descoberta enfileirada',
          detail: 'Atualize em instantes.',
        }),
      error: () => this.messages.add({ severity: 'error', summary: 'Falha ao disparar a descoberta.' }),
    });
  }

  approve(n: NicheItem): void {
    this.act(n.id, this.http.post(`/api/v1/niches/${n.id}/approve`, {}), 'Nicho aprovado.');
  }

  discard(n: NicheItem): void {
    this.act(n.id, this.http.post(`/api/v1/niches/${n.id}/discard`, {}), 'Nicho descartado.');
  }

  generate(n: NicheItem): void {
    this.busy.set(n.id);
    this.http
      .post<{ productId: string; slug: string }>('/api/v1/products', { nicheId: n.id })
      .subscribe({
        next: (r) => void this.router.navigate(['/products', r.productId]),
        error: () => {
          this.messages.add({ severity: 'error', summary: 'Falha ao gerar produto.' });
          this.busy.set(null);
        },
      });
  }

  private act(id: string, request: ReturnType<HttpClient['post']>, ok: string): void {
    this.busy.set(id);
    request.subscribe({
      next: () => {
        this.busy.set(null);
        this.messages.add({ severity: 'success', summary: ok });
        this.load();
      },
      error: () => {
        this.busy.set(null);
        this.messages.add({ severity: 'error', summary: 'Ação falhou (transição inválida?).' });
      },
    });
  }
}
