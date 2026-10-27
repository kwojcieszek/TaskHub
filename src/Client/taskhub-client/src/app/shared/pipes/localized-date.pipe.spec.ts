import { registerLocaleData } from '@angular/common';
import localePl from '@angular/common/locales/pl';
import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { AppLanguage, LanguageService } from '../../core/services/language.service';
import { LocalizedDatePipe } from './localized-date.pipe';

describe('LocalizedDatePipe', () => {
  const language = signal<AppLanguage>('en');

  let pipe: LocalizedDatePipe;

  beforeAll(() => registerLocaleData(localePl));

  beforeEach(() => {
    language.set('en');
    TestBed.configureTestingModule({ providers: [{ provide: LanguageService, useValue: { current: language } }] });
    pipe = TestBed.runInInjectionContext(() => new LocalizedDatePipe());
  });

  it('returns nothing for a missing value', () => {
    expect(pipe.transform(null)).toBeNull();
    expect(pipe.transform(undefined)).toBeNull();
    expect(pipe.transform('')).toBeNull();
  });

  it('formats a date with the given pattern', () => {
    expect(pipe.transform(new Date(2026, 4, 9, 14, 5), 'yyyy-MM-dd HH:mm')).toBe('2026-05-09 14:05');
  });

  it('writes names in the language of the application', () => {
    const date = new Date(2026, 4, 9);

    expect(pipe.transform(date, 'MMMM')).toBe('May');

    language.set('pl');

    expect(pipe.transform(date, 'LLLL')).toBe('maj');
  });

  it('follows a change of language for the same value', () => {
    const date = new Date(2026, 0, 15);
    const english = pipe.transform(date, 'EEEE');

    language.set('pl');
    const polish = pipe.transform(date, 'EEEE');

    expect(english).toBe('Thursday');
    expect(polish).toBe('czwartek');
  });
});
