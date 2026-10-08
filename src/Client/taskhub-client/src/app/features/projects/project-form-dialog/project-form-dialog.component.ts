import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { DynamicDialogConfig, DynamicDialogRef } from 'primeng/dynamicdialog';
import { InputText } from 'primeng/inputtext';
import { Textarea } from 'primeng/textarea';
import { Select } from 'primeng/select';
import { ToggleSwitch } from 'primeng/toggleswitch';
import { Button } from 'primeng/button';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { Project } from '../../../core/models/project.model';
import { ProjectGroup } from '../../../core/models/project-group.model';
import { PROJECT_ICONS } from '../../../core/utils/project-icons';
import { imageFileToIcon, isImageIcon } from '../../../core/utils/image-icon';
import { DictationDirective } from '../../../shared/dictation/dictation.directive';

export interface ProjectFormDialogData {
  project?: Project;
  groups?: ProjectGroup[];
}

@Component({
  selector: 'app-project-form-dialog',
  standalone: true,
  imports: [ReactiveFormsModule, DictationDirective, InputText, Textarea, Select, ToggleSwitch, Button, TranslateModule],
  templateUrl: './project-form-dialog.component.html',
  styleUrl: './project-form-dialog.component.scss'
})
export class ProjectFormDialogComponent {
  private readonly fb = inject(FormBuilder);
  private readonly dialogRef = inject(DynamicDialogRef);
  private readonly config = inject(DynamicDialogConfig<ProjectFormDialogData>);
  private readonly translate = inject(TranslateService);

  readonly data: ProjectFormDialogData = this.config.data ?? {};

  readonly groupOptions = this.buildGroupOptions();
  readonly icons = PROJECT_ICONS;
  readonly iconError = signal(false);

  readonly form = this.fb.nonNullable.group({
    name: [this.data.project?.name ?? '', Validators.required],
    description: [this.data.project?.description ?? ''],
    icon: [this.data.project?.icon ?? null],
    gitUrl: [this.data.project?.gitUrl ?? '', [Validators.pattern(/^https?:\/\/\S+$/i), Validators.maxLength(500)]],
    projectGroupId: [this.data.project?.projectGroupId ?? null],
    isActive: [this.data.project?.isActive ?? true]
  });

  submit(): void {
    if (this.form.invalid) {
      return;
    }
    const value = this.form.getRawValue();
    this.dialogRef.close({ ...value, gitUrl: value.gitUrl.trim() || null });
  }

  get customIcon(): string | null {
    const icon = this.form.controls.icon.value;

    return isImageIcon(icon) ? icon : null;
  }

  selectIcon(icon: string | null): void {
    this.form.controls.icon.setValue(icon);
    this.iconError.set(false);
  }

  onIconFile(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';

    if (!file) {
      return;
    }

    imageFileToIcon(file)
      .then((icon) => this.selectIcon(icon))
      .catch(() => this.iconError.set(true));
  }

  cancel(): void {
    this.dialogRef.close();
  }

  private buildGroupOptions(): { label: string; value: string | null }[] {
    const options: { label: string; value: string | null }[] = (this.data.groups ?? []).map((group) => ({
      label: group.name,
      value: group.id
    }));

    // The project may sit in a group the user cannot manage; keep it selectable so the form shows it.
    const project = this.data.project;
    if (project?.projectGroupId && !options.some((option) => option.value === project.projectGroupId)) {
      options.push({ label: project.projectGroupName ?? '', value: project.projectGroupId });
    }

    if (options.length === 0) {
      return [];
    }

    return [{ label: this.translate.instant('projectGroups.noGroup'), value: null }, ...options];
  }
}
