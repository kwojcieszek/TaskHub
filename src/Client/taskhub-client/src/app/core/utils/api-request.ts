import { appSettings } from '../config/app-settings';

export function isApiRequest(url: string): boolean {
  const base = new URL(appSettings.apiUrl, window.location.origin);
  const target = new URL(url, window.location.origin);
  const path = base.pathname.replace(/\/$/, '');
  return target.origin === base.origin && (target.pathname === path || target.pathname.startsWith(path + '/'));
}
