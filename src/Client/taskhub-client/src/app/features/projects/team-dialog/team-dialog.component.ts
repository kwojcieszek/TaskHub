import { DialogSave } from '../../../shared/dialog-save';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Button } from 'primeng/button';
import { DynamicDialogConfig, DynamicDialogRef } from 'primeng/dynamicdialog';
import { Select } from 'primeng/select';
import { TranslateModule } from '@ngx-translate/core';
import { ProjectTeam, ProjectTeamRole } from '../../../core/models/project.model';
import { ProjectService } from '../../../core/services/project.service';

@Component({
  selector: 'app-team-dialog',
  standalone: true,
  imports: [ReactiveFormsModule, Select, Button, TranslateModule],
  templateUrl: './team-dialog.component.html'
})
export class TeamDialogComponent {
  private readonly fb = inject(FormBuilder);
  private readonly projectService = inject(ProjectService);
  private readonly dialogRef = inject(DynamicDialogRef);
  readonly save = new DialogSave(this.dialogRef);
  private readonly config = inject(DynamicDialogConfig);

  readonly roles: ProjectTeamRole[] = ['Viewer', 'Editor'];
  readonly teams = signal<ProjectTeam[]>([]);
  readonly loading = signal(true);
  readonly loadError = signal(false);

  readonly form = this.fb.group({
    userGroupId: this.fb.control<string | null>(null, Validators.required),
    role: this.fb.nonNullable.control<ProjectTeamRole>('Viewer', Validators.required)
  });

  constructor() {
    this.projectService.getAvailableTeams(this.config.data.projectId).subscribe({
      next: (teams) => {
        this.teams.set(teams);
        this.loading.set(false);
      },
      error: () => {
        this.loadError.set(true);
        this.loading.set(false);
      }
    });
  }

  submit(): void {
    const { userGroupId, role } = this.form.getRawValue();

    if (!userGroupId) {
      return;
    }

    this.save.submit(this.projectService.addTeam(this.config.data.projectId, { userGroupId, role }));
  }

  cancel(): void {
    this.dialogRef.close();
  }
}
