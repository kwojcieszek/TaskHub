import { environment } from '../../../environments/environment';

export function isApiRequest(url: string): boolean {
  const base = new URL(environment.apiUrl, window.location.origin);
  const target = new URL(url, window.location.origin);
  const path = base.pathname.replace(/\/$/, '');
  return target.origin === base.origin && (target.pathname === path || target.pathname.startsWith(path + '/'));
}
