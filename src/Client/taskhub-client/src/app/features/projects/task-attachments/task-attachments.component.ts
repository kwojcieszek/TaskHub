import { Component, Input, OnInit, inject, signal } from '@angular/core';
import { concat, finalize } from 'rxjs';
import { Button } from 'primeng/button';
import { ConfirmationService } from 'primeng/api';
import { ConfirmDialog } from 'primeng/confirmdialog';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { AttachmentService } from '../../../core/services/attachment.service';
import { Attachment } from '../../../core/models/attachment.model';
import { LocalizedDatePipe } from '../../../shared/pipes/localized-date.pipe';

const MAX_FILE_SIZE_BYTES = 25 * 1024 * 1024;

@Component({
  selector: 'app-task-attachments',
  standalone: true,
  imports: [LocalizedDatePipe, Button, ConfirmDialog, TranslateModule],
  providers: [ConfirmationService],
  templateUrl: './task-attachments.component.html',
  styleUrl: './task-attachments.component.scss'
})
export class TaskAttachmentsComponent implements OnInit {
  @Input({ required: true }) projectId!: string;
  @Input({ required: true }) taskId!: string;

  private readonly attachmentService = inject(AttachmentService);
  private readonly confirmationService = inject(ConfirmationService);
  private readonly translate = inject(TranslateService);

  readonly attachments = signal<Attachment[]>([]);
  readonly uploading = signal(false);
  readonly errorKey = signal<string | null>(null);

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.attachmentService.getByTask(this.projectId, this.taskId).subscribe({
      next: (attachments) => this.attachments.set(attachments),
      error: () => this.errorKey.set('attachments.loadError')
    });
  }

  onFilesSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const files = Array.from(input.files ?? []);
    input.value = '';

    if (files.length === 0) {
      return;
    }

    if (files.some((file) => file.size > MAX_FILE_SIZE_BYTES)) {
      this.errorKey.set('attachments.tooLarge');
      return;
    }

    this.errorKey.set(null);
    this.uploading.set(true);

    concat(...files.map((file) => this.attachmentService.upload(this.projectId, this.taskId, file)))
      .pipe(
        finalize(() => {
          this.uploading.set(false);
          this.load();
        })
      )
      .subscribe({ error: () => this.errorKey.set('attachments.uploadError') });
  }

  download(attachment: Attachment): void {
    this.errorKey.set(null);
    this.attachmentService.download(this.projectId, this.taskId, attachment.id).subscribe({
      next: (blob) => {
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = attachment.fileName;
        link.click();
        URL.revokeObjectURL(url);
      },
      error: () => this.errorKey.set('attachments.downloadError')
    });
  }

  remove(attachment: Attachment): void {
    this.confirmationService.confirm({
      key: 'taskAttachments',
      message: this.translate.instant('attachments.deleteConfirm'),
      header: this.translate.instant('common.deleteConfirmHeader'),
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: this.translate.instant('common.delete'),
      rejectLabel: this.translate.instant('common.cancel'),
      acceptButtonProps: { severity: 'danger' },
      rejectButtonProps: { severity: 'secondary', outlined: true },
      accept: () => {
        this.errorKey.set(null);
        this.attachmentService.delete(this.projectId, this.taskId, attachment.id).subscribe({
          next: () => this.load(),
          error: () => this.errorKey.set('attachments.deleteError')
        });
      }
    });
  }

  formatSize(bytes: number): string {
    if (bytes < 1024) {
      return `${bytes} B`;
    }
    if (bytes < 1024 * 1024) {
      return `${(bytes / 1024).toFixed(1)} KB`;
    }
    return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  }
}
