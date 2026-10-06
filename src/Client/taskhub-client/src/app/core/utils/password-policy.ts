import { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';
import { PasswordPolicy } from '../models/password-policy.model';
import { PasswordRule } from '../models/password-rule.model';

export const MAX_PASSWORD_LENGTH = 128;
export const MIN_POLICY_LENGTH = 4;
export const MAX_POLICY_LENGTH = 64;

/** Used until the server has answered; the server applies its own policy either way. */
export const DEFAULT_PASSWORD_POLICY: PasswordPolicy = {
  minLength: 8,
  requireUppercase: true,
  requireLowercase: true,
  requireDigit: true,
  requireNonAlphanumeric: false
};

// The same character classes the server checks, so both sides agree on letters outside ASCII too.
const UPPERCASE = /\p{Lu}/u;
const LOWERCASE = /\p{Ll}/u;
const DIGIT = /\p{Nd}/u;
const NON_ALPHANUMERIC = /[^\p{L}\p{Nd}]/u;

/** The rules the policy turns on, each marked with whether the password meets it. */
export function passwordRules(password: string, policy: PasswordPolicy): PasswordRule[] {
  const rules: PasswordRule[] = [
    { labelKey: 'passwordPolicy.minLength', labelParams: { count: policy.minLength }, met: password.length >= policy.minLength }
  ];

  if (policy.requireUppercase) {
    rules.push({ labelKey: 'passwordPolicy.uppercase', met: UPPERCASE.test(password) });
  }

  if (policy.requireLowercase) {
    rules.push({ labelKey: 'passwordPolicy.lowercase', met: LOWERCASE.test(password) });
  }

  if (policy.requireDigit) {
    rules.push({ labelKey: 'passwordPolicy.digit', met: DIGIT.test(password) });
  }

  if (policy.requireNonAlphanumeric) {
    rules.push({ labelKey: 'passwordPolicy.nonAlphanumeric', met: NON_ALPHANUMERIC.test(password) });
  }

  return rules;
}

/** The policy is read on every check, so a form keeps up when the policy arrives or changes after it was built. */
export function passwordPolicyValidator(policy: () => PasswordPolicy): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const password = (control.value as string | null) ?? '';

    return passwordRules(password, policy()).every((rule) => rule.met) ? null : { passwordPolicy: true };
  };
}
