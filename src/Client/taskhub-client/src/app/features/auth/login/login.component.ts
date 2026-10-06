import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { ReactiveFormsModule, FormBuilder, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { IconField } from 'primeng/iconfield';
import { InputIcon } from 'primeng/inputicon';
import { InputText } from 'primeng/inputtext';
import { Password } from 'primeng/password';
import { Button } from 'primeng/button';
import { Message } from 'primeng/message';
import { TranslateModule } from '@ngx-translate/core';
import { AuthService } from '../../../core/services/auth.service';
import { GoogleIdentityService } from '../../../core/services/google-identity.service';
import { LoginResponse } from '../../../core/models/auth.model';
import { GoogleButtonComponent } from '../../../shared/google-button/google-button.component';
import { ThemeSwitchComponent } from '../../../shared/theme-switch/theme-switch.component';

const TOO_MANY_REQUESTS = 429;

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink, IconField, InputIcon, InputText, Password, Button, Message, TranslateModule, ThemeSwitchComponent, GoogleButtonComponent],
  templateUrl: './login.component.html',
  styleUrl: './login.component.scss'
})
export class LoginComponent {
  private readonly fb = inject(FormBuilder);
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);

  readonly google = inject(GoogleIdentityService);

  readonly loading = signal(false);
  readonly errorMessage = signal<string | null>(null);

  readonly form = this.fb.nonNullable.group({
    userName: ['', Validators.required],
    password: ['', Validators.required]
  });

  submit(): void {
    if (this.form.invalid) {
      return;
    }

    this.loading.set(true);
    this.errorMessage.set(null);

    this.authService.login(this.form.getRawValue()).subscribe({
      next: (response) => this.completeLogin(response),
      error: (error: HttpErrorResponse) => {
        this.loading.set(false);
        this.errorMessage.set(error.status === TOO_MANY_REQUESTS ? 'auth.tooManyRequests' : 'auth.invalidCredentials');
      }
    });
  }

  loginWithGoogle(idToken: string): void {
    this.loading.set(true);
    this.errorMessage.set(null);

    this.authService.loginWithGoogle(idToken).subscribe({
      next: (response) => this.completeLogin(response),
      error: (error: HttpErrorResponse) => {
        this.loading.set(false);
        this.errorMessage.set(error.status === TOO_MANY_REQUESTS ? 'auth.tooManyRequests' : 'auth.googleLoginFailed');
      }
    });
  }

  private completeLogin(response: LoginResponse): void {
    this.loading.set(false);

    if (response.requiresTwoFactor && response.twoFactorToken) {
      this.router.navigate(['/two-factor'], { state: { twoFactorToken: response.twoFactorToken } });
    } else {
      this.router.navigate(['/app/dashboard']);
    }
  }
}
