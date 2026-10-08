import { FormControl } from '@angular/forms';
import { PasswordPolicy } from '../models/password-policy.model';
import { DEFAULT_PASSWORD_POLICY, passwordPolicyValidator, passwordRules } from './password-policy';

const EVERYTHING: PasswordPolicy = {
  minLength: 10,
  requireUppercase: true,
  requireLowercase: true,
  requireDigit: true,
  requireNonAlphanumeric: true
};

const NOTHING: PasswordPolicy = {
  minLength: 4,
  requireUppercase: false,
  requireLowercase: false,
  requireDigit: false,
  requireNonAlphanumeric: false
};

function unmet(password: string, policy: PasswordPolicy): string[] {
  return passwordRules(password, policy)
    .filter((rule) => !rule.met)
    .map((rule) => rule.labelKey);
}

describe('passwordRules', () => {
  it('lists only the rules the policy turns on', () => {
    expect(passwordRules('', NOTHING).map((rule) => rule.labelKey)).toEqual(['passwordPolicy.minLength']);
    expect(passwordRules('', EVERYTHING).length).toBe(5);
  });

  it('passes the minimum length to the label', () => {
    expect(passwordRules('', EVERYTHING)[0].labelParams).toEqual({ count: 10 });
  });

  it('accepts a password that meets every rule', () => {
    expect(unmet('Str0ng-Passw0rd', EVERYTHING)).toEqual([]);
  });

  it('names each rule a password breaks', () => {
    expect(unmet('short', EVERYTHING)).toEqual([
      'passwordPolicy.minLength',
      'passwordPolicy.uppercase',
      'passwordPolicy.digit',
      'passwordPolicy.nonAlphanumeric'
    ]);
    expect(unmet('ALLUPPERCASE1!', EVERYTHING)).toEqual(['passwordPolicy.lowercase']);
    expect(unmet('NoDigitsHere!', EVERYTHING)).toEqual(['passwordPolicy.digit']);
    expect(unmet('NoSpecial123', EVERYTHING)).toEqual(['passwordPolicy.nonAlphanumeric']);
  });

  it('counts letters outside ASCII the way the server does', () => {
    expect(unmet('Żółć12345!', EVERYTHING)).toEqual([]);
    expect(unmet('żółć12345!', EVERYTHING)).toEqual(['passwordPolicy.uppercase']);
  });

  it('treats a space as a non-alphanumeric character but not as a letter or digit', () => {
    expect(unmet('Passw0rd with space', EVERYTHING)).toEqual([]);
    expect(unmet('          ', EVERYTHING)).toEqual(['passwordPolicy.uppercase', 'passwordPolicy.lowercase', 'passwordPolicy.digit']);
  });
});

describe('passwordPolicyValidator', () => {
  it('reports an error until the password meets the policy', () => {
    const control = new FormControl('weak', passwordPolicyValidator(() => DEFAULT_PASSWORD_POLICY));

    expect(control.errors).toEqual({ passwordPolicy: true });

    control.setValue('Good-Passw0rd');

    expect(control.errors).toBeNull();
  });

  it('reads the policy on every check, so a policy that arrives later applies', () => {
    let policy = NOTHING;
    const control = new FormControl('abcd', passwordPolicyValidator(() => policy));

    expect(control.valid).toBeTrue();

    policy = EVERYTHING;
    control.updateValueAndValidity();

    expect(control.valid).toBeFalse();
  });

  it('treats a missing value as an empty password', () => {
    const control = new FormControl<string | null>(null, passwordPolicyValidator(() => NOTHING));

    expect(control.errors).toEqual({ passwordPolicy: true });
  });
});
