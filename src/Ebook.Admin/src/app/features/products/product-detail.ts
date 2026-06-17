import { HttpClient } from '@angular/common/http';
import { Component, OnDestroy, inject, signal } from '@angular/core';
import { CurrencyPipe, DecimalPipe, PercentPipe } from '@angular/common';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { debounceTime, filter, merge } from 'rxjs';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { DomSanitizer, SafeUrl } from '@angular/platform-browser';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { CardModule } from 'primeng/card';
import { TagModule } from 'primeng/tag';
import { ButtonModule } from 'primeng/button';
import { InputTextModule } from 'primeng/inputtext';
import { TextareaModule } from 'primeng/textarea';
import { DialogModule } from 'primeng/dialog';
import { ConfirmationService } from 'primeng/api';
import {
  KiwifyMatch,
  Outline,
  ProductDetail as ProductDetailDto,
  ProductMetrics,
  SocialPostItem,
} from '../../core/api.types';
import { renderMarkdown } from '../../shared/markdown';
import { Loading } from '../../shared/loading';
import { NotificationService } from '../../core/notification.service';
import { RealtimeService } from '../../core/realtime.service';

const STAGES = ['Outline', 'Writing', 'Review', 'Pdf', 'Lp'] as const;

@Component({
  selector: 'app-product-detail',
  imports: [
    CurrencyPipe,
    DecimalPipe,
    PercentPipe,
    FormsModule,
    RouterLink,
    TranslocoDirective,
    CardModule,
    TagModule,
    ButtonModule,
    InputTextModule,
    TextareaModule,
    DialogModule,
    Loading,
  ],
  templateUrl: './product-detail.html',
  styleUrl: './product-detail.scss',
})
export class ProductDetail implements OnDestroy {
  private readonly http = inject(HttpClient);
  private readonly sanitizer = inject(DomSanitizer);
  private readonly notify = inject(NotificationService);
  private readonly confirm = inject(ConfirmationService);
  private readonly realtime = inject(RealtimeService);
  private readonly t = inject(TranslocoService);
  private readonly id = inject(ActivatedRoute).snapshot.paramMap.get('id')!;

  readonly steps = STAGES;
  readonly detail = signal<ProductDetailDto | null>(null);
  readonly outline = signal<Outline | null>(null);
  readonly manuscriptHtml = signal<string | null>(null);
  readonly coverUrl = signal<SafeUrl | null>(null);
  readonly social = signal<SocialPostItem[]>([]);
  readonly metrics = signal<ProductMetrics | null>(null);
  readonly error = signal<string | null>(null);
  readonly busy = signal(false);

  kiwifyProductId = '';
  checkoutUrl = '';
  readonly matching = signal(false);

  // Calendário social: edição de copy (dialog) + ações do gate.
  readonly editPostDialog = signal(false);
  readonly savingPost = signal(false);
  private editingPostId: string | null = null;
  editCaption = '';
  editHashtags = '';

  private objectUrl: string | null = null;

  constructor() {
    this.loadDetail();
    this.loadOutlineAndManuscript();
    this.loadCoverOnce();
    this.loadSocial();
    this.loadMetrics();

    // Atualização ao vivo: jobs deste produto e transições deste produto
    // recarregam o detalhe (avança o stepper, libera manuscrito/capa/LP).
    merge(
      this.realtime.jobChanged$.pipe(filter((j) => j.productId === this.id)),
      this.realtime.productChanged$.pipe(filter((p) => p.productId === this.id)),
    )
      .pipe(debounceTime(700), takeUntilDestroyed())
      .subscribe(() => {
        this.loadDetail();
        this.loadOutlineAndManuscript();
        this.loadCoverOnce();
        this.loadSocial();
        this.loadMetrics();
      });
  }

