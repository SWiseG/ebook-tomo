import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/**
 * Marca do painel "Tomo" — ícone (livro aberto sobre badge índigo) + wordmark.
 * As cores são constantes da marca (independem do tema).
 */
@Component({
  selector: 'tomo-logo',
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <span class="logo" [class.logo--compact]="compact()">
      <svg viewBox="0 0 32 32" width="32" height="32" aria-hidden="true">
        <rect x="0" y="0" width="32" height="32" rx="9" fill="#6366f1" />
        <path d="M16 9.2C13.4 7.6 9.6 7.4 7 8.4v15c2.6-1 6.4-.8 9 .8z" fill="#f5a623" />
        <path d="M16 9.2c2.6-1.6 6.4-1.8 9-.8v15c-2.6-1-6.4-.8-9 .8z" fill="#fbbf24" />
        <rect x="15.2" y="9" width="1.6" height="15.4" rx="0.8" fill="#0e0f14" opacity="0.28" />
      </svg>
      @if (!compact()) {
        <span class="logo__word">
          Tomo
          <small>console</small>
        </span>
      }
    </span>
  `,
  styles: [
    `
      .logo {
        display: inline-flex;
        align-items: center;
        gap: 10px;
      }
      svg {
        flex: none;
        border-radius: 9px;
      }
      .logo__word {
        display: flex;
        flex-direction: column;
        line-height: 1.05;
        font-weight: 700;
        font-size: 1.15rem;
        letter-spacing: -0.02em;
        color: #e7e9f0;
      }
      .logo__word small {
        font-weight: 500;
        font-size: 0.62rem;
        letter-spacing: 0.18em;
        text-transform: uppercase;
        color: #9aa1b2;
      }
    `,
  ],
})
export class TomoLogo {
  readonly compact = input(false);
}
