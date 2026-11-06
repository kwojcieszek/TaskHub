import { Observable, catchError, finalize, of } from 'rxjs';
import { TaskItemService } from '../../../core/services/task-item.service';
import { AuthService } from '../../../core/services/auth.service';
import { ProjectService } from '../../../core/services/project.service';
import { UserPreferencesService } from '../../../core/services/user-preferences.service';
import { TaskHistoryComponent } from '../task-history/task-history.component';
import { Component, computed, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { DynamicDialogConfig, DynamicDialogRef } from 'primeng/dynamicdialog';
import { InputText } from 'primeng/inputtext';
import { Textarea } from 'primeng/textarea';
import { Select } from 'primeng/select';
import { DatePicker } from 'primeng/datepicker';
import { Button } from 'primeng/button';
import { InputNumber } from 'primeng/inputnumber';
import { ToggleSwitch } from 'primeng/toggleswitch';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { RecurrenceBasis, RecurrenceUnit, TaskItem, TaskItemPriority, TaskItemStatus, TaskRecurrence } from '../../../core/models/task.model';
import { Project, ProjectMember } from '../../../core/models/project.model';
import { TaskAttachmentsComponent } from '../task-attachments/task-attachments.component';
import { CommentsComponent } from '../../../shared/comments/comments.component';
import { DictationDirective } from '../../../shared/dictation/dictation.directive';

export interface TaskDialogData {
  task?: TaskItem;
  projectId?: string;
  // Given when the task is created outside a project: the project is then picked in the dialog.
  projects?: Project[];
  parentTaskId?: string | null;
  // True when the task has subtasks: its status then follows them and cannot be edited.
  statusDerived?: boolean;
  members: ProjectMember[];
}

@Component({
  selector: 'app-task-dialog',
  standalone: true,
  imports: [ReactiveFormsModule, DictationDirective, InputText, Textarea, Select, DatePicker, Button, InputNumber, ToggleSwitch, TranslateModule, TaskAttachmentsComponent, CommentsComponent, TaskHistoryComponent],
  templateUrl: './task-dialog.component.html',
  styleUrl: './task-dialog.component.scss'
})
export class TaskDialogComponent {
  private readonly fb = inject(FormBuilder);
  private readonly dialogRef = inject(DynamicDialogRef);
  private readonly config = inject(DynamicDialogConfig<TaskDialogData>);
  private readonly translate = inject(TranslateService);
  private readonly auth = inject(AuthService);
  private readonly preferences = inject(UserPreferencesService);
  private readonly projectService = inject(ProjectService);
  private readonly service = inject(TaskItemService);

  readonly data: TaskDialogData = this.config.data ?? { members: [] };

  // The project is chosen in the dialog only for a new top-level task created outside a project.
  readonly selectsProject = !this.data.task && !this.data.parentTaskId && !!this.data.projects;

  readonly projectOptions = (this.data.projects ?? []).map((project) => ({ label: project.name, value: project.id }));

  readonly members = signal<ProjectMember[]>(this.data.members);
  readonly loadingMembers = signal(false);
  readonly saving = signal(false);

  // A viewer of the chosen project cannot add tasks to it.
  readonly canCreateInProject = computed(() => {
    if (!this.selectsProject || this.auth.isAdmin()) {
      return true;
    }

    const userId = this.auth.currentUser()?.id;
    const membership = this.members().find((member) => member.userId === userId);

    return !membership || membership.role !== 'Viewer';
  });

  readonly statuses: TaskItemStatus[] = ['ToDo', 'InProgress', 'Done', 'Cancelled'];
  readonly priorities: TaskItemPriority[] = ['Low', 'Medium', 'High', 'Critical'];

  readonly statusOptions = this.statuses.map((status) => ({
    label: this.translate.instant('tasks.status.' + status),
    value: status
  }));

  readonly priorityOptions = this.priorities.map((priority) => ({
    label: this.translate.instant('tasks.priority.' + priority),
    value: priority
  }));

  readonly recurrenceUnitOptions = (['Day', 'Week', 'Month', 'Year'] as RecurrenceUnit[]).map((unit) => ({
    label: this.translate.instant('tasks.recurrence.unit.' + unit),
    value: unit
  }));

  readonly recurrenceBasisOptions = (['CompletionDate', 'DueDate'] as RecurrenceBasis[]).map((basis) => ({
    label: this.translate.instant('tasks.recurrence.basis.' + basis),
    value: basis
  }));

  // Only a top-level task repeats; its subtasks are copied along with it.
  readonly canRepeat = !(this.data.task?.parentTaskId ?? this.data.parentTaskId);

  readonly assigneeOptions = computed(() => [
    { label: this.translate.instant('tasks.unassigned'), value: null as string | null },
    ...this.members().map((member) => ({ label: member.displayName, value: member.userId as string | null }))
  ]);

  readonly form = this.fb.nonNullable.group({
    projectId: [this.initialProjectId(), this.selectsProject ? Validators.required : []],
    title: [this.data.task?.title ?? '', Validators.required],
    description: [this.data.task?.description ?? ''],
    dueDate: [this.data.task?.dueDate ? new Date(this.data.task.dueDate) : null],
    status: [this.data.task?.status ?? 'ToDo'],
    priority: [this.data.task?.priority ?? 'Medium'],
    assignedUserId: [this.data.task ? this.data.task.assignedUserId ?? null : this.defaultAssigneeId()],
    repeats: [this.data.task?.recurrence != null],
    recurrenceInterval: [this.data.task?.recurrence?.interval ?? 1, [Validators.min(1), Validators.max(365)]],
    recurrenceUnit: [this.data.task?.recurrence?.unit ?? ('Week' as RecurrenceUnit)],
    recurrenceBasis: [this.data.task?.recurrence?.basis ?? ('CompletionDate' as RecurrenceBasis)]
  });

  constructor() {
    if (this.selectsProject && this.form.controls.projectId.value) {
      this.loadMembers(this.form.controls.projectId.value);
    }
  }

  onProjectChange(projectId: string | null): void {
    this.members.set([]);
    this.form.controls.assignedUserId.setValue(null);

    if (projectId) {
      this.loadMembers(projectId);
    }
  }

  submit(): void {
    if (this.form.invalid || !this.canCreateInProject()) {
      return;
    }

    const { projectId: selectedProjectId, repeats, recurrenceInterval, recurrenceUnit, recurrenceBasis, ...raw } = this.form.getRawValue();
    const recurrence: TaskRecurrence | null = this.canRepeat && repeats
      ? { interval: recurrenceInterval, unit: recurrenceUnit, basis: recurrenceBasis }
      : null;

    const request = {
      ...raw,
      parentTaskId: this.data.task?.parentTaskId ?? this.data.parentTaskId ?? null,
      dueDate: raw.dueDate ? this.toDateOnly(new Date(raw.dueDate)) : null,
      recurrence
    };

    const projectId = this.data.task?.projectId ?? (this.selectsProject ? selectedProjectId : this.data.projectId);

    if (!projectId || this.saving()) {
      return;
    }

    this.saving.set(true);

    const operation: Observable<TaskItem> = this.data.task
      ? this.service.update(projectId, this.data.task.id, request)
      : this.service.create(projectId, request);

    operation.pipe(finalize(() => this.saving.set(false))).subscribe({ next: () => this.dialogRef.close(true), error: () => {} });
  }

  cancel(): void {
    this.dialogRef.close();
  }

  // With a single project to choose from it is picked straight away.
  private initialProjectId(): string | null {
    if (!this.selectsProject) {
      return null;
    }

    const projects = this.data.projects ?? [];

    return projects.length === 1 ? projects[0].id : null;
  }

  private loadMembers(projectId: string): void {
    this.loadingMembers.set(true);

    // The task can still be created when the list of people to assign it to cannot be loaded.
    this.projectService
      .getMembers(projectId)
      .pipe(
        catchError(() => of([] as ProjectMember[])),
        finalize(() => this.loadingMembers.set(false))
      )
      .subscribe((members) => {
        if (this.form.controls.projectId.value !== projectId) {
          return;
        }

        this.members.set(members);
        this.form.controls.assignedUserId.setValue(this.defaultAssigneeId());
      });
  }

  // A new task goes to its creator when they enabled it in the settings and belong to the project.
  private defaultAssigneeId(): string | null {
    const userId = this.auth.currentUser()?.id;

    if (!userId || !this.preferences.assignToMeByDefault()) {
      return null;
    }

    const isMember = this.members().some((member) => member.userId === userId);

    return isMember ? userId : null;
  }

  // The due date is a calendar day, so the picked day is sent as it is; converting local midnight to UTC
  // would move it to the previous day in time zones ahead of UTC.
  private toDateOnly(date: Date): string {
    const month = String(date.getMonth() + 1).padStart(2, '0');
    const day = String(date.getDate()).padStart(2, '0');

    return `${date.getFullYear()}-${month}-${day}T00:00:00Z`;
  }
}
