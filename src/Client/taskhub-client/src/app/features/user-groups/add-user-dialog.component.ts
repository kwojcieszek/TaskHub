import { UserGroupService } from '../../core/services/user-group.service';
import { DialogSave } from '../../shared/dialog-save';
import { Component, inject, signal } from '@angular/core';
import { FormBuilder, FormsModule, ReactiveFormsModule } from '@angular/forms';
import { DynamicDialogConfig, DynamicDialogRef } from 'primeng/dynamicdialog';
import { InputText } from 'primeng/inputtext';
import { Listbox } from 'primeng/listbox';
import { Button } from 'primeng/button';
import { TranslateModule } from '@ngx-translate/core';
import { debounceTime, distinctUntilChanged, of, switchMap } from 'rxjs';
import { UserDirectoryService } from '../../core/services/user-directory.service';
import { UserSummary } from '../../core/models/user.model';

// The server answers a shorter search with nothing, so the request is not sent at all.
const MIN_SEARCH_LENGTH = 2;

@Component({
  selector: 'app-add-user-dialog',
  standalone: true,
  imports: [ReactiveFormsModule, FormsModule, InputText, Listbox, Button, TranslateModule],
  templateUrl: './add-user-dialog.component.html'
})
export class AddUserDialogComponent {
  private readonly fb = inject(FormBuilder);
  private readonly userDirectory = inject(UserDirectoryService);
  private readonly dialogRef = inject(DynamicDialogRef);
  readonly save = new DialogSave(this.dialogRef);
  private readonly config = inject(DynamicDialogConfig);
  private readonly persistence = inject(UserGroupService);

  readonly results = signal<UserSummary[]>([]);
  readonly selectedUser = signal<UserSummary | null>(null);
  readonly searchControl = this.fb.nonNullable.control('');

  constructor() {
    this.searchControl.valueChanges
      .pipe(
        debounceTime(250),
        distinctUntilChanged(),
        switchMap((query) => (query.trim().length < MIN_SEARCH_LENGTH ? of([]) : this.userDirectory.search(query.trim())))
      )
      .subscribe((users) => this.results.set(users));
  }

  submit(): void {
    const user = this.selectedUser();
    if (!user) {
      return;
    }
    this.save.submit(this.persistence.addMember(this.config.data.groupId, { userId: user.id }));
  }

  cancel(): void {
    this.dialogRef.close();
  }
}