  private loadOutlineAndManuscript(): void {
    this.http
      .get<Outline>(`/api/v1/products/${this.id}/outline`)
      .subscribe({ next: (o) => this.outline.set(o), error: () => {} });

    this.http
      .get(`/api/v1/products/${this.id}/manuscript`, { responseType: 'text' })
      .subscribe({ next: (md) => this.manuscriptHtml.set(renderMarkdown(md)), error: () => {} });
  }

  private loadCoverOnce(): void {
    if (this.objectUrl) {
      return; // capa já carregada — não revalida nem vaza object URL
    }
    this.http.get(`/api/v1/products/${this.id}/cover`, { responseType: 'blob' }).subscribe({
      next: (blob) => {
        this.objectUrl = URL.createObjectURL(blob);
        this.coverUrl.set(this.sanitizer.bypassSecurityTrustUrl(this.objectUrl));
      },
      error: () => {},
    });
  }

  private loadSocial(): void {
    this.http
      .get<SocialPostItem[]>(`/api/v1/products/${this.id}/social`)
      .subscribe({ next: (list) => this.social.set(list), error: () => {} });
  }

  private loadMetrics(): void {
    this.http
      .get<ProductMetrics>(`/api/v1/products/${this.id}/metrics`)
      .subscribe({ next: (m) => this.metrics.set(m), error: () => {} });
  }

  socialSeverity(status: string): 'success' | 'info' | 'warn' | 'danger' | 'secondary' {
    switch (status) {
      case 'Published':
        return 'success';
      case 'Queued':
        return 'warn';
      case 'Failed':
        return 'danger';
      case 'Skipped':
        return 'secondary';
      default:
        return 'info';
    }
  }

  /** URL pública do criativo do post (rota anônima /media/*). */
  mediaUrl(post: SocialPostItem): string | null {
    return post.mediaPath ? `/media/${post.mediaPath}` : null;
  }

  /** Aprova/desaprova o post (gate). */
  setPostApproval(post: SocialPostItem, approved: boolean): void {
    this.http.post(`/api/v1/social/posts/${post.id}/approval`, { approved }).subscribe({
      next: () => {
        this.notify.success(
          this.t.translate(approved ? 'productDetail.postApproved' : 'productDetail.postUnapproved'));
        this.loadSocial();
      },
      error: (e: { error?: { detail?: string } }) =>
        this.notify.error(e.error?.detail ?? this.t.translate('common.actionFailed')),
    });
  }

  openEditPost(post: SocialPostItem): void {
    this.editingPostId = post.id;
    this.editCaption = post.caption;
    this.editHashtags = post.hashtags;
    this.editPostDialog.set(true);
  }

  saveEditPost(): void {
    if (!this.editingPostId) return;
    this.savingPost.set(true);
    this.http
      .put(`/api/v1/social/posts/${this.editingPostId}/content`, {
        caption: this.editCaption,
        hashtags: this.editHashtags,
      })
      .subscribe({
        next: () => {
          this.notify.success(this.t.translate('productDetail.postSaved'));
          this.editPostDialog.set(false);
          this.savingPost.set(false);
          this.loadSocial();
        },
        error: (e: { error?: { detail?: string } }) => {
          this.notify.error(e.error?.detail ?? this.t.translate('common.actionFailed'));
          this.savingPost.set(false);
        },
      });
  }

  publishPostNow(post: SocialPostItem): void {
    this.http.post(`/api/v1/social/posts/${post.id}/publish-now`, {}).subscribe({
      next: () => {
        this.notify.success(this.t.translate('productDetail.postPublishing'));
        this.loadSocial();
      },
      error: (e: { error?: { detail?: string } }) =>
        this.notify.error(e.error?.detail ?? this.t.translate('common.actionFailed')),
    });
  }

  private loadDetail(): void {
    this.http.get<ProductDetailDto>(`/api/v1/products/${this.id}`).subscribe({
      next: (d) => this.detail.set(d),
      error: () => this.error.set(this.t.translate('productDetail.notFound')),
    });
  }

