import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { catchError, forkJoin, of } from 'rxjs';
import { Button } from 'primeng/button';
import { ConfirmationService } from 'primeng/api';
import { ConfirmDialog } from 'primeng/confirmdialog';
import { DialogService } from 'primeng/dynamicdialog';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { ProjectService } from '../../../core/services/project.service';
import { ProjectGroupService } from '../../../core/services/project-group.service';
import { AuthService } from '../../../core/services/auth.service';
import { Project } from '../../../core/models/project.model';
import { ProjectGroup } from '../../../core/models/project-group.model';
import { projectColor } from '../../../core/utils/project-color';
import { ProjectFormDialogComponent } from '../project-form-dialog/project-form-dialog.component';
import { ProjectGroupDialogComponent } from '../project-group-dialog/project-group-dialog.component';
import { ProjectDetailComponent } from '../project-detail/project-detail.component';
import { ProjectAvatarComponent } from '../../../shared/project-avatar/project-avatar.component';

interface ProjectStat {
  labelKey: string;
  icon: string;
  tone: string;
  value: number;
}

interface ProjectSection {
  group: ProjectGroup | null;
  projects: Project[];
}

@Component({
  selector: 'app-project-list',
  standalone: true,
  imports: [Button, ConfirmDialog, TranslateModule, ProjectDetailComponent, ProjectAvatarComponent],
  providers: [DialogService, ConfirmationService],
  templateUrl: './project-list.component.html',
  styleUrl: './project-list.component.scss'
})
export class ProjectListComponent implements OnInit {
  private readonly projectService = inject(ProjectService);
  private readonly projectGroupService = inject(ProjectGroupService);
  private readonly dialogService = inject(DialogService);
  private readonly confirmationService = inject(ConfirmationService);
  private readonly translate = inject(TranslateService);
  private readonly auth = inject(AuthService);

  readonly projects = signal<Project[]>([]);
  readonly groups = signal<ProjectGroup[]>([]);
  readonly loading = signal(false);
  readonly loadError = signal(false);
  readonly selectedProjectId = signal<string | null>(null);

  readonly manageableGroups = computed(() => this.groups().filter((group) => this.canManageGroup(group)));

  readonly sections = computed<ProjectSection[]>(() => {
    const groups = this.groups();
    const projects = this.projects();
    const groupIds = new Set(groups.map((group) => group.id));

    const sections: ProjectSection[] = groups.map((group) => ({
      group,
      projects: projects.filter((project) => project.projectGroupId === group.id)
    }));

    const ungrouped = projects.filter((project) => !project.projectGroupId || !groupIds.has(project.projectGroupId));
    if (ungrouped.length > 0) {
      sections.push({ group: null, projects: ungrouped });
    }

    return sections;
  });

  readonly stats = computed<ProjectStat[]>(() => {
    const projects = this.projects();

    return [
      { labelKey: 'projects.statProjects', icon: 'pi pi-briefcase', tone: 'blue', value: projects.length },
      { labelKey: 'projects.statGroups', icon: 'pi pi-folder', tone: 'amber', value: this.groups().length },
      { labelKey: 'projects.statTasks', icon: 'pi pi-check-square', tone: 'violet', value: projects.reduce((sum, p) => sum + p.taskCount, 0) }
    ];
  });

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.loadError.set(false);
    forkJoin({
      projects: this.projectService.getAll(),
      groups: this.projectGroupService.getAll().pipe(catchError(() => of<ProjectGroup[]>([])))
    }).subscribe({
      next: ({ projects, groups }) => {
        this.projects.set(projects);
        this.groups.set(groups);
        this.loading.set(false);

        if (!projects.some((p) => p.id === this.selectedProjectId())) {
          // The first project as listed on screen (groups first), not the first one the API returned.
          const firstListed = this.sections().flatMap((section) => section.projects)[0];
          this.selectedProjectId.set(firstListed?.id ?? null);
        }
      },
      error: () => {
        this.loading.set(false);
        this.loadError.set(true);
      }
    });
  }

  colorOf(project: Project): string {
    return projectColor(project.id);
  }

  canManageGroup(group: ProjectGroup): boolean {
    return this.auth.isAdmin() || group.ownerId === this.auth.currentUser()?.id;
  }

  openCreateDialog(): void {
    const ref = this.dialogService.open(ProjectFormDialogComponent, {
      header: this.translate.instant('projects.newProject'),
      width: '480px',
      data: { groups: this.manageableGroups() }
    });
    ref?.onClose.subscribe((result) => {
      if (!result) {
        return;
      }
      this.projectService.create(result).subscribe((project) => {
        this.selectedProjectId.set(project.id);
        this.load();
      });
    });
  }

  openCreateGroupDialog(): void {
    const ref = this.dialogService.open(ProjectGroupDialogComponent, {
      header: this.translate.instant('projectGroups.newGroup'),
      width: '480px',
      data: {}
    });
    ref?.onClose.subscribe((result) => {
      if (!result) {
        return;
      }
      this.projectGroupService.create(result).subscribe(() => this.load());
    });
  }

  editGroup(group: ProjectGroup): void {
    const ref = this.dialogService.open(ProjectGroupDialogComponent, {
      header: this.translate.instant('common.edit'),
      width: '480px',
      data: { group }
    });
    ref?.onClose.subscribe((result) => {
      if (!result) {
        return;
      }
      this.projectGroupService.update(group.id, result).subscribe(() => this.load());
    });
  }

  deleteGroup(group: ProjectGroup): void {
    this.confirmDelete('projectGroups.deleteConfirm', () => {
      this.projectGroupService.delete(group.id).subscribe(() => this.load());
    });
  }

  select(project: Project): void {
    this.selectedProjectId.set(project.id);
  }

  onDetailChanged(): void {
    this.load();
  }

  onDetailDeleted(): void {
    this.selectedProjectId.set(null);
    this.load();
  }

  private confirmDelete(messageKey: string, accept: () => void): void {
    this.confirmationService.confirm({
      key: 'projectList',
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
