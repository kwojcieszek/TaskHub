import { Component, Input, signal } from '@angular/core';
import { TranslateModule } from '@ngx-translate/core';
import { Dialog } from 'primeng/dialog';
import { TaskItem } from '../../core/models/task.model';

/**
 * An icon that shows the task description in a read-only window,
 * so it can be read without opening the task for editing.
 */
@Component({
  selector: 'app-task-description-preview',
  standalone: true,
  imports: [Dialog, TranslateModule],
  templateUrl: './task-description-preview.component.html',
  styleUrl: './task-description-preview.component.scss'
})
export class TaskDescriptionPreviewComponent {
  @Input({ required: true }) task!: TaskItem;

  readonly visible = signal(false);

  show(event: Event): void {
    // The icon sits on clickable rows and cards, which must not open as well.
    event.stopPropagation();
    this.visible.set(true);
  }
}
