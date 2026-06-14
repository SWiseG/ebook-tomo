import { definePreset } from '@primeuix/themes';
import Aura from '@primeuix/themes/aura';

/**
 * Tema "Tomo" sobre o preset Aura.
 * - Claro: primária âmbar + superfícies bege/creme.
 * - Escuro: primária índigo + superfícies escuras (mantém a identidade atual).
 * O modo escuro é ativado pela classe `.app-dark` (ver ThemeService / providePrimeNG).
 */
export const TomoPreset = definePreset(Aura, {
  semantic: {
    primary: {
      50: '{amber.50}',
      100: '{amber.100}',
      200: '{amber.200}',
      300: '{amber.300}',
      400: '{amber.400}',
      500: '{amber.500}',
      600: '{amber.600}',
      700: '{amber.700}',
      800: '{amber.800}',
      900: '{amber.900}',
      950: '{amber.950}',
    },
    colorScheme: {
      light: {
        primary: {
          color: '{amber.600}',
          contrastColor: '#ffffff',
          hoverColor: '{amber.700}',
          activeColor: '{amber.800}',
        },
        highlight: {
          background: '{amber.100}',
          focusBackground: '{amber.200}',
          color: '{amber.800}',
          focusColor: '{amber.900}',
        },
        // rampa bege/creme: 0 = papel claro, 950 = marrom escuro (texto)
        surface: {
          0: '#fffdf7',
          50: '#faf3e3',
          100: '#f3e9d3',
          200: '#e9dcc2',
          300: '#ddcba8',
          400: '#cdb98f',
          500: '#b7a275',
          600: '#9a8961',
          700: '#6f6149',
          800: '#4a4031',
          900: '#2b2117',
          950: '#1a1410',
        },
      },
      dark: {
        primary: {
          color: '{indigo.400}',
          contrastColor: '{indigo.950}',
          hoverColor: '{indigo.300}',
          activeColor: '{indigo.200}',
        },
        highlight: {
          background: 'rgba(99, 102, 241, 0.16)',
          focusBackground: 'rgba(99, 102, 241, 0.24)',
          color: 'rgba(255, 255, 255, 0.87)',
          focusColor: 'rgba(255, 255, 255, 0.87)',
        },
        // rampa azul-grafite: 0 = claro (texto), 950 = quase preto (fundo)
        surface: {
          0: '#f4f5f8',
          50: '#e7e9f0',
          100: '#c7ccd6',
          200: '#9aa1b2',
          300: '#6b7280',
          400: '#3a3f4e',
          500: '#2a2e3a',
          600: '#262a36',
          700: '#1e212b',
          800: '#16181f',
          900: '#121419',
          950: '#0e0f14',
        },
      },
    },
  },
});
