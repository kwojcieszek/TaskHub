import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Card } from 'primeng/card';
import { Button } from 'primeng/button';
import { InputText } from 'primeng/inputtext';
import { Message } from 'primeng/message';
import { Password } from 'primeng/password';
import { TranslateModule } from '@ngx-translate/core';
import { AuthService } from '../../../core/services/auth.service';
import { TwoFactorSetupResponse } from '../../../core/models/auth.model';
import { GoogleIdentityService } from '../../../core/services/google-identity.service';
import { GoogleButtonComponent } from '../../../shared/google-button/google-button.component';
import { ApiTokensComponent } from './api-tokens/api-tokens.component';
import { ChangePasswordComponent } from './change-password/change-password.component';
import { LoginHistoryComponent } from './login-history/login-history.component';

const CONFLICT = 409;

@Component({
  selector: 'app-profile',
  standalone: true,
  imports: [
    ReactiveFormsModule,
    Card,
    Button,
    InputText,
    Password,
    Message,
    TranslateModule,
    ChangePasswordComponent,
    LoginHistoryComponent,
    ApiTokensComponent,
    GoogleButtonComponent
  ],
  templateUrl: './profile.component.html',
  styleUrl: './profile.component.scss'
})
export class ProfileComponent implements OnInit {
  readonly auth = inject(AuthService);
  readonly google = inject(GoogleIdentityService);
  private readonly fb = inject(FormBuilder);

  readonly googleErrorKey = signal<string | null>(null);

  readonly setupResponse = signal<TwoFactorSetupResponse | null>(null);
  readonly errorMessage = signal<string | null>(null);

  readonly confirmForm = this.fb.nonNullable.group({
    code: ['', [Validators.required, Validators.minLength(6), Validators.maxLength(6)]]
  });

  // Removing the second factor needs the password again, so a stolen session cannot do it.
  readonly disableForm = this.fb.nonNullable.group({
    password: ['', Validators.required]
  });
  readonly disableErrorKey = signal<string | null>(null);

  ngOnInit(): void {
    this.google.loadConfig();
    // The stored user may predate the Google fields, so take the current state from the server.
    this.auth.refreshCurrentUser().subscribe();
  }

  startSetup(): void {
    this.errorMessage.set(null);
    this.auth.beginTwoFactorSetup().subscribe((response) => this.setupResponse.set(response));
  }

  confirmSetup(): void {
    if (this.confirmForm.invalid) {
      return;
    }

    this.auth.enableTwoFactor(this.confirmForm.getRawValue().code).subscribe({
      next: () => {
        this.setupResponse.set(null);
        this.confirmForm.reset();
      },
      error: () => this.errorMessage.set('Invalid code.')
    });
  }

  disable(): void {
    if (this.disableForm.invalid) {
      return;
    }

    this.disableErrorKey.set(null);

    this.auth.disableTwoFactor(this.disableForm.getRawValue().password).subscribe({
      next: () => this.disableForm.reset(),
      error: () => this.disableErrorKey.set('profile.invalidPassword')
    });
  }

  linkGoogle(idToken: string): void {
    this.googleErrorKey.set(null);

    this.auth.linkGoogle(idToken).subscribe({
      error: (error: HttpErrorResponse) =>
        this.googleErrorKey.set(error.status === CONFLICT ? 'profile.googleAlreadyLinked' : 'profile.googleLinkError')
    });
  }

  unlinkGoogle(): void {
    this.googleErrorKey.set(null);

    this.auth.unlinkGoogle().subscribe({
      error: () => this.googleErrorKey.set('profile.googleUnlinkError')
    });
  }
}
