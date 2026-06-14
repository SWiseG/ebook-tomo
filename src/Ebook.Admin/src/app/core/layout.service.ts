import { Injectable, signal } from '@angular/core';

const KEY = 'tomo.sidebar';

/**
 * Estado do layout: sidebar retrátil (rail de ícones ↔ expandida), persistida,
 * e o drawer móvel (overlay) controlado separadamente.
 */
@Injectable({ providedIn: 'root' })
export class LayoutService {
  /** true = rail compacto (apenas ícones). Persistido em localStorage. */
  readonly collapsed = signal<boolean>(localStorage.getItem(KEY) === '1');

  /** true = drawer aberto no mobile (overlay com backdrop). */
  readonly mobileOpen = signal<boolean>(false);

  toggleCollapsed(): void {
    this.collapsed.update((v) => !v);
    localStorage.setItem(KEY, this.collapsed() ? '1' : '0');
  }

  openMobile(): void {
    this.mobileOpen.set(true);
  }

  closeMobile(): void {
    this.mobileOpen.set(false);
  }

  toggleMobile(): void {
    this.mobileOpen.update((v) => !v);
  }
}
