import { Component, EventEmitter, Input, OnChanges, Output, SimpleChanges, computed, inject, signal } from '@angular/core';
import { of, switchMap } from 'rxjs';
import { Button } from 'primeng/button';
import { Menu } from 'primeng/menu';
import { MenuItem, ConfirmationService } from 'primeng/api';
import { ConfirmDialog } from 'primeng/confirmdialog';
import { DialogService } from 'primeng/dynamicdialog';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { ProjectService } from '../../../core/services/project.service';
import { TaskItemService } from '../../../core/services/task-item.service';
import { AuthService } from '../../../core/services/auth.service';
import { Project, ProjectMember, ProjectTeam } from '../../../core/models/project.model';
import { ProjectGroup } from '../../../core/models/project-group.model';
import { TaskItem, TaskItemStatus } from '../../../core/models/task.model';
import { TaskStatusFilter } from '../../../core/models/task-status-filter.model';
import { entityColor, projectColor } from '../../../core/utils/project-color';
import { StatusFilterComponent } from '../../../shared/status-filter/status-filter.component';
import { TaskRowComponent } from '../../../shared/task-row/task-row.component';
import { ProjectAvatarComponent } from '../../../shared/project-avatar/project-avatar.component';
import { ProjectFormDialogComponent } from '../project-form-dialog/project-form-dialog.component';
import { TaskDialogComponent } from '../task-dialog/task-dialog.component';
import { MemberDialogComponent } from '../member-dialog/member-dialog.component';
import { TeamDialogComponent } from '../team-dialog/team-dialog.component';
import { CommentsComponent } from '../../../shared/comments/comments.component';

@Component({
  selector: 'app-project-detail',
  standalone: true,
  imports: [Button, Menu, ConfirmDialog, TranslateModule, TaskRowComponent, ProjectAvatarComponent, StatusFilterComponent, CommentsComponent],
  providers: [DialogService, ConfirmationService],
  templateUrl: './project-detail.component.html',
  styleUrl: './project-detail.component.scss'
})
export class ProjectDetailComponent implements OnChanges {
  @Input({ required: true }) projectId!: string;
  @Input() groups: ProjectGroup[] = [];
  @Output() readonly deleted = new EventEmitter<void>();
  @Output() readonly changed = new EventEmitter<void>();

  private readonly projectService = inject(ProjectService);
  private readonly taskItemService = inject(TaskItemService);
  private readonly dialogService = inject(DialogService);
  private readonly confirmationService = inject(ConfirmationService);
  private readonly translate = inject(TranslateService);
  readonly auth = inject(AuthService);

  readonly project = signal<Project | null>(null);
  readonly tasks = signal<TaskItem[]>([]);
  readonly tasksLoadError = signal(false);
  readonly statusFilter = signal<TaskStatusFilter>('ToDo');
  readonly view = signal<'tasks' | 'members' | 'comments'>('tasks');
  readonly commentCount = signal<number | null>(null);

  // A task stays visible when it matches the filter or when one of its subtasks does, so matches keep their context.
  readonly visibleTasks = computed(() => {
    const filter = this.statusFilter();

    if (filter === 'All') {
      return this.tasks().map((task) => ({ task, subTasks: task.subTasks }));
    }

    return this.tasks()
      .map((task) => ({ task, subTasks: task.subTasks.filter((subTask) => subTask.status === filter) }))
      .filter((entry) => entry.task.status === filter || entry.subTasks.length > 0);
  });
  readonly members = signal<ProjectMember[]>([]);
  readonly teams = signal<ProjectTeam[]>([]);

  readonly projectMenuItems: MenuItem[] = [
    { label: this.translate.instant('common.edit'), icon: 'pi pi-pencil', command: () => this.editProject() },
    { label: this.translate.instant('common.delete'), icon: 'pi pi-trash', command: () => this.deleteProject() }
  ];

