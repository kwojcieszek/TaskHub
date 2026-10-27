import { signal } from '@angular/core';
import { Observable, finalize } from 'rxjs';
import { DynamicDialogRef } from 'primeng/dynamicdialog';

/** Close a form only after its request succeeds, keeping entered values on failure. */
export class DialogSave {
  readonly pending = signal(false);
  constructor(private readonly ref: DynamicDialogRef) {}
  submit(operation: Observable<unknown>): void {
    if (this.pending()) return;
    this.pending.set(true);
    operation.pipe(finalize(() => this.pending.set(false))).subscribe({next: result => this.ref.close(result ?? true), error: () => {}});
  }
}
