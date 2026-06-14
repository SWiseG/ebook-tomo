import { HttpClient } from '@angular/common/http';
import { Component, OnDestroy, inject, signal } from '@angular/core';
import { CurrencyPipe } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { DomSanitizer, SafeUrl } from '@angular/platform-browser';
import { CardModule } from 'primeng/card';
import { TagModule } from 'primeng/tag';
import { ButtonModule } from 'primeng/button';
import { Outline, ProductDetail as ProductDetailDto } from '../../core/api.types';
import { renderMarkdown } from '../../shared/markdown';
import { Loading } from '../../shared/loading';

const STAGES = ['Outline', 'Writing', 'Review', 'Pdf', 'Lp'] as const;

@Component({
  selector: 'app-product-detail',
  imports: [CurrencyPipe, RouterLink, CardModule, TagModule, ButtonModule, Loading],
  templateUrl: './product-detail.html',
  styleUrl: './product-detail.scss',
})
export class ProductDetail implements OnDestroy {
  private readonly http = inject(HttpClient);
  private readonly sanitizer = inject(DomSanitizer);
  private readonly id = inject(ActivatedRoute).snapshot.paramMap.get('id')!;

  readonly steps = STAGES;
  readonly detail = signal<ProductDetailDto | null>(null);
  readonly outline = signal<Outline | null>(null);
  readonly manuscriptHtml = signal<string | null>(null);
  readonly coverUrl = signal<SafeUrl | null>(null);
  readonly error = signal<string | null>(null);

  private objectUrl: string | null = null;

  constructor() {
    this.http.get<ProductDetailDto>(`/api/v1/products/${this.id}`).subscribe({
      next: (d) => this.detail.set(d),
      error: () => this.error.set('Produto não encontrado.'),
    });

    this.http
      .get<Outline>(`/api/v1/products/${this.id}/outline`)
      .subscribe({ next: (o) => this.outline.set(o), error: () => {} });

    this.http
      .get(`/api/v1/products/${this.id}/manuscript`, { responseType: 'text' })
      .subscribe({ next: (md) => this.manuscriptHtml.set(renderMarkdown(md)), error: () => {} });

    this.http.get(`/api/v1/products/${this.id}/cover`, { responseType: 'blob' }).subscribe({
      next: (blob) => {
        this.objectUrl = URL.createObjectURL(blob);
        this.coverUrl.set(this.sanitizer.bypassSecurityTrustUrl(this.objectUrl));
      },
      error: () => {},
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
