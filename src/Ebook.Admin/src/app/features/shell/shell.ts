import { Component, DestroyRef, inject, signal } from '@angular/core';
import { DatePipe } from '@angular/common';
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
import { ButtonModule } from 'primeng/button';
import { ToastModule } from 'primeng/toast';
import { TooltipModule } from 'primeng/tooltip';
import { ConfirmDialogModule } from 'primeng/confirmdialog';
import { PopoverModule } from 'primeng/popover';
import { AuthService } from '../../core/auth.service';
import { ThemeService } from '../../core/theme.service';
import { LayoutService } from '../../core/layout.service';
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
  readonly realtime = inject(RealtimeService);
  private readonly router = inject(Router);

  /** true enquanto uma navegação (incl. carregamento lazy) está em andamento → barra de progresso. */
  readonly navigating = signal(false);

  readonly groups: NavGroup[] = [
    { title: 'Visão geral', items: [{ path: '/dashboard', label: 'Dashboard', icon: 'pi pi-th-large' }] },
    {
      title: 'Conteúdo',
      items: [
        { path: '/niches', label: 'Nichos', icon: 'pi pi-compass' },
        { path: '/products', label: 'Produtos', icon: 'pi pi-book' },
      ],
    },
    {
      title: 'Operação',
      items: [
        { path: '/jobs', label: 'Jobs', icon: 'pi pi-bolt' },
        { path: '/optimizer', label: 'Otimizador', icon: 'pi pi-bullseye' },
        { path: '/settings', label: 'Configurações', icon: 'pi pi-sliders-h' },
      ],
    },
    { title: 'Ajuda', items: [{ path: '/tutorial', label: 'Como usar', icon: 'pi pi-question-circle' }] },
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

    // Conexão em tempo real viva enquanto o shell (sessão autenticada) existir.
    void this.realtime.start();
    inject(DestroyRef).onDestroy(() => void this.realtime.stop());
  }
}
