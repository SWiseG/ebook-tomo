import { HttpClient } from '@angular/common/http';
import {
  AfterViewChecked,
  Component,
  ElementRef,
  inject,
  OnDestroy,
  signal,
  ViewChild,
} from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TranslocoDirective, TranslocoService } from '@jsverse/transloco';
import { ButtonModule } from 'primeng/button';
import { SelectButtonModule } from 'primeng/selectbutton';
import { LogsIndicatorService } from '../../core/logs-indicator.service';

interface LogEntry {
  id: number;
  timestamp: string;
  level: string;
  message: string;
  source: string | null;
}

interface LogResponse {
  lastId: number;
  entries: LogEntry[];
}

const LEVEL_FILTER_ORDER = ['ALL', 'DEBUG', 'INFO', 'WARN', 'ERROR', 'FATAL'];

@Component({
  selector: 'app-logs',
  imports: [FormsModule, TranslocoDirective, ButtonModule, SelectButtonModule],
  templateUrl: './logs.html',
  styleUrl: './logs.scss',
})
export class Logs implements AfterViewChecked, OnDestroy {
  private readonly http = inject(HttpClient);
  private readonly t = inject(TranslocoService);
  private readonly logsIndicator = inject(LogsIndicatorService);

  @ViewChild('terminal') terminalRef!: ElementRef<HTMLDivElement>;

  readonly entries = signal<LogEntry[]>([]);
  readonly paused = signal(false);
  readonly levelFilter = signal('ALL');

  private lastId = 0;
  private intervalId?: ReturnType<typeof setInterval>;
  shouldScrollToBottom = true;

  readonly levelOptions = LEVEL_FILTER_ORDER.map((l) => ({
    label: l === 'ALL' ? this.t.translate('logs.filterAll') : l,
    value: l,
  }));

  constructor() {
    this.poll();
    this.intervalId = setInterval(() => {
      if (!this.paused()) this.poll();
    }, 2000);
  }

  ngAfterViewChecked(): void {
    if (this.shouldScrollToBottom) this.scrollToBottom();
  }

  ngOnDestroy(): void {
    clearInterval(this.intervalId);
  }

  private poll(): void {
    this.http
      .get<LogResponse>(`/api/v1/logs?afterId=${this.lastId}&limit=200`)
      .subscribe({
        next: (res) => {
          if (res.entries.length > 0) {
            this.entries.update((prev) => {
              const combined = [...prev, ...res.entries];
              return combined.length > 2000 ? combined.slice(combined.length - 2000) : combined;
            });
            this.lastId = res.lastId;
            this.shouldScrollToBottom = !this.paused();
          }
          this.logsIndicator.markVisited(res.lastId);
        },
        error: () => {},
      });
  }

  get filtered(): LogEntry[] {
    const lvl = this.levelFilter();
    if (lvl === 'ALL') return this.entries();
    const minIdx = LEVEL_FILTER_ORDER.indexOf(lvl);
    return this.entries().filter((e) => LEVEL_FILTER_ORDER.indexOf(e.level) >= minIdx);
  }

  levelClass(level: string): string {
    switch (level) {
      case 'DEBUG': return 'log-debug';
      case 'INFO':  return 'log-info';
      case 'WARN':  return 'log-warn';
      case 'ERROR':
      case 'FATAL': return 'log-error';
      default:      return 'log-trace';
    }
  }

  togglePause(): void {
    this.paused.update((v) => !v);
    if (!this.paused()) {
      this.shouldScrollToBottom = true;
      this.poll();
    }
  }

  clear(): void {
    this.entries.set([]);
    this.lastId = 0;
  }

  scrollToBottom(): void {
    const el = this.terminalRef?.nativeElement;
    if (el) el.scrollTop = el.scrollHeight;
  }

  formatTime(ts: string): string {
    const d = new Date(ts);
    return d.toLocaleTimeString('pt-BR', { hour: '2-digit', minute: '2-digit', second: '2-digit' });
  }
}
