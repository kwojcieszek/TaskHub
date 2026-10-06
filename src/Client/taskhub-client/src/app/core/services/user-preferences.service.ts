import { Injectable, effect, inject, untracked } from '@angular/core';
import { AuthService } from './auth.service';
import { AppLanguage, LanguageService } from './language.service';
import { ThemeMode, ThemeService } from './theme.service';
import { UserSettingService } from './user-setting.service';

const THEME_KEY = 'ui.theme';
const LANGUAGE_KEY = 'ui.language';
const THEME_MODES: ThemeMode[] = ['light', 'dark', 'auto'];
const LANGUAGES: AppLanguage[] = ['en', 'pl'];

/**
 * Keeps the theme and language of the signed-in user in sync with their settings stored in the API.
 * localStorage stays the source for the sign-in pages and for instant start-up.
 */
@Injectable({ providedIn: 'root' })
export class UserPreferencesService {
  private readonly auth = inject(AuthService);
  private readonly theme = inject(ThemeService);
  private readonly language = inject(LanguageService);
  private readonly userSettingService = inject(UserSettingService);

  private loaded = false;
  private readonly synced: Record<string, string | undefined> = {};

  constructor() {
    effect(() => {
      const authenticated = this.auth.isAuthenticated();
      untracked(() => {
        if (authenticated) {
          this.load();
        } else {
          this.reset();
        }
      });
    });

    effect(() => {
      const values = { [THEME_KEY]: this.theme.mode(), [LANGUAGE_KEY]: this.language.current() };
      untracked(() => this.save(values));
    });
  }

  private load(): void {
    this.userSettingService.getAll().subscribe({
      next: (settings) => {
        for (const setting of settings) {
          this.synced[setting.key] = setting.value;
        }

        const theme = this.synced[THEME_KEY] as ThemeMode | undefined;
        if (theme && THEME_MODES.includes(theme)) {
          this.theme.setMode(theme);
        }

        const language = this.synced[LANGUAGE_KEY] as AppLanguage | undefined;
        if (language && LANGUAGES.includes(language)) {
          this.language.setLanguage(language);
        }

        this.loaded = true;
        this.save({ [THEME_KEY]: this.theme.mode(), [LANGUAGE_KEY]: this.language.current() });
      },
      error: () => {
        // The preferences keep working from localStorage when the API is unavailable.
      }
    });
  }

  private save(values: Record<string, string>): void {
    if (!this.loaded) {
      return;
    }

    for (const [key, value] of Object.entries(values)) {
      if (this.synced[key] === value) {
        continue;
      }

      const previous = this.synced[key];
      this.synced[key] = value;
      this.userSettingService.set(key, value).subscribe({
        error: () => {
          this.synced[key] = previous;
        }
      });
    }
  }

  private reset(): void {
    this.loaded = false;
    for (const key of Object.keys(this.synced)) {
      delete this.synced[key];
    }
  }
}
