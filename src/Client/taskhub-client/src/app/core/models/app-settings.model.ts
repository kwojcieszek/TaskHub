import { AppLanguage } from '../services/language.service';

// The settings read at startup from config.json, which can be changed on the server without rebuilding the client.
export interface AppSettings {
  // Base address of the API, absolute or relative to the address the client is served from.
  apiUrl: string;
  // The interface language for users who have not chosen one and whose browser language is not supported.
  defaultLanguage: AppLanguage;
}
