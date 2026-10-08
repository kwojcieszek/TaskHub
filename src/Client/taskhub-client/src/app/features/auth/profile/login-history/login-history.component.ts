import { Component, inject, signal } from '@angular/core';
import { Card } from 'primeng/card';
import { TableLazyLoadEvent, TableModule } from 'primeng/table';
import { TranslateModule } from '@ngx-translate/core';
import { AuditLogService } from '../../../../core/services/audit-log.service';
import { UserActivityLog } from '../../../../core/models/audit-log.model';
import { LocalizedDatePipe } from '../../../../shared/pipes/localized-date.pipe';

const PAGE_SIZE = 10;

@Component({
  selector: 'app-login-history',
  standalone: true,
  imports: [LocalizedDatePipe, Card, TableModule, TranslateModule],
  templateUrl: './login-history.component.html',
  styleUrl: './login-history.component.scss'
})
export class LoginHistoryComponent {
  private readonly auditLogService = inject(AuditLogService);

  readonly logs = signal<UserActivityLog[]>([]);
  readonly totalCount = signal(0);
  readonly loading = signal(false);
  readonly failed = signal(false);
  readonly pageSize = PAGE_SIZE;

  onLazyLoad(event: TableLazyLoadEvent): void {
    this.loading.set(true);
    this.failed.set(false);

    const page = Math.floor((event.first ?? 0) / PAGE_SIZE) + 1;

    this.auditLogService.getMyLogins({ page, pageSize: PAGE_SIZE }).subscribe({
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
}
