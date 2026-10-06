import { PasswordPolicy } from './password-policy.model';

export interface SecuritySettings {
  lockoutEnabled: boolean;
  maxFailedAccessAttempts: number;
  lockoutMinutes: number;
  registrationEnabled: boolean;
  passwordPolicy: PasswordPolicy;
}
