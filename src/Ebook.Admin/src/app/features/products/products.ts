import { HttpClient } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { CurrencyPipe, DatePipe } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { debounceTime } from 'rxjs';
import { Router, RouterLink } from '@angular/router';
import { DomSanitizer, SafeUrl } from '@angular/platform-browser';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { MenuItem } from 'primeng/api';
import { TableModule } from 'primeng/table';
import { TagModule } from 'primeng/tag';
import { ButtonModule } from 'primeng/button';
import { MenuModule } from 'primeng/menu';
import { Menu } from 'primeng/menu';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { TextareaModule } from 'primeng/textarea';
import { SelectModule } from 'primeng/select';
import { ProductDetail, ProductItem, ProductStatus } from '../../core/api.types';
import { NotificationService } from '../../core/notification.service';
import { RealtimeService } from '../../core/realtime.service';
import { Loading } from '../../shared/loading';

type Severity = 'success' | 'info' | 'warn' | 'danger' | 'secondary' | 'contrast' | undefined;

/** Recorte do sales-copy.json (copy de venda gerada pela IA) usado para pré-preencher o modal. */
interface SalesCopy {
  headline?: string;
  subheadline?: string;
  bullets?: string[];
  solutionSection?: string;
  category?: string;
}

const SEVERITY: Record<ProductStatus, Severity> = {
  Pipeline: 'info',
  AwaitingApproval: 'warn',
  Reworking: 'warn',
  Publishing: undefined,
  Published: 'info',
  Synchronized: 'success',
  Unsynchronized: 'danger',
  Live: 'success',
  Iterating: 'contrast',
  Retired: 'secondary',
};

@Component({
  selector: 'app-products',
  imports: [
    DatePipe,
    CurrencyPipe,
    FormsModule,
    RouterLink,
    TranslocoDirective,
    TableModule,
    TagModule,
    ButtonModule,
    MenuModule,
    DialogModule,
    InputTextModule,
    TextareaModule,
    SelectModule,
    Loading,
  ],
  templateUrl: './products.html',
  styleUrl: './products.scss',
})
export class Products {
  private readonly http = inject(HttpClient);
  private readonly router = inject(Router);
  private readonly realtime = inject(RealtimeService);
  private readonly notify = inject(NotificationService);
  private readonly sanitizer = inject(DomSanitizer);
  private readonly t = inject(TranslocoService);

  readonly products = signal<ProductItem[] | null>(null);
  readonly error = signal<string | null>(null);

  // Menu de ações por linha
  readonly menuModel = signal<MenuItem[]>([]);

  // Modal: Dados de Publicação
  readonly pubDialog = signal(false);
  readonly saving = signal(false);
  readonly coverUrl = signal<SafeUrl | null>(null);
  private current: ProductItem | null = null;
  private coverObjectUrl: string | null = null;
  pubPlatform = 'Kiwify';
  pubTitle = '';
  pubDescription = '';
  pubPrice = 0;
  pubCurrency = 'BRL';
  pubEmailLanguage = 'pt-BR';
  pubCategory = '';
  pubLpUrl: string | null = null;

  // Modal: Inserir link de checkout
  readonly checkoutDialog = signal(false);
  checkoutUrl = '';

  // Modal: Marcar como publicado
  readonly publishDialog = signal(false);
  publishPlatform = 'Kiwify';

  readonly platformOptions = [{ label: 'Kiwify', value: 'Kiwify' }];
  readonly emailLanguageOptions = [
    { label: 'Português', value: 'pt-BR' },
    { label: 'English', value: 'en' },
    { label: 'Español', value: 'es' },
  ];

  constructor() {
    this.load();

    // Atualização ao vivo: qualquer transição de produto recarrega a lista.
    this.realtime.productChanged$
      .pipe(debounceTime(600), takeUntilDestroyed())
      .subscribe(() => this.load());
  }

  private load(): void {
    this.http.get<ProductItem[]>('/api/v1/products').subscribe({
      next: (data) => this.products.set(data),
      error: () => this.error.set(this.t.translate('products.loadError')),
    });
  }

  severity(status: ProductStatus): Severity {
    return SEVERITY[status];
  }

  open(p: ProductItem): void {
    void this.router.navigate(['/products', p.id]);
  }

