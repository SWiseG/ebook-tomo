import { Component, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { ButtonModule } from 'primeng/button';
import { ToastModule } from 'primeng/toast';
import { TooltipModule } from 'primeng/tooltip';
import { AuthService } from '../../core/auth.service';
import { ThemeService } from '../../core/theme.service';
import { TomoLogo } from '../../shared/tomo-logo';

interface NavGroup {
  title: string;
  items: { path: string; label: string; icon: string }[];
}

@Component({
  selector: 'app-shell',
  imports: [
    RouterOutlet,
    RouterLink,
    RouterLinkActive,
    TomoLogo,
    ButtonModule,
    ToastModule,
    TooltipModule,
  ],
  templateUrl: './shell.html',
  styleUrl: './shell.scss',
})
export class Shell {
  readonly auth = inject(AuthService);
  readonly theme = inject(ThemeService);

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
        { path: '/settings', label: 'Configurações', icon: 'pi pi-sliders-h' },
      ],
    },
  ];
}
