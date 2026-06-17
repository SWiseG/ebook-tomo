import { HttpClient } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { DatePipe, DecimalPipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { ButtonModule } from 'primeng/button';
import { SelectButtonModule } from 'primeng/selectbutton';
import { ConfirmationService } from 'primeng/api';
import { NicheItem, NicheStatus } from '../../core/api.types';
import { LanguageService } from '../../core/language.service';
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
    TranslocoDirective,
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
  private readonly t = inject(TranslocoService);
  private readonly language = inject(LanguageService);

  readonly niches = signal<NicheItem[] | null>(null);
  readonly busy = signal<string | null>(null);

  status = '';
  // Recalcula os rótulos ao trocar de idioma (depende de `language.current()`).
  readonly statusOptions = computed(() => {
    this.language.current();
    return [
      { label: this.t.translate('niches.filter.all'), value: '' },
      { label: this.t.translate('niches.filter.candidate'), value: 'Candidate' },
      { label: this.t.translate('niches.filter.selected'), value: 'Selected' },
      { label: this.t.translate('niches.filter.active'), value: 'Active' },
      { label: this.t.translate('niches.filter.discarded'), value: 'Discarded' },
    ];
  });

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
      error: () => this.notify.error(this.t.translate('niches.loadError')),
    });
  }

  discover(): void {
    this.http.post('/api/v1/niches/discover', {}).subscribe({
      next: () =>
        this.notify.success(
          this.t.translate('niches.discoverQueued'),
          this.t.translate('niches.discoverQueuedDetail'),
        ),
      error: () => this.notify.error(this.t.translate('niches.discoverError')),
    });
  }

  approve(n: NicheItem): void {
    this.act(
      n.id,
      this.http.post(`/api/v1/niches/${n.id}/approve`, {}),
      this.t.translate('niches.approved'),
    );
  }

  discard(n: NicheItem): void {
    this.confirm.confirm({
      header: this.t.translate('niches.discardHeader'),
      message: this.t.translate('niches.discardMessage', { name: n.name }),
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: this.t.translate('niches.discardConfirm'),
      rejectLabel: this.t.translate('common.cancel'),
      acceptButtonStyleClass: 'p-button-danger',
      accept: () =>
        this.act(
          n.id,
          this.http.post(`/api/v1/niches/${n.id}/discard`, {}),
          this.t.translate('niches.discarded'),
        ),
    });
  }

  generate(n: NicheItem): void {
    this.busy.set(n.id);
    this.http
      .post<{ productId: string; slug: string }>('/api/v1/products', { nicheId: n.id })
      .subscribe({
        next: (r) => void this.router.navigate(['/products', r.productId]),
        error: () => {
          this.notify.error(this.t.translate('niches.generateError'));
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
        this.notify.error(
          this.t.translate('common.actionFailed'),
          this.t.translate('common.actionFailedDetail'),
        );
      },
    });
  }
}
