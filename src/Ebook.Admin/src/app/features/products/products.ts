import { HttpClient } from '@angular/common/http';
import { Component, effect, inject, signal, ViewChild } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { debounceTime } from 'rxjs';
import { Router, RouterLink } from '@angular/router';
import { DomSanitizer, SafeUrl } from '@angular/platform-browser';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { MenuItem } from 'primeng/api';
import { TagModule } from 'primeng/tag';
import { ButtonModule } from 'primeng/button';
import { MenuModule } from 'primeng/menu';
import { Menu } from 'primeng/menu';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { TextareaModule } from 'primeng/textarea';
import { SelectModule } from 'primeng/select';
import { AgGridAngular } from 'ag-grid-angular';
import { ColDef, GridApi, GridReadyEvent, ICellRendererParams } from 'ag-grid-community';
import { tomoAgTheme } from '../../shared/ag-grid/tomo-ag-theme';
import { ProductDetail, ProductItem, ProductStatus } from '../../core/api.types';
import { NotificationService } from '../../core/notification.service';
import { RealtimeService } from '../../core/realtime.service';
import { Loading } from '../../shared/loading';

type Severity = 'success' | 'info' | 'warn' | 'danger' | 'secondary' | 'contrast' | undefined;

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
    FormsModule,
    RouterLink,
    TranslocoDirective,
    AgGridAngular,
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

  @ViewChild('actionsMenu') actionsMenu!: Menu;

  readonly products = signal<ProductItem[] | null>(null);
  readonly error = signal<string | null>(null);
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

  // AG Grid
  readonly theme = tomoAgTheme;
  gridApi?: GridApi<ProductItem>;

  quickFilter = '';

  readonly defaultColDef: ColDef = {
    sortable: true,
    resizable: true,
    suppressMovable: true,
    suppressHeaderMenuButton: true,
    filter: true,
  };

  readonly colDefs: ColDef<ProductItem>[] = this.buildCols();

  readonly gridOptions = {
    context: { openMenu: (e: MouseEvent, p: ProductItem) => this.openMenu(e, p) },
    rowClass: 'ag-row-hover-cursor',
    suppressCellFocus: true,
    suppressPaginationPanel: false,
  };

  constructor() {
    this.load();
    this.realtime.productChanged$
      .pipe(debounceTime(600), takeUntilDestroyed())
      .subscribe(() => this.load());
  }

  onGridReady(e: GridReadyEvent<ProductItem>): void {
    this.gridApi = e.api;
  }

  onSearch(): void {
    this.gridApi?.setGridOption('quickFilterText', this.quickFilter);
  }

  private buildCols(): ColDef<ProductItem>[] {
    const t = this.t;
    const fmt = (d: string) =>
      new Date(d).toLocaleString('pt-BR', {
        day: '2-digit', month: '2-digit', year: '2-digit',
        hour: '2-digit', minute: '2-digit',
      });

    return [
      {
        headerName: t.translate('products.col.product'),
        flex: 2,
        minWidth: 180,
        sortable: true,
        valueGetter: (p) => p.data?.title ?? '',
        cellRenderer: (params: ICellRendererParams<ProductItem>) =>
          `<strong>${params.data!.title}</strong>`,
      },
      {
        headerName: t.translate('products.col.stage'),
        field: 'stage',
        width: 110,
        cellRenderer: (params: ICellRendererParams<ProductItem>) =>
          `<span class="tomo-badge tomo-badge--secondary">${t.translate('status.stage.' + params.value)}</span>`,
      },
      {
        headerName: t.translate('products.col.status'),
        field: 'status',
        width: 150,
        cellRenderer: (params: ICellRendererParams<ProductItem>) => {
          const sev = SEVERITY[params.value as ProductStatus];
          return `<span class="tomo-badge tomo-badge--${sev ?? 'default'}">${t.translate('status.product.' + params.value)}</span>`;
        },
      },
      {
        headerName: t.translate('products.col.platform'),
        field: 'publicationPlatform',
        width: 115,
        cellRenderer: (params: ICellRendererParams<ProductItem>) =>
          params.value ? t.translate('status.platform.' + params.value) : '—',
      },
      {
        headerName: t.translate('products.col.price'),
        field: 'price',
        width: 110,
        valueFormatter: (params) =>
          params.data?.price && params.data.price > 0
            ? new Intl.NumberFormat('pt-BR', {
                style: 'currency',
                currency: params.data.currency || 'BRL',
              }).format(params.data.price)
            : '—',
      },
      {
        headerName: t.translate('products.col.created'),
        field: 'createdAtUtc',
        width: 130,
        valueFormatter: (params) => (params.value ? fmt(params.value as string) : '—'),
      },
      {
        headerName: '',
        width: 56,
        sortable: false,
        resizable: false,
        cellRenderer: (params: ICellRendererParams<ProductItem>) => {
          const btn = document.createElement('button');
          btn.className = 'tomo-grid-icon-btn';
          btn.setAttribute('aria-label', t.translate('products.actions.menu'));
          btn.innerHTML = '<span class="pi pi-ellipsis-v"></span>';
          btn.addEventListener('click', (e: Event) => {
            e.stopPropagation();
            (params.context as { openMenu: (ev: MouseEvent, p: ProductItem) => void })
              .openMenu(e as MouseEvent, params.data!);
          });
          return btn;
        },
      },
    ];
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

  openMenu(event: MouseEvent, p: ProductItem): void {
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
    this.actionsMenu.toggle(event);
  }

  // ── Dados de Publicação ──
  private openPublicationData(p: ProductItem): void {
    this.current = p;
    this.http.get<ProductDetail>(`/api/v1/products/${p.id}`).subscribe((d) => {
      const copy = this.parseSalesCopy(d.salesCopyJson);
      this.pubPlatform = d.publicationPlatform ?? 'Kiwify';
      this.pubTitle = d.title;
      this.pubDescription = d.description?.trim() ? d.description : this.composeDescription(copy);
      this.pubPrice = d.price;
      this.pubCurrency = d.currency || 'BRL';
      this.pubEmailLanguage = d.emailLanguage ?? 'pt-BR';
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
    try { return JSON.parse(json) as SalesCopy; } catch { return {}; }
  }

  private composeDescription(copy: SalesCopy): string {
    const parts: string[] = [];
    if (copy.headline?.trim()) parts.push(copy.headline.trim());
    if (copy.subheadline?.trim()) parts.push(copy.subheadline.trim());
    const bullets = (copy.bullets ?? []).filter((b) => b?.trim()).map((b) => `• ${b.trim()}`);
    if (bullets.length) parts.push(bullets.join('\n'));
    if (copy.solutionSection?.trim()) parts.push(copy.solutionSection.trim());
    return parts.join('\n\n');
  }

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
    for (const [re, cat] of rules) { if (re.test(t)) return cat; }
    return 'Desenvolvimento Pessoal';
  }

  private revokeCover(): void {
    if (this.coverObjectUrl) { URL.revokeObjectURL(this.coverObjectUrl); this.coverObjectUrl = null; }
  }
}
