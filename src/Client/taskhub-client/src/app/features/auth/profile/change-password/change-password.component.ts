import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Button } from 'primeng/button';
import { Card } from 'primeng/card';
import { Password } from 'primeng/password';
import { TranslateModule } from '@ngx-translate/core';
import { AuthService } from '../../../../core/services/auth.service';
import { MAX_PASSWORD_LENGTH, passwordPolicyValidator } from '../../../../core/utils/password-policy';
import { PasswordRulesComponent } from '../../../../shared/password-rules/password-rules.component';

@Component({
  selector: 'app-change-password',
  standalone: true,
  imports: [ReactiveFormsModule, Card, Button, Password, TranslateModule, PasswordRulesComponent],
  templateUrl: './change-password.component.html'
})
export class ChangePasswordComponent {
  readonly auth = inject(AuthService);
  private readonly fb = inject(FormBuilder);

  readonly saving = signal(false);
  readonly saved = signal(false);
  readonly errorKey = signal<string | null>(null);

  readonly form = this.fb.nonNullable.group({
    currentPassword: ['', Validators.required],
    newPassword: ['', [Validators.required, passwordPolicyValidator(() => this.auth.passwordPolicy()), Validators.maxLength(MAX_PASSWORD_LENGTH)]],
    repeatPassword: ['', Validators.required]
  });

  get passwordsDiffer(): boolean {
    const { newPassword, repeatPassword } = this.form.getRawValue();

    return repeatPassword !== '' && newPassword !== repeatPassword;
  }

  submit(): void {
    if (this.form.invalid || this.passwordsDiffer) {
      return;
    }

    const { currentPassword, newPassword } = this.form.getRawValue();

    this.saving.set(true);
    this.saved.set(false);
    this.errorKey.set(null);

    this.auth.changePassword(currentPassword, newPassword).subscribe({
      next: () => {
        this.saving.set(false);
        this.saved.set(true);
        this.form.reset();
      },
      error: () => {
        this.saving.set(false);
        this.errorKey.set('profile.passwordChangeError');
      }
    });
  }
}
