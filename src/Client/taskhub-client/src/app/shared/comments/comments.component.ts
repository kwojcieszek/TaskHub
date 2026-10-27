import { Component, EventEmitter, Input, OnChanges, Output, SimpleChanges, inject, signal } from '@angular/core';
import { FormControl, ReactiveFormsModule, Validators } from '@angular/forms';
import { ConfirmationService } from 'primeng/api';
import { Button } from 'primeng/button';
import { ConfirmDialog } from 'primeng/confirmdialog';
import { Textarea } from 'primeng/textarea';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { Comment } from '../../core/models/comment.model';
import { CommentService } from '../../core/services/comment.service';
import { entityColor } from '../../core/utils/project-color';
import { DictationDirective } from '../dictation/dictation.directive';
import { LocalizedDatePipe } from '../pipes/localized-date.pipe';

const MAX_LENGTH = 4000;

/**
 * The comments of a project, or of one task when a task is given. It may sit inside another form,
 * so it has no form element of its own and none of its buttons submit.
 */
@Component({
  selector: 'app-comments',
  standalone: true,
  imports: [LocalizedDatePipe, ReactiveFormsModule, Button, ConfirmDialog, Textarea, TranslateModule, DictationDirective],
  providers: [ConfirmationService],
  templateUrl: './comments.component.html',
  styleUrl: './comments.component.scss'
})
export class CommentsComponent implements OnChanges {
  @Input({ required: true }) projectId!: string;
  @Input() taskId: string | null = null;
  @Output() readonly countChange = new EventEmitter<number>();

  private readonly commentService = inject(CommentService);
  private readonly confirmationService = inject(ConfirmationService);
  private readonly translate = inject(TranslateService);

  readonly comments = signal<Comment[]>([]);
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly errorKey = signal<string | null>(null);
  readonly editingId = signal<string | null>(null);
  readonly maxLength = MAX_LENGTH;

  readonly newControl = new FormControl('', { nonNullable: true, validators: [Validators.maxLength(MAX_LENGTH)] });
  readonly editControl = new FormControl('', { nonNullable: true, validators: [Validators.maxLength(MAX_LENGTH)] });

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['projectId'] || changes['taskId']) {
      this.editingId.set(null);
      this.newControl.reset('');
      this.load();
    }
  }

  load(): void {
    this.loading.set(true);

    this.commentService.getAll(this.projectId, this.taskId).subscribe({
      next: (comments) => {
        this.comments.set(comments);
        this.countChange.emit(comments.length);
        this.loading.set(false);
      },
      error: () => {
        this.errorKey.set('comments.loadError');
        this.loading.set(false);
      }
    });
  }

  add(): void {
    const content = this.newControl.value.trim();

    if (!content || this.newControl.invalid || this.saving()) {
      return;
    }

    this.saving.set(true);
    this.errorKey.set(null);

    this.commentService.add(this.projectId, this.taskId, content).subscribe({
      next: () => {
        this.newControl.reset('');
        this.saving.set(false);
        this.load();
      },
      error: () => {
        this.errorKey.set('comments.saveError');
        this.saving.set(false);
      }
    });
  }

  startEdit(comment: Comment): void {
    this.editControl.setValue(comment.content);
    this.editingId.set(comment.id);
  }

  cancelEdit(): void {
    this.editingId.set(null);
  }

  saveEdit(comment: Comment): void {
    const content = this.editControl.value.trim();

    if (!content || this.editControl.invalid) {
      return;
    }

    this.errorKey.set(null);

    this.commentService.update(this.projectId, comment.id, content).subscribe({
      next: () => {
        this.editingId.set(null);
        this.load();
      },
      error: () => this.errorKey.set('comments.saveError')
    });
  }

  remove(comment: Comment): void {
    this.confirmationService.confirm({
      key: 'comments',
      message: this.translate.instant('comments.deleteConfirm'),
      header: this.translate.instant('common.deleteConfirmHeader'),
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: this.translate.instant('common.delete'),
      rejectLabel: this.translate.instant('common.cancel'),
      acceptButtonProps: { severity: 'danger' },
      rejectButtonProps: { severity: 'secondary', outlined: true },
      accept: () => {
        this.errorKey.set(null);

        this.commentService.delete(this.projectId, comment.id).subscribe({
          next: () => this.load(),
          error: () => this.errorKey.set('comments.deleteError')
        });
      }
    });
  }

  // Ctrl+Enter sends; a plain Enter starts a new line and must not submit a form the comments sit in.
  onNewKeydown(event: KeyboardEvent): void {
    if (event.key === 'Enter' && (event.ctrlKey || event.metaKey)) {
      event.preventDefault();
      this.add();
    }
  }

  initial(comment: Comment): string {
    return (comment.authorDisplayName ?? '?').charAt(0).toUpperCase();
  }

  color(comment: Comment): string {
    return entityColor(comment.authorId ?? comment.id);
  }
}
