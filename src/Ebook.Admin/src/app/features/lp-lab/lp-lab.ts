import { HttpClient } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { DomSanitizer, SafeHtml } from '@angular/platform-browser';
import { SelectModule } from 'primeng/select';
import { ButtonModule } from 'primeng/button';
import { TextareaModule } from 'primeng/textarea';
import { DialogModule } from 'primeng/dialog';
import { TagModule } from 'primeng/tag';
import { GenerateTestLpResult, LpTrace, NicheItem } from '../../core/api.types';
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
    this.loading.set(true);
    this.http
      .post<GenerateTestLpResult>('/api/v1/lp-lab/generate', {
        nicheId: this.nicheId,
        feedback: this.feedback,
      })
      .subscribe({
        next: (r) => {
          this.html.set(r.html);
          this.trace.set(r.trace);
          this.loading.set(false);
          this.notify.success('Landing page gerada.');
        },
        error: (e: { error?: { detail?: string } }) => {
          this.loading.set(false);
          this.notify.error('Falha ao gerar a LP', e.error?.detail ?? 'Tente novamente.');
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