  /** Abre o menu de ações da linha, montando os itens com habilitação por status/estágio. */
  openMenu(event: MouseEvent, p: ProductItem, menu: Menu): void {
    const inPublishing = p.status === 'Publishing' && p.stage === 'Publishing';
    const published = p.status === 'Published' || p.status === 'Synchronized' || p.status === 'Unsynchronized';
    const checkoutable = p.status === 'Publishing' || published;

    this.menuModel.set([
      {
        label: this.t.translate('products.actions.publicationData'),
        icon: 'pi pi-file-edit',
        disabled: !inPublishing,
        command: () => this.openPublicationData(p),
      },
      {
        label: this.t.translate('products.actions.checkout'),
        icon: 'pi pi-link',
        disabled: !checkoutable,
        command: () => this.openCheckout(p),
      },
      {
        label: this.t.translate('products.actions.markPublished'),
        icon: 'pi pi-send',
        disabled: p.status !== 'Publishing',
        command: () => this.openMarkPublished(p),
      },
      {
        label: this.t.translate('products.actions.sync'),
        icon: 'pi pi-sync',
        disabled: !published,
        command: () => this.sync(p),
      },
    ]);
    menu.toggle(event);
  }

  // ── Dados de Publicação ──
  private openPublicationData(p: ProductItem): void {
    this.current = p;
    this.http.get<ProductDetail>(`/api/v1/products/${p.id}`).subscribe((d) => {
      const copy = this.parseSalesCopy(d.salesCopyJson);
      this.pubPlatform = d.publicationPlatform ?? 'Kiwify';
      this.pubTitle = d.title;
      // Descrição = copy de venda gerada (a menos que já tenha sido editada/salva).
      this.pubDescription = d.description?.trim() ? d.description : this.composeDescription(copy);
      this.pubPrice = d.price;
      this.pubCurrency = d.currency || 'BRL';
      this.pubEmailLanguage = d.emailLanguage ?? 'pt-BR';
      // Categoria sugerida: salva → da copy (IA) → heurística pelo título.
      this.pubCategory = d.category?.trim() || copy.category?.trim() || this.suggestCategory(d.title);
      this.pubLpUrl = d.lpUrl;
      this.loadCover(p.id);
      this.pubDialog.set(true);
    });
  }

  private loadCover(id: string): void {
    this.revokeCover();
    this.coverUrl.set(null);
    this.http.get(`/api/v1/products/${id}/cover`, { responseType: 'blob' }).subscribe({
      next: (blob) => {
        this.coverObjectUrl = URL.createObjectURL(blob);
        this.coverUrl.set(this.sanitizer.bypassSecurityTrustUrl(this.coverObjectUrl));
      },
      error: () => {},
    });
  }

  savePublicationData(): void {
    if (!this.current) return;
    if (this.pubDescription.trim().length < 100) {
      this.notify.warn(this.t.translate('products.pub.descriptionTooShort'));
      return;
    }
    this.saving.set(true);
    this.http
      .put(`/api/v1/products/${this.current.id}/publication-data`, {
        platform: this.pubPlatform,
        title: this.pubTitle,
        description: this.pubDescription,
        price: this.pubPrice,
        currency: this.pubCurrency,
        emailLanguage: this.pubEmailLanguage,
        category: this.pubCategory,
      })
      .subscribe({
        next: () => {
          this.notify.success(this.t.translate('products.pub.saved'));
          this.pubDialog.set(false);
          this.saving.set(false);
          this.load();
        },
        error: (e: { error?: { detail?: string } }) => {
          this.notify.error(e.error?.detail ?? this.t.translate('common.actionFailed'));
          this.saving.set(false);
        },
      });
  }

  downloadCover(): void {
    if (!this.current) return;
    const slug = this.current.slug;
    this.http.get(`/api/v1/products/${this.current.id}/cover`, { responseType: 'blob' }).subscribe((blob) => {
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `tomo-capa-${slug}.png`;
      a.click();
      URL.revokeObjectURL(url);
    });
  }

  // ── Inserir link de checkout ──
  private openCheckout(p: ProductItem): void {
    this.current = p;
    this.checkoutUrl = '';
    this.http.get<ProductDetail>(`/api/v1/products/${p.id}`).subscribe((d) => {
      this.checkoutUrl = d.checkoutUrl ?? '';
      this.checkoutDialog.set(true);
    });
  }

