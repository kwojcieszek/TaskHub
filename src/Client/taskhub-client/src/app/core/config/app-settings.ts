import { environment } from '../../../environments/environment';
import { AppSettings } from '../models/app-settings.model';

// Filled from the settings file before the application starts, so it can be read anywhere without injection.
export const appSettings: AppSettings = {
  apiUrl: '/api/v1',
  defaultLanguage: 'en'
};

export async function loadAppSettings(): Promise<void> {
  const response = await fetch(environment.settingsUrl, { cache: 'no-cache' });
  if (!response.ok) {
    throw new Error(`Could not load ${environment.settingsUrl} (HTTP ${response.status}).`);
  }

  const settings = (await response.json()) as Partial<AppSettings>;
  Object.assign(appSettings, settings);
  appSettings.apiUrl = appSettings.apiUrl.replace(/\/+$/, '');
}
