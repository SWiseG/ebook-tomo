import { Injectable, computed, inject, signal } from '@angular/core';
import { Subject } from 'rxjs';
import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from '@microsoft/signalr';
import { RealtimeJobChanged, RealtimeProductChanged } from './api.types';
import { AuthService } from './auth.service';

export interface RealtimeNotification {
  id: number;
  kind: 'job' | 'product';
  severity: 'success' | 'info' | 'warn' | 'danger';
  title: string;
  detail: string;
  at: Date;
  read: boolean;
}

const MAX_NOTIFICATIONS = 30;

/**
 * Conexão SignalR com o hub /hubs/tomo. Reconecta sozinho, autentica via JWT
 * (query string access_token) e expõe:
 *  - streams (Subject) de jobs/produtos para as páginas reagirem (refetch);
 *  - estado de conexão e uma lista de notificações para o sino do header.
 */
@Injectable({ providedIn: 'root' })
export class RealtimeService {
  private readonly auth = inject(AuthService);
  private connection?: HubConnection;
  private seq = 0;

  /** Estado da conexão para o indicador visual no header. */
  readonly connected = signal(false);

  /** Notificações recentes (mais novas primeiro) para o sino. */
  readonly notifications = signal<RealtimeNotification[]>([]);
  readonly unreadCount = computed(() => this.notifications().filter((n) => !n.read).length);

  /** Streams para as páginas: cada push emite um evento. */
  readonly jobChanged$ = new Subject<RealtimeJobChanged>();
  readonly productChanged$ = new Subject<RealtimeProductChanged>();

  /** Conecta (idempotente). Chamado pelo Shell após autenticação. */
  async start(): Promise<void> {
    if (this.connection && this.connection.state !== HubConnectionState.Disconnected) {
      return;
    }

    this.connection = new HubConnectionBuilder()
      .withUrl('/hubs/tomo', { accessTokenFactory: () => this.auth.token ?? '' })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();

    this.connection.on('JobChanged', (c: RealtimeJobChanged) => this.onJob(c));
    this.connection.on('ProductChanged', (c: RealtimeProductChanged) => this.onProduct(c));

    this.connection.onreconnected(() => this.connected.set(true));
    this.connection.onreconnecting(() => this.connected.set(false));
    this.connection.onclose(() => this.connected.set(false));

    try {
      await this.connection.start();
      this.connected.set(true);
    } catch {
      this.connected.set(false);
    }
  }

  /** Encerra a conexão. Chamado no logout. */
  async stop(): Promise<void> {
    this.connected.set(false);
    try {
      await this.connection?.stop();
    } finally {
      this.connection = undefined;
      this.notifications.set([]);
    }
  }

  markAllRead(): void {
    this.notifications.update((list) => list.map((n) => ({ ...n, read: true })));
  }

  clear(): void {
    this.notifications.set([]);
  }

  private onJob(c: RealtimeJobChanged): void {
    this.jobChanged$.next(c);
    // Só notifica os estados que importam ao operador: sucesso e dead-letter.
    if (c.status === 'Succeeded') {
      this.push('job', 'success', 'Job concluído', this.jobLabel(c.type));
    } else if (c.status === 'Dead') {
      this.push('job', 'danger', 'Job falhou', `${this.jobLabel(c.type)} — ${c.lastError ?? 'sem detalhe'}`);
    }
  }

  private onProduct(c: RealtimeProductChanged): void {
    this.productChanged$.next(c);
    const map: Record<string, { sev: RealtimeNotification['severity']; msg: string }> = {
      ProductCreated: { sev: 'info', msg: 'Novo produto no pipeline' },
      ProductStageAdvanced: { sev: 'info', msg: 'Produto avançou de etapa' },
      ProductSubmittedForApproval: { sev: 'warn', msg: 'Produto aguardando aprovação' },
      ProductRejected: { sev: 'warn', msg: 'Produto rejeitado para retrabalho' },
      ProductPublishingStarted: { sev: 'info', msg: 'Publicação iniciada' },
      ProductPublished: { sev: 'success', msg: 'Produto publicado' },
      ProductRetired: { sev: 'warn', msg: 'Produto aposentado' },
    };
    const entry = map[c.event];
    if (entry) {
      this.push('product', entry.sev, entry.msg, '');
    }
  }

  private push(
    kind: RealtimeNotification['kind'],
    severity: RealtimeNotification['severity'],
    title: string,
    detail: string,
  ): void {
    const note: RealtimeNotification = {
      id: ++this.seq,
      kind,
      severity,
      title,
      detail,
      at: new Date(),
      read: false,
    };
    this.notifications.update((list) => [note, ...list].slice(0, MAX_NOTIFICATIONS));
  }

  /** Converte "ebook.chapter" → "Capítulo" etc. para o texto da notificação. */
  private jobLabel(type: string): string {
    const labels: Record<string, string> = {
      'ebook.outline': 'Estrutura do ebook',
      'ebook.chapter': 'Capítulo',
      'ebook.review': 'Revisão',
      'ebook.pdf': 'PDF',
      'ebook.cover': 'Capa',
      'lp.generate': 'Landing page',
      'kiwify.publish': 'Publicação Kiwify',
      'social.calendar': 'Calendário social',
      'video.reel': 'Reel de vídeo',
    };
    return labels[type] ?? type;
  }
}
