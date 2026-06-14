import { HttpClient } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { ButtonModule } from 'primeng/button';
import { SelectButtonModule } from 'primeng/selectbutton';
import { ConfirmationService } from 'primeng/api';
import { NicheItem, NicheStatus } from '../../core/api.types';
import { NotificationService } from '../../core/notification.service';
import { Loading } from '../../shared/loading';

type Severity = 'success' | 'info' | 'warn' | 'danger' | 'secondary' | 'contrast' | undefined;

const SEVERITY: Record<NicheStatus, Severity> = {
  Candidate: 'info',
  Selected: undefined, // cor primária
  Active: 'success',
  Discarded: 'secondary',
};

@Component({
  selector: 'app-niches',
  imports: [
    DatePipe,
    DecimalPipe,
    FormsModule,
    TableModule,
    TagModule,
    ButtonModule,
    SelectButtonModule,
    Loading,
  ],
  templateUrl: './niches.html',
  styleUrl: './niches.scss',
})
export class Niches {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly notify = inject(NotificationService);
  private readonly confirm = inject(ConfirmationService);

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
      error: () => this.notify.error('Falha ao carregar nichos.'),
    });
  }

  discover(): void {
    this.http.post('/api/v1/niches/discover', {}).subscribe({
      next: () => this.notify.success('Descoberta enfileirada', 'Atualize em instantes.'),
      error: () => this.notify.error('Falha ao disparar a descoberta.'),
    });
  }

  approve(n: NicheItem): void {
    this.act(n.id, this.http.post(`/api/v1/niches/${n.id}/approve`, {}), 'Nicho aprovado.');
  }

  discard(n: NicheItem): void {
    this.confirm.confirm({
      header: 'Descartar nicho',
      message: `Descartar "${n.name}"? Ele sairá do ranking de descoberta.`,
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: 'Descartar',
      rejectLabel: 'Cancelar',
      acceptButtonStyleClass: 'p-button-danger',
      accept: () =>
        this.act(n.id, this.http.post(`/api/v1/niches/${n.id}/discard`, {}), 'Nicho descartado.'),
    });
  }

  generate(n: NicheItem): void {
    this.busy.set(n.id);
    this.http
      .post<{ productId: string; slug: string }>('/api/v1/products', { nicheId: n.id })
      .subscribe({
        next: (r) => void this.router.navigate(['/products', r.productId]),
        error: () => {
          this.notify.error('Falha ao gerar produto.');
          this.busy.set(null);
        },
      });
  }

  private act(id: string, request: ReturnType<HttpClient['post']>, ok: string): void {
    this.busy.set(id);
    request.subscribe({
      next: () => {
        this.busy.set(null);
        this.notify.success(ok);
        this.load();
      },
      error: () => {
        this.busy.set(null);
        this.notify.error('Ação falhou', 'Transição inválida?');
      },
    });
  }
}
