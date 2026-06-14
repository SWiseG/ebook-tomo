import { ChangeDetectionStrategy, Component, input } from '@angular/core';
import { SkeletonModule } from 'primeng/skeleton';

type LoadingVariant = 'kpi' | 'table' | 'detail';

/**
 * Esqueletos de carregamento reutilizáveis (feedback visual no lugar de "Carregando…").
 * Usa p-skeleton (shimmer nativo do PrimeNG).
 */
@Component({
  selector: 'app-loading',
  changeDetection: ChangeDetectionStrategy.OnPush,
  imports: [SkeletonModule],
  template: `
    @switch (variant()) {
      @case ('kpi') {
        <div class="sk-kpis">
          @for (i of slots(); track i) {
            <div class="sk-kpi">
              <p-skeleton shape="circle" size="2.6rem" />
              <div class="sk-kpi__body">
                <p-skeleton width="60%" height="1.4rem" />
                <p-skeleton width="80%" height="0.8rem" />
              </div>
            </div>
          }
        </div>
      }
      @case ('detail') {
        <div class="sk-detail">
          <p-skeleton width="40%" height="2rem" />
          <p-skeleton width="25%" height="1rem" />
          <p-skeleton width="100%" height="220px" borderRadius="16px" />
        </div>
      }
      @default {
        <div class="sk-table">
          <p-skeleton width="100%" height="2.4rem" />
          @for (i of slots(); track i) {
            <p-skeleton width="100%" height="3rem" />
          }
        </div>
      }
    }
  `,
  styles: [
    `
      .sk-kpis {
        display: grid;
        grid-template-columns: repeat(auto-fill, minmax(210px, 1fr));
        gap: 16px;
      }
      .sk-kpi {
        display: flex;
        gap: 12px;
        align-items: center;
        padding: 20px 24px;
        border: 1px solid var(--p-content-border-color);
        border-radius: 16px;
        background: var(--p-content-background);
      }
      .sk-kpi__body {
        display: flex;
        flex-direction: column;
        gap: 8px;
        flex: 1;
      }
      .sk-table {
        display: flex;
        flex-direction: column;
        gap: 8px;
      }
      .sk-detail {
        display: flex;
        flex-direction: column;
        gap: 14px;
      }
    `,
  ],
})
export class Loading {
  readonly variant = input<LoadingVariant>('table');
  readonly rows = input(5);

  slots() {
    return Array.from({ length: this.rows() }, (_, i) => i);
  }
}
