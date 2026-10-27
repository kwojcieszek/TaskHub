import { HttpClient } from '@angular/common/http';
import { Injectable, signal } from '@angular/core';
import { appSettings } from '../config/app-settings';
import { AuthProviders } from '../models/auth.model';
import { GoogleButtonOptions, GoogleIdentityApi } from '../models/google-identity.model';

const SCRIPT_URL = 'https://accounts.google.com/gsi/client';

@Injectable({ providedIn: 'root' })
export class GoogleIdentityService {
  private scriptPromise: Promise<GoogleIdentityApi> | null = null;
  private configRequested = false;

  // Null until the API reports a client ID; stays null when Google sign-in is turned off on the server.
  readonly clientId = signal<string | null>(null);

  constructor(private readonly http: HttpClient) {}

  loadConfig(): void {
    if (this.configRequested) {
      return;
    }

    this.configRequested = true;

    this.http.get<AuthProviders>(`${appSettings.apiUrl}/auth/providers`).subscribe({
      next: (providers) => this.clientId.set(providers.googleClientId),
      error: () => {
        this.configRequested = false;
      }
    });
  }

  async renderButton(
    host: HTMLElement,
    clientId: string,
    options: GoogleButtonOptions,
    onCredential: (idToken: string) => void
  ): Promise<void> {
    const google = await this.loadScript();

    google.accounts.id.initialize({
      client_id: clientId,
      callback: (response) => onCredential(response.credential)
    });

    google.accounts.id.renderButton(host, options);
  }

  private loadScript(): Promise<GoogleIdentityApi> {
    if (!this.scriptPromise) {
      this.scriptPromise = new Promise<GoogleIdentityApi>((resolve, reject) => {
        const script = document.createElement('script');
        script.src = SCRIPT_URL;
        script.async = true;
        script.onload = () => resolve((window as unknown as { google: GoogleIdentityApi }).google);
        script.onerror = () => {
          this.scriptPromise = null;
          reject(new Error('Google Identity Services could not be loaded.'));
        };
        document.head.appendChild(script);
      });
    }

    return this.scriptPromise;
  }
}
