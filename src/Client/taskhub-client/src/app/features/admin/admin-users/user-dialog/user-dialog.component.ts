import { Component, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { DynamicDialogRef } from 'primeng/dynamicdialog';
import { Button } from 'primeng/button';
import { InputText } from 'primeng/inputtext';
import { Password } from 'primeng/password';
import { TranslateModule } from '@ngx-translate/core';
import { AuthService } from '../../../../core/services/auth.service';
import { MAX_PASSWORD_LENGTH, passwordPolicyValidator } from '../../../../core/utils/password-policy';
import { PasswordRulesComponent } from '../../../../shared/password-rules/password-rules.component';

const MIN_USER_NAME_LENGTH = 3;

@Component({
  selector: 'app-user-dialog',
  standalone: true,
  imports: [ReactiveFormsModule, InputText, Password, Button, TranslateModule, PasswordRulesComponent],
  templateUrl: './user-dialog.component.html'
})
export class UserDialogComponent {
  private readonly fb = inject(FormBuilder);
  private readonly dialogRef = inject(DynamicDialogRef);
  private readonly authService = inject(AuthService);

  readonly form = this.fb.nonNullable.group({
    userName: ['', [Validators.required, Validators.minLength(MIN_USER_NAME_LENGTH), Validators.maxLength(50)]],
    email: ['', [Validators.required, Validators.email, Validators.maxLength(256)]],
    displayName: ['', Validators.maxLength(100)],
    password: ['', [Validators.required, passwordPolicyValidator(() => this.authService.passwordPolicy()), Validators.maxLength(MAX_PASSWORD_LENGTH)]]
  });

  submit(): void {
    if (this.form.invalid) {
      return;
    }
    this.dialogRef.close(this.form.getRawValue());
  }

  cancel(): void {
    this.dialogRef.close();
  }
}