  readonly color = computed(() => projectColor(this.project()?.id ?? ''));

  readonly gitLink = computed(() => {
    const url = this.project()?.gitUrl;
    if (!url) {
      return null;
    }

    const label = url.replace(/^https?:\/\//i, '').replace(/\.git$/i, '').replace(/\/$/, '');
    const icon = /(^|\.)github\./i.test(label) ? 'pi pi-github' : 'pi pi-code';

    return { url, label, icon };
  });

  readonly progress = computed(() => {
    const all = this.tasks().flatMap((task) => [task, ...task.subTasks]);
    if (all.length === 0) {
      return null;
    }

    const statuses: TaskItemStatus[] = ['Done', 'InProgress', 'ToDo', 'Cancelled'];
    const parts = statuses
      .map((status) => ({ status, count: all.filter((task) => task.status === status).length }))
      .filter((part) => part.count > 0);
    const done = all.filter((task) => task.status === 'Done').length;

    return { parts, percentDone: Math.round((done / all.length) * 100) };
  });

  readonly canManage = computed(() => {
    const project = this.project();
    if (!project) {
      return false;
    }
    return this.auth.isAdmin() || project.ownerId === this.auth.currentUser()?.id;
  });

  readonly canEdit = computed(() => {
    if (this.auth.isAdmin() || this.canManage()) {
      return true;
    }
    const currentUserId = this.auth.currentUser()?.id;
    const membership = this.members().find((m) => m.userId === currentUserId);
    return membership?.role === 'Editor';
  });

  toggleMembers(): void {
    this.view.update((view) => (view === 'members' ? 'tasks' : 'members'));
  }

  toggleComments(): void {
    this.view.update((view) => (view === 'comments' ? 'tasks' : 'comments'));
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['projectId']) {
      this.commentCount.set(null);
      this.loadAll();
    }
  }

  loadAll(): void {
    this.projectService.getById(this.projectId).subscribe((project) => this.project.set(project));
    this.tasksLoadError.set(false);
    this.taskItemService.getByProject(this.projectId).subscribe({
      next: (tasks) => this.tasks.set(tasks),
      error: () => this.tasksLoadError.set(true)
    });
    this.projectService.getMembers(this.projectId).subscribe((members) => this.members.set(members));
    this.projectService.getTeams(this.projectId).subscribe((teams) => this.teams.set(teams));
  }

  editProject(): void {
    const project = this.project();
    if (!project) {
      return;
    }
    const ref = this.dialogService.open(ProjectFormDialogComponent, {
      header: this.translate.instant('common.edit'),
      width: '480px',
      data: { project, groups: this.groups }
    });
    ref?.onClose.subscribe((result) => {
      if (!result) {
        return;
      }
      const groupChanged = result.projectGroupId !== project.projectGroupId;
      this.projectService
        .update(this.projectId, {
          name: result.name,
          description: result.description,
          icon: result.icon,
          gitUrl: result.gitUrl,
          isActive: result.isActive
        })
        .pipe(switchMap((updated) => (groupChanged ? this.projectService.setGroup(this.projectId, result.projectGroupId) : of(updated))))
        .subscribe(() => {
          this.loadAll();
          this.changed.emit();
        });
    });
  }

  deleteProject(): void {
    this.confirmDelete('projects.deleteConfirm', () => {
      this.projectService.delete(this.projectId).subscribe(() => this.deleted.emit());
    });
  }

  addTask(parentTask: TaskItem | null): void {
    const ref = this.dialogService.open(TaskDialogComponent, {
      header: this.translate.instant(parentTask ? 'tasks.newSubTask' : 'tasks.newTask'),
      width: 'min(620px, 96vw)',
      data: { members: this.members() }
    });
    ref?.onClose.subscribe((result) => {
      if (!result) {
        return;
      }
      this.taskItemService.create(this.projectId, { ...result, parentTaskId: parentTask?.id ?? null }).subscribe(() => {
        this.loadAll();
        this.changed.emit();
      });
    });
  }

