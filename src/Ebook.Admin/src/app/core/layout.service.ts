import { Injectable, signal } from '@angular/core';

const KEY = 'tomo.sidebar';
const MOBILE_MQ = '(max-width: 860px)';

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

  /** true = viewport mobile (≤860px). Atualizado via matchMedia. */
  private readonly _isMobile = signal(window.matchMedia(MOBILE_MQ).matches);
  readonly isMobile = this._isMobile.asReadonly();

  constructor() {
    const mql = window.matchMedia(MOBILE_MQ);
    mql.addEventListener('change', (e) => this._isMobile.set(e.matches));
  }

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
