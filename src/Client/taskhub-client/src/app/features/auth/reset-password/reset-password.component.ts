import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { IconField } from 'primeng/iconfield';
import { InputIcon } from 'primeng/inputicon';
import { Password } from 'primeng/password';
import { Button } from 'primeng/button';
import { Message } from 'primeng/message';
import { TranslateModule } from '@ngx-translate/core';
import { AuthService } from '../../../core/services/auth.service';
import { MAX_PASSWORD_LENGTH, passwordPolicyValidator } from '../../../core/utils/password-policy';
import { PasswordRulesComponent } from '../../../shared/password-rules/password-rules.component';
import { ThemeSwitchComponent } from '../../../shared/theme-switch/theme-switch.component';

const INVALID_LINK_MESSAGE = 'invalid or has expired';

@Component({
  selector: 'app-reset-password',
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink, IconField, InputIcon, Password, Button, Message, TranslateModule, ThemeSwitchComponent, PasswordRulesComponent],
  templateUrl: './reset-password.component.html',
  styleUrl: './reset-password.component.scss'
})
export class ResetPasswordComponent {
  private readonly fb = inject(FormBuilder);
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  private readonly email = this.route.snapshot.queryParamMap.get('email') ?? '';
  private readonly token = this.route.snapshot.queryParamMap.get('token') ?? '';

  readonly loading = signal(false);
  readonly success = signal(false);
  readonly linkInvalid = signal(this.email === '' || this.token === '');
  readonly errorKey = signal<string | null>(null);

  readonly form = this.fb.nonNullable.group({
    newPassword: ['', [Validators.required, passwordPolicyValidator(() => this.authService.passwordPolicy()), Validators.maxLength(MAX_PASSWORD_LENGTH)]],
    repeatPassword: ['', Validators.required]
  });

  get passwordsDiffer(): boolean {
    const { newPassword, repeatPassword } = this.form.getRawValue();

    return repeatPassword !== '' && newPassword !== repeatPassword;
  }

  submit(): void {
    if (this.form.invalid || this.passwordsDiffer || this.loading()) {
      return;
    }

    this.loading.set(true);
    this.errorKey.set(null);

    this.authService.resetPassword({ email: this.email, token: this.token, newPassword: this.form.getRawValue().newPassword }).subscribe({
      next: () => {
        this.loading.set(false);
        this.success.set(true);
        setTimeout(() => this.router.navigate(['/login']), 1500);
      },
      error: (error: HttpErrorResponse) => {
        this.loading.set(false);
        this.handleError(error);
      }
    });
  }

  private handleError(error: HttpErrorResponse): void {
    const errors: string[] = error.error?.errors ?? [];

    if (errors.some((message) => message.includes(INVALID_LINK_MESSAGE))) {
      this.linkInvalid.set(true);

      return;
    }

    this.errorKey.set(error.status === 429 ? 'auth.tooManyRequests' : 'auth.resetFailed');
  }
}
