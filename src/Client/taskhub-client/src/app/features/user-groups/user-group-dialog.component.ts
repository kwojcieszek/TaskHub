import { UserGroupService } from '../../core/services/user-group.service';
import { finalize } from 'rxjs';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { DynamicDialogConfig, DynamicDialogRef } from 'primeng/dynamicdialog';
import { InputText } from 'primeng/inputtext';
import { Textarea } from 'primeng/textarea';
import { Button } from 'primeng/button';
import { TranslateModule } from '@ngx-translate/core';
import { UserGroup } from '../../core/models/user-group.model';
import { DictationDirective } from '../../shared/dictation/dictation.directive';

@Component({
  selector: 'app-user-group-dialog',
  standalone: true,
  imports: [ReactiveFormsModule, DictationDirective, InputText, Textarea, Button, TranslateModule],
  templateUrl: './user-group-dialog.component.html'
})
export class UserGroupDialogComponent {
  private readonly fb = inject(FormBuilder);
  private readonly dialogRef = inject(DynamicDialogRef);
  private readonly config = inject(DynamicDialogConfig);

  // Given when an existing team is edited; a new team starts empty.
  private readonly group: UserGroup | undefined = this.config.data?.group;

  readonly form = this.fb.nonNullable.group({
    name: [this.group?.name ?? '', [Validators.required, Validators.maxLength(200)]],
    description: [this.group?.description ?? '', Validators.maxLength(2000)]
  });

  readonly saving = signal(false);
  private readonly service = inject(UserGroupService);
  submit(): void {
    if (this.form.invalid) {
      return;
    }
    if (this.saving()) return;
    this.saving.set(true);
    const request = this.form.getRawValue();
    const operation = this.group ? this.service.update(this.group.id, request) : this.service.create(request);
    operation.pipe(finalize(() => this.saving.set(false))).subscribe({next: () => this.dialogRef.close(true), error: () => {}});
  }

  cancel(): void {
    this.dialogRef.close();
  }
}
