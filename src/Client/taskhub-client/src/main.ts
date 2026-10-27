import { bootstrapApplication } from '@angular/platform-browser';
import { appConfig } from './app/app.config';
import { AppComponent } from './app/app.component';
import { loadAppSettings } from './app/core/config/app-settings';

loadAppSettings()
  .then(() => bootstrapApplication(AppComponent, appConfig))
  .catch((err) => console.error(err));