  approve(): void {
    this.confirm.confirm({
      header: this.t.translate('productDetail.approveHeader'),
      message: this.t.translate('productDetail.approveMessage'),
      icon: 'pi pi-send',
      acceptLabel: this.t.translate('productDetail.approveConfirm'),
      rejectLabel: this.t.translate('common.cancel'),
      accept: () =>
        this.act(
          this.http.post(`/api/v1/products/${this.id}/approve`, {}),
          this.t.translate('productDetail.approved'),
        ),
    });
  }

  reject(): void {
    this.confirm.confirm({
      header: this.t.translate('productDetail.rejectHeader'),
      message: this.t.translate('productDetail.rejectMessage'),
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: this.t.translate('productDetail.rejectConfirm'),
      rejectLabel: this.t.translate('common.cancel'),
      acceptButtonStyleClass: 'p-button-danger',
      accept: () =>
        this.act(
          this.http.post(`/api/v1/products/${this.id}/reject`, {
            reason: this.t.translate('productDetail.rejectReason'),
          }),
          this.t.translate('productDetail.rejected'),
        ),
    });
  }

  /** Busca o produto na API da Kiwify (por nome) e pré-preenche id + URL de checkout. */
  fetchKiwifyMatch(): void {
    this.matching.set(true);
    this.http.get<KiwifyMatch>(`/api/v1/products/${this.id}/kiwify-match`).subscribe({
      next: (m) => {
        this.kiwifyProductId = m.kiwifyProductId;
        this.checkoutUrl = m.checkoutUrl;
        this.notify.success(this.t.translate('productDetail.kiwifyFound'), m.name);
        this.matching.set(false);
      },
      error: (e: { error?: { detail?: string } }) => {
        this.notify.error(e.error?.detail ?? this.t.translate('productDetail.kiwifyNotFound'));
        this.matching.set(false);
      },
    });
  }

  completePublishing(): void {
    if (!this.kiwifyProductId.trim() || !this.checkoutUrl.trim()) {
      this.notify.warn(this.t.translate('productDetail.publishMissing'));
      return;
    }
    this.act(
      this.http.post(`/api/v1/products/${this.id}/publish`, {
        kiwifyProductId: this.kiwifyProductId.trim(),
        checkoutUrl: this.checkoutUrl.trim(),
      }),
      this.t.translate('productDetail.published'),
    );
  }

  private act(request: ReturnType<HttpClient['post']>, ok: string): void {
    this.busy.set(true);
    request.subscribe({
      next: () => {
        this.notify.success(ok);
        this.loadDetail();
        this.loadSocial();
        this.busy.set(false);
      },
      error: () => {
        this.notify.error(
          this.t.translate('common.actionFailed'),
          this.t.translate('common.actionFailedDetail'),
        );
        this.busy.set(false);
      },
    });
  }

  stepClass(stage: string): string {
    const d = this.detail();
    if (!d) {
      return '';
    }
    const current = STAGES.indexOf(d.stage as (typeof STAGES)[number]);
    const beyond = current < 0; // Publishing/Live → tudo concluído
    const index = STAGES.indexOf(stage as (typeof STAGES)[number]);
    if (beyond || index < current) {
      return 'done';
    }
    return index === current ? 'current' : '';
  }

  salesHeadline(): string | null {
    const json = this.detail()?.salesCopyJson;
    if (!json) {
      return null;
    }
    try {
      return (JSON.parse(json) as { headline?: string }).headline ?? null;
    } catch {
      return null;
    }
  }

  openLp(): void {
    const url = this.detail()?.lpUrl;
    if (url) {
      window.open(url, '_blank', 'noopener');
    }
  }

  downloadPdf(): void {
    this.http.get(`/api/v1/products/${this.id}/pdf`, { responseType: 'blob' }).subscribe((blob) => {
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `tomo-${this.detail()?.slug ?? this.id}.pdf`;
      a.click();
      URL.revokeObjectURL(url);
    });
  }

  ngOnDestroy(): void {
    if (this.objectUrl) {
      URL.revokeObjectURL(this.objectUrl);
    }
  }
}
