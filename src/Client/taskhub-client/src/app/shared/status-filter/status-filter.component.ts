import { Component, EventEmitter, Input, Output } from '@angular/core';
import { TranslateModule } from '@ngx-translate/core';
import { TASK_STATUS_FILTERS, TaskStatusFilter } from '../../core/models/task-status-filter.model';

@Component({
  selector: 'app-status-filter',
  standalone: true,
  imports: [TranslateModule],
  templateUrl: './status-filter.component.html',
  styleUrl: './status-filter.component.scss'
})
export class StatusFilterComponent {
  @Input() value: TaskStatusFilter = 'All';
  @Output() readonly valueChange = new EventEmitter<TaskStatusFilter>();

  readonly options = TASK_STATUS_FILTERS;

  labelKey(option: TaskStatusFilter): string {
    return option === 'All' ? 'tasks.filterAll' : `tasks.status.${option}`;
  }

  select(option: TaskStatusFilter): void {
    if (option !== this.value) {
      this.valueChange.emit(option);
    }
  }
}
