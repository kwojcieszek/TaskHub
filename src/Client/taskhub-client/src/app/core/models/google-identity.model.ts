export type GoogleButtonText = 'signin_with' | 'continue_with';

export interface GoogleCredentialResponse {
  credential: string;
}

export interface GoogleIdConfiguration {
  client_id: string;
  callback: (response: GoogleCredentialResponse) => void;
}

export interface GoogleButtonOptions {
  type: 'standard';
  theme: 'outline' | 'filled_black';
  size: 'large';
  text: GoogleButtonText;
  width: number;
  locale: string;
}

// The part of the Google Identity Services script (accounts.google.com/gsi/client) the app uses.
export interface GoogleIdentityApi {
  accounts: {
    id: {
      initialize(configuration: GoogleIdConfiguration): void;
      renderButton(parent: HTMLElement, options: GoogleButtonOptions): void;
    };
  };
}
