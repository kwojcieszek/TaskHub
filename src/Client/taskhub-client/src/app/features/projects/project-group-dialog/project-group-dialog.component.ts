import { Component, inject } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { DynamicDialogConfig, DynamicDialogRef } from 'primeng/dynamicdialog';
import { InputText } from 'primeng/inputtext';
import { Textarea } from 'primeng/textarea';
import { Button } from 'primeng/button';
import { TranslateModule } from '@ngx-translate/core';
import { ProjectGroup } from '../../../core/models/project-group.model';

export interface ProjectGroupDialogData {
  group?: ProjectGroup;
}

@Component({
  selector: 'app-project-group-dialog',
  standalone: true,
  imports: [ReactiveFormsModule, InputText, Textarea, Button, TranslateModule],
  templateUrl: './project-group-dialog.component.html'
})
export class ProjectGroupDialogComponent {
  private readonly fb = inject(FormBuilder);
  private readonly dialogRef = inject(DynamicDialogRef);
  private readonly config = inject(DynamicDialogConfig<ProjectGroupDialogData>);

  readonly data: ProjectGroupDialogData = this.config.data ?? {};

  readonly form = this.fb.nonNullable.group({
    name: [this.data.group?.name ?? '', Validators.required],
    description: [this.data.group?.description ?? '']
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
