import { Injectable, signal } from '@angular/core';

const KEY = 'tomo.theme';

/** Alterna o tema escuro (padrão) / claro, persistindo a escolha e movendo a classe `.app-dark`. */
@Injectable({ providedIn: 'root' })
export class ThemeService {
  readonly dark = signal<boolean>(this.initialDark());

  constructor() {
    this.apply();
  }

  toggle(): void {
    this.dark.update((v) => !v);
    localStorage.setItem(KEY, this.dark() ? 'dark' : 'light');
    this.apply();
  }

  private initialDark(): boolean {
    const stored = localStorage.getItem(KEY);
    return stored ? stored === 'dark' : true; // escuro por padrão
  }

  private apply(): void {
    document.documentElement.classList.toggle('app-dark', this.dark());
    document
      .querySelector('meta[name="theme-color"]')
      ?.setAttribute('content', this.dark() ? '#100d0a' : '#f4ecdd');
  }
}
