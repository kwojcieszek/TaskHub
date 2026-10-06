import { Component, inject, signal } from '@angular/core';
import { FormBuilder, FormsModule, ReactiveFormsModule } from '@angular/forms';
import { DynamicDialogRef } from 'primeng/dynamicdialog';
import { InputText } from 'primeng/inputtext';
import { Listbox } from 'primeng/listbox';
import { Button } from 'primeng/button';
import { TranslateModule } from '@ngx-translate/core';
import { debounceTime, distinctUntilChanged, switchMap } from 'rxjs';
import { UserDirectoryService } from '../../core/services/user-directory.service';
import { UserSummary } from '../../core/models/user.model';

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

  readonly results = signal<UserSummary[]>([]);
  readonly selectedUser = signal<UserSummary | null>(null);
  readonly searchControl = this.fb.nonNullable.control('');

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
    this.dialogRef.close({ userId: user.id });
  }

  cancel(): void {
    this.dialogRef.close();
  }
}
