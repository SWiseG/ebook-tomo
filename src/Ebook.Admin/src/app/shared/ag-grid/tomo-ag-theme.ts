import { themeQuartz } from 'ag-grid-community';

/**
 * Tema AG Grid para o Tomo: cores via CSS vars PrimeNG, tipografia Inter herdada,
 * bordas quentes do design system. Adapta-se automaticamente ao dark mode (.app-dark).
 */
export const tomoAgTheme = themeQuartz.withParams({
  // Cores via CSS vars — respondem ao .app-dark sem configuração extra
  accentColor: 'var(--p-primary-color)',
  backgroundColor: 'var(--p-content-background)',
  foregroundColor: 'var(--p-text-color)',
  borderColor: 'var(--p-content-border-color)',
  headerBackgroundColor: 'var(--app-ground)',
  headerTextColor: 'var(--p-text-muted-color)',
  rowHoverColor: 'var(--p-content-hover-background)',
  oddRowBackgroundColor: 'transparent',

  // Tipografia (Inter já carregada globalmente)
  headerFontSize: 11,
  headerFontWeight: 600,

  // Dimensões
  headerHeight: 40,
  rowHeight: 52,
  cellHorizontalPaddingScale: 1.1,

  // Bordas do container
  wrapperBorderRadius: 12,
  wrapperBorder: { color: 'var(--p-content-border-color)', width: 1, style: 'solid' },
});