  editTask(task: TaskItem): void {
    const ref = this.dialogService.open(TaskDialogComponent, {
      header: this.translate.instant('common.edit'),
      width: 'min(1040px, 96vw)',
      data: { task, members: this.members(), statusDerived: task.subTasks.length > 0 }
    });
    ref?.onClose.subscribe((result) => {
      if (!result) {
        // Attachments are saved straight from the dialog, so the row may have changed even on cancel.
        this.loadAll();
        return;
      }
      this.taskItemService.update(this.projectId, task.id, result).subscribe(() => this.loadAll());
    });
  }

  startTask(task: TaskItem): void {
    this.taskItemService.start(this.projectId, task.id).subscribe(() => {
      this.loadAll();
      this.changed.emit();
    });
  }

  completeTask(task: TaskItem): void {
    this.confirmationService.confirm({
      message: this.translate.instant('tasks.completeConfirm'),
      header: this.translate.instant('tasks.completeConfirmHeader'),
      icon: 'pi pi-check-circle',
      acceptLabel: this.translate.instant('common.yes'),
      rejectLabel: this.translate.instant('common.no'),
      accept: () => {
        this.taskItemService.complete(this.projectId, task.id).subscribe(() => {
          this.loadAll();
          this.changed.emit();
        });
      }
    });
  }

  reopenTask(task: TaskItem): void {
    this.confirmationService.confirm({
      message: this.translate.instant('tasks.reopenConfirm'),
      header: this.translate.instant('tasks.reopenConfirmHeader'),
      icon: 'pi pi-undo',
      acceptLabel: this.translate.instant('common.yes'),
      rejectLabel: this.translate.instant('common.no'),
      accept: () => {
        this.taskItemService.reopen(task).subscribe(() => {
          this.loadAll();
          this.changed.emit();
        });
      }
    });
  }

  deleteTask(task: TaskItem): void {
    const messageKey = task.subTasks.length > 0 ? 'tasks.deleteWithSubTasksConfirm' : 'tasks.deleteConfirm';
    this.confirmDelete(messageKey, () => {
      this.taskItemService.delete(this.projectId, task.id).subscribe(() => {
        this.loadAll();
        this.changed.emit();
      });
    });
  }

  memberColor(member: ProjectMember): string {
    return entityColor(member.userId);
  }

  addMember(): void {
    const ref = this.dialogService.open(MemberDialogComponent, {
      header: this.translate.instant('projects.addMember'),
      width: '420px'
    });
    ref?.onClose.subscribe((result) => {
      if (!result) {
        return;
      }
      this.projectService.addMember(this.projectId, result).subscribe(() => this.loadAll());
    });
  }

  removeMember(member: ProjectMember): void {
    this.projectService.removeMember(this.projectId, member.userId).subscribe(() => this.loadAll());
  }

  addTeam(): void {
    const ref = this.dialogService.open(TeamDialogComponent, {
      header: this.translate.instant('projects.teams.add'),
      width: '420px',
      data: { projectId: this.projectId }
    });
    ref?.onClose.subscribe((result) => {
      if (!result) {
        return;
      }
      this.projectService.addTeam(this.projectId, result).subscribe(() => this.loadAll());
    });
  }

  removeTeam(team: ProjectTeam): void {
    this.projectService.removeTeam(this.projectId, team.userGroupId).subscribe(() => this.loadAll());
  }

  private confirmDelete(messageKey: string, accept: () => void): void {
    this.confirmationService.confirm({
      message: this.translate.instant(messageKey),
      header: this.translate.instant('common.deleteConfirmHeader'),
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: this.translate.instant('common.delete'),
      rejectLabel: this.translate.instant('common.cancel'),
      acceptButtonProps: { severity: 'danger' },
      rejectButtonProps: { severity: 'secondary', outlined: true },
      accept
    });
  }
}
