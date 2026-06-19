import { Component, computed, DestroyRef, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import {
  NavigationCancel,
  NavigationEnd,
  NavigationError,
  NavigationStart,
  Router,
  RouterLink,
  RouterLinkActive,
  RouterOutlet,
} from '@angular/router';
import { TranslocoDirective } from '@jsverse/transloco';
import { ButtonModule } from 'primeng/button';
import { ToastModule } from 'primeng/toast';
import { TooltipModule } from 'primeng/tooltip';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { PopoverModule } from 'primeng/popover';
import { AuthService } from '../../core/auth.service';
import { ThemeService } from '../../core/theme.service';
import { LayoutService } from '../../core/layout.service';
import { LanguageService } from '../../core/language.service';
import { LogsIndicatorService } from '../../core/logs-indicator.service';
import { RealtimeService } from '../../core/realtime.service';
import { TomoLogo } from '../../shared/tomo-logo';

interface NavItem {
  path: string;
  label: string;
  icon: string;
}
interface NavGroup {
  title: string;
  items: NavItem[];
}

@Component({
  selector: 'app-shell',
  imports: [
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    DatePipe,
    TranslocoDirective,
    TomoLogo,
    ButtonModule,
    ToastModule,
    TooltipModule,
    ConfirmDialogModule,
    PopoverModule,
  ],
  templateUrl: './shell.html',
  styleUrl: './shell.scss',
})
export class Shell {
  readonly auth = inject(AuthService);
  readonly theme = inject(ThemeService);
  readonly layout = inject(LayoutService);
  readonly language = inject(LanguageService);
  readonly realtime = inject(RealtimeService);
  readonly logsIndicator = inject(LogsIndicatorService);
  private readonly router = inject(Router);
  private readonly http = inject(HttpClient);

  /** true enquanto uma navegação (incl. carregamento lazy) está em andamento → barra de progresso. */
  readonly navigating = signal(false);

  /** Número de build (build.txt gerado na publicação); ausente em dev sem build → rodapé escondido. */
  readonly build = signal<string | null>(null);

  /** Idioma ativo (objeto), para mostrar a bandeira no botão do seletor. */
  readonly currentLanguage = computed(
    () => this.language.languages.find((l) => l.code === this.language.current()) ?? this.language.languages[0],
  );

  // Labels e títulos são chaves i18n (traduzidas no template via `t(...)`).
  readonly groups: NavGroup[] = [
    { title: 'nav.groups.overview', items: [{ path: '/dashboard', label: 'nav.dashboard', icon: 'pi pi-th-large' }] },
    {
      title: 'nav.groups.content',
      items: [
        { path: '/niches', label: 'nav.niches', icon: 'pi pi-compass' },
        { path: '/products', label: 'nav.products', icon: 'pi pi-book' },
      ],
    },
    {
      title: 'nav.groups.distribution',
      items: [{ path: '/channels', label: 'nav.channels', icon: 'pi pi-megaphone' }],
    },
    {
      title: 'nav.groups.operation',
      items: [
        { path: '/jobs', label: 'nav.jobs', icon: 'pi pi-bolt' },
        { path: '/optimizer', label: 'nav.optimizer', icon: 'pi pi-bullseye' },
        { path: '/media', label: 'nav.media', icon: 'pi pi-image' },
        { path: '/logs', label: 'nav.logs', icon: 'pi pi-desktop' },
        { path: '/settings', label: 'nav.settings', icon: 'pi pi-sliders-h' },
      ],
    },
    { title: 'nav.groups.help', items: [{ path: '/tutorial', label: 'nav.tutorial', icon: 'pi pi-question-circle' }] },
  ];

  constructor() {
    this.router.events.pipe(takeUntilDestroyed()).subscribe((e) => {
      if (e instanceof NavigationStart) {
        this.navigating.set(true);
      } else if (
        e instanceof NavigationEnd ||
        e instanceof NavigationCancel ||
        e instanceof NavigationError
      ) {
        this.navigating.set(false);
        this.layout.closeMobile(); // fecha o drawer ao trocar de rota
      }
    });

    // Número de build (se publicado): consome /build.txt; ausente → rodapé fica escondido.
    this.http.get('/build.txt', { responseType: 'text' }).subscribe({
      next: (v) => this.build.set(v.trim() || null),
      error: () => this.build.set(null),
    });

    // Conexão em tempo real viva enquanto o shell (sessão autenticada) existir.
    void this.realtime.start();
    inject(DestroyRef).onDestroy(() => void this.realtime.stop());
  }
}
