import { Component, inject, signal } from '@angular/core';
import { FormBuilder, FormsModule, ReactiveFormsModule, Validators } from '@angular/forms';
import { DynamicDialogRef } from 'primeng/dynamicdialog';
import { InputText } from 'primeng/inputtext';
import { Listbox } from 'primeng/listbox';
import { Select } from 'primeng/select';
import { Button } from 'primeng/button';
import { TranslateModule } from '@ngx-translate/core';
import { debounceTime, distinctUntilChanged, switchMap } from 'rxjs';
import { UserDirectoryService } from '../../../core/services/user-directory.service';
import { UserSummary } from '../../../core/models/user.model';
import { ProjectMemberRole } from '../../../core/models/project.model';

@Component({
  selector: 'app-member-dialog',
  standalone: true,
  imports: [ReactiveFormsModule, FormsModule, InputText, Listbox, Select, Button, TranslateModule],
  templateUrl: './member-dialog.component.html'
})
export class MemberDialogComponent {
  private readonly fb = inject(FormBuilder);
  private readonly userDirectory = inject(UserDirectoryService);
  private readonly dialogRef = inject(DynamicDialogRef);

  readonly roles: ProjectMemberRole[] = ['Viewer', 'Editor', 'Owner'];
  readonly roleOptions = this.roles.map((role) => ({ label: role, value: role }));
  readonly results = signal<UserSummary[]>([]);
  readonly selectedUser = signal<UserSummary | null>(null);

  readonly searchControl = this.fb.nonNullable.control('');
  readonly roleControl = this.fb.nonNullable.control<ProjectMemberRole>('Viewer', Validators.required);

  constructor() {
    this.searchControl.valueChanges
      .pipe(
        debounceTime(250),
        distinctUntilChanged(),
        switchMap((query) => this.userDirectory.search(query))
      )
      .subscribe((users) => this.results.set(users));
  }

  submit(): void {
    const user = this.selectedUser();
    if (!user) {
      return;
    }
    this.dialogRef.close({ userId: user.id, role: this.roleControl.value });
  }

  cancel(): void {
    this.dialogRef.close();
  }
}
