import { Component, inject } from '@angular/core';
import { RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from '../../core/auth.service';
import { TomoLogo } from '../../shared/tomo-logo';

interface NavGroup {
  title: string;
  items: { path: string; label: string }[];
}

@Component({
  selector: 'app-shell',
  imports: [RouterOutlet, RouterLink, RouterLinkActive, TomoLogo],
  templateUrl: './shell.html',
  styleUrl: './shell.scss',
})
export class Shell {
  readonly auth = inject(AuthService);

  readonly groups: NavGroup[] = [
    { title: 'Visão geral', items: [{ path: '/dashboard', label: 'Dashboard' }] },
    {
      title: 'Conteúdo',
      items: [
        { path: '/niches', label: 'Nichos' },
        { path: '/products', label: 'Produtos' },
      ],
    },
    {
      title: 'Operação',
      items: [
        { path: '/jobs', label: 'Jobs' },
        { path: '/settings', label: 'Configurações' },
      ],
    },
  ];
}
