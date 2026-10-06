import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { IconField } from 'primeng/iconfield';
import { InputIcon } from 'primeng/inputicon';
import { InputText } from 'primeng/inputtext';
import { Password } from 'primeng/password';
import { Button } from 'primeng/button';
import { Message } from 'primeng/message';
import { TranslateModule } from '@ngx-translate/core';
import { AuthService } from '../../../core/services/auth.service';
import { MAX_PASSWORD_LENGTH, passwordPolicyValidator } from '../../../core/utils/password-policy';
import { PasswordRulesComponent } from '../../../shared/password-rules/password-rules.component';
import { ThemeSwitchComponent } from '../../../shared/theme-switch/theme-switch.component';

@Component({
  selector: 'app-register',
  standalone: true,
  imports: [ReactiveFormsModule, RouterLink, IconField, InputIcon, InputText, Password, Button, Message, TranslateModule, ThemeSwitchComponent, PasswordRulesComponent],
  templateUrl: './register.component.html',
  styleUrl: './register.component.scss'
})
export class RegisterComponent {
  private readonly fb = inject(FormBuilder);
  private readonly authService = inject(AuthService);
  private readonly router = inject(Router);

  readonly loading = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly success = signal(false);

  readonly form = this.fb.nonNullable.group({
    displayName: ['', Validators.required],
    userName: ['', Validators.required],
    email: ['', [Validators.required, Validators.email]],
    password: ['', [Validators.required, passwordPolicyValidator(() => this.authService.passwordPolicy()), Validators.maxLength(MAX_PASSWORD_LENGTH)]]
  });

  submit(): void {
    if (this.form.invalid) {
      return;
    }

    this.loading.set(true);
    this.errorMessage.set(null);

    this.authService.register(this.form.getRawValue()).subscribe({
      next: () => {
        this.loading.set(false);
        this.success.set(true);
        setTimeout(() => this.router.navigate(['/login']), 1200);
      },
      error: (error) => {
        this.loading.set(false);
        this.errorMessage.set(error.error?.errors?.join(' ') ?? 'Registration failed.');
      }
    });
  }
}
