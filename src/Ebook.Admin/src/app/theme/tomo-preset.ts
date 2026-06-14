import { definePreset } from '@primeuix/themes';
import Aura from '@primeuix/themes/aura';

/**
 * Tema "Tomo" sobre o preset Aura — fonte ÚNICA de cor da aplicação.
 * Marca unificada no ÂMBAR nos dois esquemas:
 *  - Claro: pastéis quentes (off-white / bege) + primária âmbar + texto preto-morno.
 *  - Escuro: preto/espresso (com leve calor) + primária âmbar + texto off-white.
 * O modo escuro é ativado pela classe `.app-dark` (ver ThemeService / providePrimeNG).
 * Raio/movimento/espaçamento são primitivas NÃO-cor (ver styles/_tokens.scss).
 */
export const TomoPreset = definePreset(Aura, {
  semantic: {
    // ramp âmbar usado como primária em ambos os esquemas
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
    // raio de foco coerente com a estética suave
    focusRing: {
      width: '2px',
      style: 'solid',
      color: '{primary.color}',
      offset: '2px',
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
        // rampa pastel quente: 0 = off-white (papel), 950 = preto-morno (texto/títulos)
        surface: {
          0: '#fffdf9',
          50: '#faf5ea',
          100: '#f3ebda',
          200: '#e9dec8',
          300: '#dccfb2',
          400: '#c8b894',
          500: '#ab9874',
          600: '#897959',
          700: '#655842',
          800: '#463c2c',
          900: '#2c251a',
          950: '#1b160f',
        },
      },
      dark: {
        primary: {
          color: '{amber.400}',
          contrastColor: '#1b160f',
          hoverColor: '{amber.300}',
          activeColor: '{amber.200}',
        },
        highlight: {
          background: 'rgba(245, 166, 35, 0.16)',
          focusBackground: 'rgba(245, 166, 35, 0.24)',
          color: 'rgba(255, 251, 242, 0.92)',
          focusColor: 'rgba(255, 251, 242, 0.92)',
        },
        // rampa espresso quente: 0 = off-white (texto), 950 = preto-morno (fundo)
        surface: {
          0: '#f7f4ee',
          50: '#ece7dd',
          100: '#d8d0c2',
          200: '#b3a995',
          300: '#8a8170',
          400: '#5f574a',
          500: '#463f34',
          600: '#342e26',
          700: '#28231d',
          800: '#1e1a15',
          900: '#161310',
          950: '#100d0a',
        },
      },
    },
  },
});
