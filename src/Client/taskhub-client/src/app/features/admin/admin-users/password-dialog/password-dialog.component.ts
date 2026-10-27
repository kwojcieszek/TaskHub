import { AdminService } from '../../../../core/services/admin.service';
import { DialogSave } from '../../../../shared/dialog-save';
import { Component, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { DynamicDialogConfig, DynamicDialogRef } from 'primeng/dynamicdialog';
import { Button } from 'primeng/button';
import { Password } from 'primeng/password';
import { TranslateModule } from '@ngx-translate/core';
import { AuthService } from '../../../../core/services/auth.service';
import { MAX_PASSWORD_LENGTH, passwordPolicyValidator } from '../../../../core/utils/password-policy';
import { PasswordRulesComponent } from '../../../../shared/password-rules/password-rules.component';

@Component({
  selector: 'app-password-dialog',
  standalone: true,
  imports: [ReactiveFormsModule, Password, Button, TranslateModule, PasswordRulesComponent],
  templateUrl: './password-dialog.component.html'
})
export class PasswordDialogComponent {
  private readonly fb = inject(FormBuilder);
  private readonly dialogRef = inject(DynamicDialogRef);
  readonly save = new DialogSave(this.dialogRef);
  private readonly config = inject(DynamicDialogConfig);
  private readonly persistence = inject(AdminService);
  private readonly authService = inject(AuthService);

  readonly form = this.fb.nonNullable.group({
    newPassword: ['', [Validators.required, passwordPolicyValidator(() => this.authService.passwordPolicy()), Validators.maxLength(MAX_PASSWORD_LENGTH)]]
  });

  submit(): void {
    if (this.form.invalid) {
      return;
    }
    this.save.submit(this.persistence.resetPassword(this.config.data.userId, this.form.getRawValue().newPassword));
  }

  cancel(): void {
    this.dialogRef.close();
  }
}
