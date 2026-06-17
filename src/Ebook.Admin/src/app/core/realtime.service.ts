import { Injectable, computed, inject, signal } from '@angular/core';
import { Subject } from 'rxjs';
import {
  HubConnection,
  HubConnectionBuilder,
  HubConnectionState,
  LogLevel,
} from '@microsoft/signalr';
import { TranslocoService } from '@jsverse/transloco';
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
  private readonly t = inject(TranslocoService);
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
      this.push('job', 'success', this.t.translate('realtime.job.succeeded'), this.jobLabel(c.type));
    } else if (c.status === 'Dead') {
      const detail = c.lastError ?? this.t.translate('realtime.job.noDetail');
      this.push('job', 'danger', this.t.translate('realtime.job.failed'), `${this.jobLabel(c.type)} — ${detail}`);
    }
  }

  private onProduct(c: RealtimeProductChanged): void {
    this.productChanged$.next(c);
    const map: Record<string, { sev: RealtimeNotification['severity']; key: string }> = {
      ProductCreated: { sev: 'info', key: 'realtime.product.created' },
      ProductStageAdvanced: { sev: 'info', key: 'realtime.product.stageAdvanced' },
      ProductSubmittedForApproval: { sev: 'warn', key: 'realtime.product.submittedForApproval' },
      ProductRejected: { sev: 'warn', key: 'realtime.product.rejected' },
      ProductPublishingStarted: { sev: 'info', key: 'realtime.product.publishingStarted' },
      ProductPublished: { sev: 'success', key: 'realtime.product.published' },
      ProductRetired: { sev: 'warn', key: 'realtime.product.retired' },
    };
    const entry = map[c.event];
    if (entry) {
      this.push('product', entry.sev, this.t.translate(entry.key), '');
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

  /** Converte "ebook.chapter" → "Capítulo" etc. (traduzido) para o texto da notificação. */
  private jobLabel(type: string): string {
    const keys: Record<string, string> = {
      'ebook.outline': 'realtime.jobType.ebookOutline',
      'ebook.chapter': 'realtime.jobType.ebookChapter',
      'ebook.review': 'realtime.jobType.ebookReview',
      'ebook.pdf': 'realtime.jobType.ebookPdf',
      'ebook.cover': 'realtime.jobType.ebookCover',
      'lp.generate': 'realtime.jobType.lpGenerate',
      'kiwify.publish': 'realtime.jobType.kiwifyPublish',
      'social.calendar': 'realtime.jobType.socialCalendar',
      'video.reel': 'realtime.jobType.videoReel',
    };
    const key = keys[type];
    return key ? this.t.translate(key) : type;
  }
}
