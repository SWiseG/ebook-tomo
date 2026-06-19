import { HttpClient } from '@angular/common/http';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { ButtonModule } from 'primeng/button';
import { DialogModule } from 'primeng/dialog';
import { InputTextModule } from 'primeng/inputtext';
import { SelectModule } from 'primeng/select';
import { AgGridAngular } from 'ag-grid-angular';
import { ColDef, GridReadyEvent, ICellRendererParams } from 'ag-grid-community';
import { tomoAgTheme } from '../../shared/ag-grid/tomo-ag-theme';
import { Channel, NicheItem } from '../../core/api.types';
import { NotificationService } from '../../core/notification.service';
import { Loading } from '../../shared/loading';

@Component({
  selector: 'app-channels',
  imports: [
    FormsModule,
    TranslocoDirective,
    AgGridAngular,
    ButtonModule,
    DialogModule,
    InputTextModule,
    SelectModule,
    Loading,
  ],
  templateUrl: './channels.html',
  styleUrl: './channels.scss',
})
export class Channels {
  private readonly http = inject(HttpClient);
  private readonly notify = inject(NotificationService);
  private readonly t = inject(TranslocoService);

  readonly channels = signal<Channel[] | null>(null);
  readonly niches = signal<NicheItem[]>([]);
  readonly saving = signal(false);

  readonly availableNiches = computed(() => {
    const taken = new Set((this.channels() ?? []).map((c) => c.nicheId));
    return this.niches().filter((n) => !taken.has(n.id));
  });

  // Criar canal
  readonly createDialog = signal(false);
  newNicheId = '';
  newName = '';

  // Conectar Meta
  readonly connectDialog = signal(false);
  private currentId: string | null = null;
  connName = '';
  connPageId = '';
  connIgUserId = '';
  connAccessToken = '';
  connMediaBaseUrl = '';

  // AG Grid
  readonly theme = tomoAgTheme;

  readonly defaultColDef: ColDef = {
    sortable: true,
    resizable: true,
    suppressMovable: true,
    suppressHeaderMenuButton: true,
  };

  readonly colDefs: ColDef<Channel>[] = this.buildCols();

  readonly gridOptions = {
    context: { openConnect: (c: Channel) => this.openConnect(c) },
  };

  constructor() { this.load(); }

  onGridReady(_e: GridReadyEvent<Channel>): void {}

  private buildCols(): ColDef<Channel>[] {
    const t = this.t;
    return [
      {
        headerName: t.translate('channels.col.niche'),
        field: 'nicheName',
        flex: 1,
        minWidth: 120,
        cellRenderer: (p: ICellRendererParams<Channel>) => `<strong>${p.value ?? ''}</strong>`,
      },
      {
        headerName: t.translate('channels.col.name'),
        field: 'name',
        flex: 1,
        minWidth: 120,
      },
      {
        headerName: t.translate('channels.col.platform'),
        field: 'platform',
        width: 110,
      },
      {
        headerName: t.translate('channels.col.status'),
        field: 'connected',
        width: 130,
        cellRenderer: (p: ICellRendererParams<Channel>) => {
          const connected = p.value as boolean;
          const sev = connected ? 'success' : 'warn';
          const label = connected ? t.translate('channels.connectedTag') : t.translate('channels.disconnectedTag');
          return `<span class="tomo-badge tomo-badge--${sev}">${label}</span>`;
        },
      },
      {
        headerName: '',
        width: 110,
        sortable: false,
        resizable: false,
        cellRenderer: (p: ICellRendererParams<Channel>) => {
          const ctx = p.context as { openConnect: (c: Channel) => void };
          const connected = p.data?.connected;
          const btn = document.createElement('button');
          btn.className = 'tomo-row-btn';
          btn.innerHTML = connected
            ? `<span class="pi pi-pencil"></span> ${t.translate('channels.edit')}`
            : `<span class="pi pi-link"></span> ${t.translate('channels.connect')}`;
          btn.addEventListener('click', (e) => { e.stopPropagation(); ctx.openConnect(p.data!); });
          return btn;
        },
      },
    ];
  }

  private load(): void {
    this.http.get<Channel[]>('/api/v1/channels').subscribe({
      next: (data) => this.channels.set(data),
      error: () => this.notify.error(this.t.translate('channels.loadError')),
    });
    this.http.get<NicheItem[]>('/api/v1/niches').subscribe({ next: (n) => this.niches.set(n), error: () => {} });
  }

  openCreate(): void {
    this.newNicheId = this.availableNiches()[0]?.id ?? '';
    this.newName = '';
    this.createDialog.set(true);
  }

  create(): void {
    if (!this.newNicheId) return;
    const niche = this.niches().find((n) => n.id === this.newNicheId);
    this.saving.set(true);
    this.http.post('/api/v1/channels', { nicheId: this.newNicheId, name: this.newName.trim() || niche?.name || '' }).subscribe({
      next: () => { this.notify.success(this.t.translate('channels.created')); this.createDialog.set(false); this.saving.set(false); this.load(); },
      error: (e: { error?: { detail?: string } }) => { this.notify.error(e.error?.detail ?? this.t.translate('common.actionFailed')); this.saving.set(false); },
    });
  }

  openConnect(c: Channel): void {
    this.currentId = c.id;
    this.connName = c.name;
    this.connPageId = c.pageId ?? '';
    this.connIgUserId = c.igUserId ?? '';
    this.connAccessToken = '';
    this.connMediaBaseUrl = c.publicMediaBaseUrl ?? '';
    this.connectDialog.set(true);
  }

  saveConnect(): void {
    if (!this.currentId || !this.connAccessToken.trim()) { this.notify.warn(this.t.translate('channels.tokenRequired')); return; }
    this.saving.set(true);
    this.http.put(`/api/v1/channels/${this.currentId}/connect`, {
      name: this.connName.trim(),
      pageId: this.connPageId.trim() || null,
      igUserId: this.connIgUserId.trim() || null,
      accessToken: this.connAccessToken.trim(),
      publicMediaBaseUrl: this.connMediaBaseUrl.trim() || null,
      tokenExpiresAtUtc: null,
    }).subscribe({
      next: () => { this.notify.success(this.t.translate('channels.connected')); this.connectDialog.set(false); this.saving.set(false); this.load(); },
      error: (e: { error?: { detail?: string } }) => { this.notify.error(e.error?.detail ?? this.t.translate('common.actionFailed')); this.saving.set(false); },
    });
  }
}
