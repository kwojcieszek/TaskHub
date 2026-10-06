import { Component, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Button } from 'primeng/button';
import { InputText } from 'primeng/inputtext';
import { TableLazyLoadEvent, TableModule } from 'primeng/table';
import { TranslateModule } from '@ngx-translate/core';
import { AuditLogService } from '../../../core/services/audit-log.service';
import { UserActivityLog } from '../../../core/models/audit-log.model';
import { LocalizedDatePipe } from '../../../shared/pipes/localized-date.pipe';

const DEFAULT_PAGE_SIZE = 25;
const FIRST_ERROR_STATUS = 400;

@Component({
  selector: 'app-activity-logs',
  standalone: true,
  imports: [LocalizedDatePipe, FormsModule, TableModule, Button, InputText, TranslateModule],
  templateUrl: './activity-logs.component.html',
  styleUrl: './activity-logs.component.scss'
})
export class ActivityLogsComponent {
  private readonly auditLogService = inject(AuditLogService);

  readonly logs = signal<UserActivityLog[]>([]);
  readonly totalCount = signal(0);
  readonly loading = signal(false);
  readonly failed = signal(false);
  readonly first = signal(0);
  readonly pageSizeOptions = [25, 50, 100];

  pageSize = DEFAULT_PAGE_SIZE;
  search = '';

  onLazyLoad(event: TableLazyLoadEvent): void {
    this.first.set(event.first ?? 0);
    this.pageSize = event.rows ?? DEFAULT_PAGE_SIZE;
    this.load();
  }

  applySearch(): void {
    this.first.set(0);
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.failed.set(false);

    const page = Math.floor(this.first() / this.pageSize) + 1;

    this.auditLogService.getActivity({ page, pageSize: this.pageSize, search: this.search.trim() }).subscribe({
      next: (result) => {
        this.logs.set(result.items);
        this.totalCount.set(result.totalCount);
        this.loading.set(false);
      },
      error: () => {
        this.failed.set(true);
        this.loading.set(false);
      }
    });
  }

  isError(log: UserActivityLog): boolean {
    return log.statusCode >= FIRST_ERROR_STATUS;
  }
}
