import { Injectable, signal } from '@angular/core';
import { TranslateService } from '@ngx-translate/core';
import { PrimeNG } from 'primeng/config';

import { appSettings } from '../config/app-settings';

export type AppLanguage = 'en' | 'pl';

const STORAGE_KEY = 'taskhub.language';
const SUPPORTED_LANGUAGES: AppLanguage[] = ['en', 'pl'];

@Injectable({ providedIn: 'root' })
export class LanguageService {
  readonly current = signal<AppLanguage>(this.resolveInitialLanguage());

  constructor(
    private readonly translate: TranslateService,
    private readonly primeng: PrimeNG
  ) {
    this.translate.addLangs(SUPPORTED_LANGUAGES);
    this.translate.setDefaultLang('en');
    this.translate.use(this.current());
    this.applyCalendarNames(this.current());
  }

  setLanguage(language: AppLanguage): void {
    this.current.set(language);
    localStorage.setItem(STORAGE_KEY, language);
    this.translate.use(language);
    this.applyCalendarNames(language);
  }

  // Date pickers take their month and day names from PrimeNG; they are generated for the chosen language.
  private applyCalendarNames(language: AppLanguage): void {
    const monthNames = this.names(12, (index) => new Date(2024, index, 1), language, { month: 'long' });
    const monthNamesShort = this.names(12, (index) => new Date(2024, index, 1), language, { month: 'short' });
    // 2024-09-01 was a Sunday, which is where PrimeNG starts its list of days.
    const dayNames = this.names(7, (index) => new Date(2024, 8, 1 + index), language, { weekday: 'long' });
    const dayNamesShort = this.names(7, (index) => new Date(2024, 8, 1 + index), language, { weekday: 'short' });

    this.primeng.setTranslation({
      monthNames,
      monthNamesShort,
      dayNames,
      dayNamesShort,
      dayNamesMin: dayNamesShort.map((name) => name.slice(0, 2)),
      firstDayOfWeek: language === 'pl' ? 1 : 0
    });
  }

  private names(
    count: number,
    dateOf: (index: number) => Date,
    language: AppLanguage,
    options: Intl.DateTimeFormatOptions
  ): string[] {
    const formatter = new Intl.DateTimeFormat(language, options);

    return Array.from({ length: count }, (_, index) => formatter.format(dateOf(index)));
  }

  private resolveInitialLanguage(): AppLanguage {
    const stored = localStorage.getItem(STORAGE_KEY) as AppLanguage | null;
    if (stored && SUPPORTED_LANGUAGES.includes(stored)) {
      return stored;
    }

    const browserLang = navigator.language?.slice(0, 2) as AppLanguage | undefined;
    if (browserLang && SUPPORTED_LANGUAGES.includes(browserLang)) {
      return browserLang;
    }

    return SUPPORTED_LANGUAGES.includes(appSettings.defaultLanguage) ? appSettings.defaultLanguage : 'en';
  }
}
