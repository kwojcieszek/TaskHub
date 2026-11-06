import { Injectable, effect, inject, signal, untracked } from '@angular/core';
import { AuthService } from './auth.service';
import { AppLanguage, LanguageService } from './language.service';
import { ThemeMode, ThemeService } from './theme.service';
import { UserSettingService } from './user-setting.service';

const THEME_KEY = 'ui.theme';
const LANGUAGE_KEY = 'ui.language';
const ASSIGN_TO_ME_KEY = 'tasks.assignToMeByDefault';
const THEME_MODES: ThemeMode[] = ['light', 'dark', 'auto'];
const LANGUAGES: AppLanguage[] = ['en', 'pl'];

/**
 * Keeps the theme, language and task defaults of the signed-in user in sync with their settings stored in the API.
 * localStorage stays the source for the sign-in pages and for instant start-up.
 */
@Injectable({ providedIn: 'root' })
export class UserPreferencesService {
  private readonly auth = inject(AuthService);
  private readonly theme = inject(ThemeService);
  private readonly language = inject(LanguageService);
  private readonly userSettingService = inject(UserSettingService);

  private readonly assignToMeByDefaultSignal = signal(false);

  private loaded = false;
  private readonly synced: Record<string, string | undefined> = {};

  // When enabled, a new task is assigned to the user who creates it.
  readonly assignToMeByDefault = this.assignToMeByDefaultSignal.asReadonly();

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
      const values = this.currentValues();
      untracked(() => this.save(values));
    });
  }

  setAssignToMeByDefault(enabled: boolean): void {
    this.assignToMeByDefaultSignal.set(enabled);
  }

  private currentValues(): Record<string, string> {
    return {
      [THEME_KEY]: this.theme.mode(),
      [LANGUAGE_KEY]: this.language.current(),
      [ASSIGN_TO_ME_KEY]: String(this.assignToMeByDefaultSignal())
    };
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

        this.assignToMeByDefaultSignal.set(this.synced[ASSIGN_TO_ME_KEY] === 'true');

        this.loaded = true;
        this.save(this.currentValues());
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
    this.assignToMeByDefaultSignal.set(false);
    for (const key of Object.keys(this.synced)) {
      delete this.synced[key];
    }
  }
}
