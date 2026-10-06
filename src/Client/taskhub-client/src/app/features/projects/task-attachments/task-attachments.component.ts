import { HttpErrorResponse } from '@angular/common/http';
import { Component, Input, OnInit, computed, inject, signal } from '@angular/core';
import { concat, finalize } from 'rxjs';
import { Button } from 'primeng/button';
import { ConfirmationService } from 'primeng/api';
import { ConfirmDialog } from 'primeng/confirmdialog';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { AttachmentService } from '../../../core/services/attachment.service';
import { Attachment } from '../../../core/models/attachment.model';
import { LocalizedDatePipe } from '../../../shared/pipes/localized-date.pipe';

// Used until the server reports its own limit, and when that request fails.
const DEFAULT_MAX_FILE_SIZE_BYTES = 25 * 1024 * 1024;

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
  // Names of the files refused for their size, shown with the error.
  readonly rejectedFiles = signal('');
  readonly maxFileSizeBytes = signal(DEFAULT_MAX_FILE_SIZE_BYTES);
  readonly maxFileSizeLabel = computed(() => this.formatSize(this.maxFileSizeBytes()));
  // The signed-in user's total across all tasks; a limit of 0 means there is none.
  readonly maxTotalBytes = signal(0);
  readonly usedBytes = signal(0);
  readonly maxTotalLabel = computed(() => this.formatSize(this.maxTotalBytes()));
  readonly usedLabel = computed(() => this.formatSize(this.usedBytes()));

  ngOnInit(): void {
    this.load();
    this.loadLimits();
  }

  loadLimits(): void {
    this.attachmentService.getLimits().subscribe({
      next: (limits) => {
        this.maxFileSizeBytes.set(limits.maxFileSizeBytes);
        this.maxTotalBytes.set(limits.maxTotalBytesPerUser ?? 0);
        this.usedBytes.set(limits.usedBytes ?? 0);
      },
      error: () => this.maxFileSizeBytes.set(DEFAULT_MAX_FILE_SIZE_BYTES)
    });
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

    // Each file is checked before anything is sent: too large ones are named and skipped, the rest are uploaded.
    const tooLarge = files.filter((file) => file.size > this.maxFileSizeBytes());
    const accepted = files.filter((file) => file.size <= this.maxFileSizeBytes());

    this.rejectedFiles.set(tooLarge.map((file) => `${file.name} (${this.formatSize(file.size)})`).join(', '));
    this.errorKey.set(tooLarge.length > 0 ? 'attachments.tooLarge' : null);

    if (accepted.length === 0) {
      return;
    }

    this.uploading.set(true);

    concat(...accepted.map((file) => this.attachmentService.upload(this.projectId, this.taskId, file)))
      .pipe(
        finalize(() => {
          this.uploading.set(false);
          this.load();
          this.loadLimits();
        })
      )
      .subscribe({
        error: (error: HttpErrorResponse) => {
          // The server applies the same limit; it answers 400 or 413 when a file slips past the check above.
          const message = error.error?.errors?.[0] ?? '';
          const refusedForSize = error.status === 413 || /too large/i.test(message);

          this.rejectedFiles.set('');

          if (/quota/i.test(message)) {
            this.errorKey.set('attachments.quotaExceeded');
          } else {
            this.errorKey.set(refusedForSize ? 'attachments.tooLargeServer' : 'attachments.uploadError');
          }
        }
      });
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
          next: () => {
            this.load();
            this.loadLimits();
          },
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
      return `${this.trimZero(bytes / 1024)} KB`;
    }
    return `${this.trimZero(bytes / (1024 * 1024))} MB`;
  }

  // One decimal place, without a trailing ".0": 25 MB rather than 25.0 MB.
  private trimZero(value: number): string {
    return value.toFixed(1).replace(/\.0$/, '');
  }
}