  saveCheckout(): void {
    if (!this.current || !this.checkoutUrl.trim()) return;
    this.saving.set(true);
    this.http
      .put(`/api/v1/products/${this.current.id}/checkout`, { checkoutUrl: this.checkoutUrl.trim() })
      .subscribe({
        next: () => {
          this.notify.success(this.t.translate('products.checkout.saved'));
          this.checkoutDialog.set(false);
          this.saving.set(false);
          this.load();
        },
        error: (e: { error?: { detail?: string } }) => {
          this.notify.error(e.error?.detail ?? this.t.translate('common.actionFailed'));
          this.saving.set(false);
        },
      });
  }

  // ── Marcar como publicado ──
  private openMarkPublished(p: ProductItem): void {
    this.current = p;
    this.publishPlatform = 'Kiwify';
    this.publishDialog.set(true);
  }

  confirmMarkPublished(): void {
    if (!this.current) return;
    this.saving.set(true);
    this.http
      .post(`/api/v1/products/${this.current.id}/mark-published`, { platform: this.publishPlatform })
      .subscribe({
        next: () => {
          this.notify.success(this.t.translate('products.publish.done'));
          this.publishDialog.set(false);
          this.saving.set(false);
          this.load();
        },
        error: (e: { error?: { detail?: string } }) => {
          this.notify.error(e.error?.detail ?? this.t.translate('common.actionFailed'));
          this.saving.set(false);
        },
      });
  }

  // ── Sincronizar ──
  private sync(p: ProductItem): void {
    this.http.post(`/api/v1/products/${p.id}/sync`, {}).subscribe({
      next: () => this.notify.success(this.t.translate('products.sync.queued')),
      error: (e: { error?: { detail?: string } }) =>
        this.notify.error(e.error?.detail ?? this.t.translate('common.actionFailed')),
    });
  }

  private parseSalesCopy(json: string | null | undefined): SalesCopy {
    if (!json) return {};
    try {
      return JSON.parse(json) as SalesCopy;
    } catch {
      return {};
    }
  }

  /** Monta a descrição para a Kiwify a partir da copy de venda (headline + sub + bullets + solução). */
  private composeDescription(copy: SalesCopy): string {
    const parts: string[] = [];
    if (copy.headline?.trim()) parts.push(copy.headline.trim());
    if (copy.subheadline?.trim()) parts.push(copy.subheadline.trim());
    const bullets = (copy.bullets ?? []).filter((b) => b?.trim()).map((b) => `• ${b.trim()}`);
    if (bullets.length) parts.push(bullets.join('\n'));
    if (copy.solutionSection?.trim()) parts.push(copy.solutionSection.trim());
    return parts.join('\n\n');
  }

  /** Sugere uma categoria pelo título quando a IA não a forneceu (fallback heurístico). */
  private suggestCategory(title: string): string {
    const t = title.normalize('NFD').replace(/[̀-ͯ]/g, '').toLowerCase();
    const rules: [RegExp, string][] = [
      [/financ|dinheiro|invest|renda|lucro|econom|riqueza|divida/, 'Finanças'],
      [/emagre|dieta|fitness|saude|queima|peso|treino|gordura|metabol/, 'Saúde'],
      [/relacion|amor|casamento|conquist|paquera/, 'Relacionamentos'],
      [/produtiv|habito|foco|mentalidade|autoestima|disciplina|ansiedade/, 'Desenvolvimento Pessoal'],
      [/negocio|empreend|carreira|vendas|marketing|trafego|cliente/, 'Negócios e Carreira'],
      [/deus|espirit|oracao|biblia|cristao/, 'Espiritualidade'],
      [/receita|culinaria|cozinha/, 'Culinária'],
      [/ingles|idioma|estudo|concurso|aprend/, 'Educação'],
      [/beleza|skincare|maquiagem|cabelo/, 'Beleza'],
    ];
    for (const [re, cat] of rules) {
      if (re.test(t)) return cat;
    }
    return 'Desenvolvimento Pessoal';
  }

  private revokeCover(): void {
    if (this.coverObjectUrl) {
      URL.revokeObjectURL(this.coverObjectUrl);
      this.coverObjectUrl = null;
    }
  }
}
