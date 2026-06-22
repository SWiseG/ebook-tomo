import { HttpClient } from '@angular/common/http';
import { Component, DestroyRef, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { Subscription, switchMap, takeWhile, timer } from 'rxjs';
import { SelectModule } from 'primeng/select';
import { ButtonModule } from 'primeng/button';
import { TextareaModule } from 'primeng/textarea';
import { DialogModule } from 'primeng/dialog';
import { TagModule } from 'primeng/tag';
import { EnqueueTestLpResult, LpLabRun, LpTrace, NicheItem } from '../../core/api.types';
import { NotificationService } from '../../core/notification.service';

/**
 * Laboratório de LP (ferramenta de teste): seleciona um nicho, gera uma landing page de teste
 * (sem criar e-book), deixa feedback em "Memória" (injetado na regeneração), salva a memória e
 * inspeciona o caminho percorrido (o que foi usado, quem usou e como) num modal.
 */
@Component({
  selector: 'app-lp-lab',
  imports: [FormsModule, SelectModule, ButtonModule, TextareaModule, DialogModule, TagModule],
  templateUrl: './lp-lab.html',
  styleUrl: './lp-lab.scss',
})
export class LpLab {
  private readonly http = inject(HttpClient);
  private readonly sanitizer = inject(DomSanitizer);
  private readonly notify = inject(NotificationService);
  private readonly destroyRef = inject(DestroyRef);

  /** Polling do run em andamento; cancelado ao reenfileirar ou destruir o componente. */
  private pollSub?: Subscription;

  readonly niches = signal<NicheItem[]>([]);
  nicheId: string | null = null;
  feedback = '';

  readonly html = signal<string | null>(null);
  readonly trace = signal<LpTrace | null>(null);
  readonly loading = signal(false);
  readonly savingMemory = signal(false);
  readonly traceOpen = signal(false);

  /** O HTML auto-contido da LP vai para o iframe via srcdoc (precisa ser confiável). */
  readonly previewSrc = computed<SafeHtml | null>(() => {
    const h = this.html();
    return h ? this.sanitizer.bypassSecurityTrustHtml(h) : null;
  });

  constructor() {
    this.http.get<NicheItem[]>('/api/v1/niches').subscribe({
      next: (list) => this.niches.set(list),
      error: () => this.notify.error('Falha ao carregar nichos.'),
    });
  }

  /** Ao trocar de nicho, recarrega a memória salva (feedback) daquele nicho. */
  onNicheChange(): void {
    this.html.set(null);
    this.trace.set(null);
    if (!this.nicheId) {
      return;
    }
    this.http
      .get<{ feedback: string }>(`/api/v1/lp-lab/memory?nicheId=${this.nicheId}`)
      .subscribe({ next: (r) => (this.feedback = r.feedback ?? ''), error: () => {} });
  }

  generate(): void {
    if (!this.nicheId) {
      this.notify.warn('Selecione um nicho para testar.');
      return;
    }
    this.pollSub?.unsubscribe();
    this.loading.set(true);
    this.html.set(null);
    this.trace.set(null);
    this.http
      .post<EnqueueTestLpResult>('/api/v1/lp-lab/generate', {
        nicheId: this.nicheId,
        feedback: this.feedback,
      })
      .subscribe({
        next: (r) => this.poll(r.runId),
        error: (e: { error?: { detail?: string } }) => {
          this.loading.set(false);
          this.notify.error('Falha ao enfileirar a LP', e.error?.detail ?? 'Tente novamente.');
        },
      });
  }

  /**
   * A geração roda num job (assíncrono, imune ao timeout do proxy). Busca o resultado a cada 2,5s
   * enquanto estiver "pending"; encerra ao concluir, falhar ou destruir o componente.
   */
  private poll(runId: string): void {
    this.pollSub = timer(0, 2500)
      .pipe(
        switchMap(() => this.http.get<LpLabRun>(`/api/v1/lp-lab/result?runId=${runId}`)),
        takeWhile((r) => r.status === 'pending', true),
        takeUntilDestroyed(this.destroyRef),
      )
      .subscribe({
        next: (r) => {
          if (r.status === 'pending') {
            return;
          }
          this.loading.set(false);
          if (r.status === 'succeeded') {
            this.html.set(r.html);
            this.trace.set(r.trace);
            this.notify.success('Landing page gerada.');
          } else {
            this.notify.error('Falha ao gerar a LP', r.error ?? 'Tente novamente.');
          }
        },
        error: () => {
          this.loading.set(false);
          this.notify.error('Falha ao acompanhar a geração da LP.');
        },
      });
  }

  saveMemory(): void {
    if (!this.nicheId) {
      this.notify.warn('Selecione um nicho.');
      return;
    }
    this.savingMemory.set(true);
    this.http
      .post('/api/v1/lp-lab/memory', { nicheId: this.nicheId, feedback: this.feedback })
      .subscribe({
        next: () => {
          this.savingMemory.set(false);
          this.notify.success('Memória salva', 'Será usada na próxima regeneração.');
        },
        error: () => {
          this.savingMemory.set(false);
          this.notify.error('Falha ao salvar a memória.');
        },
      });
  }
}
