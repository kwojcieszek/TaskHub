import { User } from './user.model';

export interface RegisterRequest {
  userName: string;
  email: string;
  password: string;
  displayName: string;
}

export interface LoginRequest {
  userName: string;
  password: string;
}

export interface LoginResponse {
  requiresTwoFactor: boolean;
  twoFactorToken: string | null;
  accessToken: string | null;
  expiresAt: string | null;
  user: User | null;
}

export interface AuthProviders {
  googleClientId: string | null;
}

export interface TwoFactorVerifyRequest {
  twoFactorToken: string;
  code: string;
}

export interface TwoFactorSetupResponse {
  sharedKey: string;
  authenticatorUri: string;
}
