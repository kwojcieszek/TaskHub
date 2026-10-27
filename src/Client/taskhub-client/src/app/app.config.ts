import { registerLocaleData } from '@angular/common';
import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import localePl from '@angular/common/locales/pl';
import { ApplicationConfig, provideZoneChangeDetection } from '@angular/core';
import { provideAnimationsAsync } from '@angular/platform-browser/animations/async';
import { provideRouter } from '@angular/router';
import { TranslateLoader, provideTranslateService } from '@ngx-translate/core';
import { TranslateHttpLoader } from '@ngx-translate/http-loader';
import { providePrimeNG } from 'primeng/config';
import { DialogService } from 'primeng/dynamicdialog';

import { routes } from './app.routes';
import { TaskHubPreset } from './app.theme';
import { authInterceptor } from './core/interceptors/auth.interceptor';
import { apiFeedbackInterceptor } from './core/interceptors/api-feedback.interceptor';

// English ships with Angular; every other interface language needs its date and number formats registered.
registerLocaleData(localePl);

function httpLoaderFactory(http: HttpClient): TranslateLoader {
  return new TranslateHttpLoader(http, '/i18n/', '.json');
}

export const appConfig: ApplicationConfig = {
  providers: [
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideRouter(routes),
    provideHttpClient(withInterceptors([authInterceptor, apiFeedbackInterceptor])),
    provideAnimationsAsync(),
    providePrimeNG({
      theme: {
        preset: TaskHubPreset,
        options: { darkModeSelector: '[data-theme="dark"]' }
      }
    }),
    DialogService,
    provideTranslateService({
      loader: {
        provide: TranslateLoader,
        useFactory: httpLoaderFactory,
        deps: [HttpClient]
      },
      defaultLanguage: 'en'
    })
  ]
};
