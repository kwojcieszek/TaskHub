import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router } from '@angular/router';
import { IconField } from 'primeng/iconfield';
import { InputIcon } from 'primeng/inputicon';
import { InputText } from 'primeng/inputtext';
import { Button } from 'primeng/button';
import { Message } from 'primeng/message';
import { TranslateModule } from '@ngx-translate/core';
import { AuthService } from '../../../core/services/auth.service';
import { ThemeSwitchComponent } from '../../../shared/theme-switch/theme-switch.component';

@Component({
  selector: 'app-two-factor-verify',
  standalone: true,
  imports: [ReactiveFormsModule, IconField, InputIcon, InputText, Button, Message, TranslateModule, ThemeSwitchComponent],
  templateUrl: './two-factor-verify.component.html',
  styleUrl: './two-factor-verify.component.scss'
})
export class TwoFactorVerifyComponent {
  private readonly fb = inject(FormBuilder);
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);

  readonly loading = signal(false);
  readonly errorMessage = signal<string | null>(null);

  private readonly twoFactorToken: string | undefined = (history.state as { twoFactorToken?: string })?.twoFactorToken;

  readonly form = this.fb.nonNullable.group({
    code: ['', [Validators.required, Validators.minLength(6), Validators.maxLength(6)]]
  });

  constructor() {
    if (!this.twoFactorToken) {
      this.router.navigate(['/login']);
    }
  }

  submit(): void {
    if (this.form.invalid || !this.twoFactorToken) {
      return;
    }

    this.loading.set(true);
    this.errorMessage.set(null);

    this.authService.verifyTwoFactor({ twoFactorToken: this.twoFactorToken, code: this.form.getRawValue().code }).subscribe({
      next: () => {
        this.loading.set(false);
        this.router.navigate(['/app/dashboard']);
      },
      error: () => {
        this.loading.set(false);
        this.errorMessage.set('Invalid code.');
      }
    });
  }
}
