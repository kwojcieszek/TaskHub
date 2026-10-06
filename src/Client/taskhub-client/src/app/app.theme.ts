import { definePreset } from '@primeuix/themes';
import Aura from '@primeuix/themes/aura';

const slateSurface = {
  0: '#ffffff',
  50: '{slate.50}',
  100: '{slate.100}',
  200: '{slate.200}',
  300: '{slate.300}',
  400: '{slate.400}',
  500: '{slate.500}',
  600: '{slate.600}',
  700: '{slate.700}',
  800: '{slate.800}',
  900: '{slate.900}',
  950: '{slate.950}'
};

const navySurface = {
  0: '#ffffff',
  50: '#f1f5fb',
  100: '#dfe7f3',
  200: '#c2cfe4',
  300: '#97a9c9',
  400: '#6b80a6',
  500: '#4b5f85',
  600: '#36486b',
  700: '#26334f',
  800: '#1b2740',
  900: '#111a2e',
  950: '#0b1220'
};

export const TaskHubPreset = definePreset(Aura, {
  semantic: {
    primary: {
      50: '#eff6ff',
      100: '#e0eaff',
      200: '#bfd3fe',
      300: '#93b4fd',
      400: '#60a5fa',
      500: '#3b82f6',
      600: '#2563eb',
      700: '#1d4ed8',
      800: '#1e40af',
      900: '#1e3a8a',
      950: '#172554'
    },
    colorScheme: {
      light: {
        surface: slateSurface,
        primary: {
          color: '{primary.400}',
          contrastColor: '#ffffff',
          hoverColor: '{primary.500}',
          activeColor: '{primary.600}'
        },
        highlight: {
          background: '{primary.50}',
          focusBackground: '{primary.100}',
          color: '{primary.600}',
          focusColor: '{primary.700}'
        }
      },
      dark: {
        surface: navySurface,
        primary: {
          color: '{primary.400}',
          contrastColor: '{surface.900}',
          hoverColor: '{primary.300}',
          activeColor: '{primary.200}'
        },
        highlight: {
          background: 'color-mix(in srgb, {primary.400}, transparent 84%)',
          focusBackground: 'color-mix(in srgb, {primary.400}, transparent 76%)',
          color: 'rgba(255,255,255,.87)',
          focusColor: 'rgba(255,255,255,.87)'
        }
      }
    }
  }
});
