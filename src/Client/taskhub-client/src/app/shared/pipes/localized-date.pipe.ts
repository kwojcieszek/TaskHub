import { formatDate } from '@angular/common';
import { Pipe, PipeTransform, inject } from '@angular/core';
import { LanguageService } from '../../core/services/language.service';

type DateInput = Date | string | number | null | undefined;

/**
 * Drop-in replacement for Angular's date pipe that formats in the language chosen in the application
 * instead of the build-time locale. It is impure so that open views follow a language switch.
 */
@Pipe({ name: 'date', standalone: true, pure: false })
export class LocalizedDatePipe implements PipeTransform {
  private readonly language = inject(LanguageService);

  private lastKey: string | null = null;
  private lastResult: string | null = null;

  transform(value: DateInput, format = 'mediumDate', timezone?: string): string | null {
    if (value === null || value === undefined || value === '') {
      return null;
    }

    const locale = this.language.current();
    const key = `${value instanceof Date ? value.getTime() : value}|${format}|${timezone ?? ''}|${locale}`;

    // The pipe runs on every change detection pass, so the previous result is reused while nothing changed.
    if (key !== this.lastKey) {
      this.lastKey = key;
      this.lastResult = formatDate(value, format, locale, timezone);
    }

    return this.lastResult;
  }
}
