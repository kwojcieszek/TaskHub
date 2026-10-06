import { Injectable, signal } from '@angular/core';

export type ThemeMode = 'light' | 'dark' | 'auto';

const STORAGE_KEY = 'taskhub.theme';

@Injectable({ providedIn: 'root' })
export class ThemeService {
  private readonly mediaQuery = window.matchMedia('(prefers-color-scheme: dark)');
  readonly mode = signal<ThemeMode>(this.readStoredMode());

  constructor() {
    this.applyTheme();
    this.mediaQuery.addEventListener('change', () => {
      if (this.mode() === 'auto') {
        this.applyTheme();
      }
    });
  }

  setMode(mode: ThemeMode): void {
    this.mode.set(mode);
    localStorage.setItem(STORAGE_KEY, mode);
    this.applyTheme();
  }

  private applyTheme(): void {
    const resolved = this.mode() === 'auto' ? (this.mediaQuery.matches ? 'dark' : 'light') : this.mode();
    document.documentElement.setAttribute('data-theme', resolved);
  }

  private readStoredMode(): ThemeMode {
    const stored = localStorage.getItem(STORAGE_KEY) as ThemeMode | null;
    return stored ?? 'auto';
  }
}
