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

const SIDEBAR_WIDTH_STORAGE_KEY = 'taskhub.projectsSidebarWidth';
const DEFAULT_SIDEBAR_WIDTH = 360;
const MIN_SIDEBAR_WIDTH = 220;
const MAX_SIDEBAR_WIDTH = 640;
// The tasks keep at least this share of the layout, however far the divider is dragged.
const MAX_SIDEBAR_SHARE = 0.6;
const KEYBOARD_RESIZE_STEP = 16;

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
  readonly sidebarWidth = signal(this.readStoredSidebarWidth());
  readonly resizing = signal(false);

  readonly minSidebarWidth = MIN_SIDEBAR_WIDTH;
  readonly maxSidebarWidth = MAX_SIDEBAR_WIDTH;

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
      { labelKey: 'projects.statGroups', icon: 'pi pi-folder', tone: 'green', value: this.groups().length },
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
      this.selectedProjectId.set(result.id);
      this.load();
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
      this.load();
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
      this.load();
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

  // The divider captures the pointer, so it keeps receiving the moves while dragged over the panels.
  startResize(event: PointerEvent): void {
    if (event.button !== 0) {
      return;
    }

    (event.currentTarget as HTMLElement).setPointerCapture(event.pointerId);
    this.resizing.set(true);
    event.preventDefault();
  }

  resize(event: PointerEvent): void {
    if (!this.resizing()) {
      return;
    }

    const divider = event.currentTarget as HTMLElement;
    const layout = divider.parentElement!.getBoundingClientRect();
    const width = event.clientX - layout.left - divider.offsetWidth / 2;

    this.sidebarWidth.set(this.clampSidebarWidth(width, layout.width));
  }

  endResize(event: PointerEvent): void {
    if (!this.resizing()) {
      return;
    }

    (event.currentTarget as HTMLElement).releasePointerCapture(event.pointerId);
    this.resizing.set(false);
    this.storeSidebarWidth();
  }

  resizeWithKeyboard(event: KeyboardEvent): void {
    const step = event.key === 'ArrowLeft' ? -KEYBOARD_RESIZE_STEP : event.key === 'ArrowRight' ? KEYBOARD_RESIZE_STEP : 0;

    if (step === 0) {
      return;
    }

    const layoutWidth = (event.currentTarget as HTMLElement).parentElement!.getBoundingClientRect().width;
    this.sidebarWidth.set(this.clampSidebarWidth(this.sidebarWidth() + step, layoutWidth));
    this.storeSidebarWidth();
    event.preventDefault();
  }

  resetSidebarWidth(): void {
    this.sidebarWidth.set(DEFAULT_SIDEBAR_WIDTH);
    this.storeSidebarWidth();
  }

  onDetailChanged(): void {
    this.load();
  }

  onDetailDeleted(): void {
    this.selectedProjectId.set(null);
    this.load();
  }

  private clampSidebarWidth(width: number, layoutWidth: number): number {
    const max = Math.max(MIN_SIDEBAR_WIDTH, Math.min(MAX_SIDEBAR_WIDTH, layoutWidth * MAX_SIDEBAR_SHARE));

    return Math.round(Math.min(max, Math.max(MIN_SIDEBAR_WIDTH, width)));
  }

  private readStoredSidebarWidth(): number {
    try {
      const stored = Number(localStorage.getItem(SIDEBAR_WIDTH_STORAGE_KEY));

      return stored >= MIN_SIDEBAR_WIDTH && stored <= MAX_SIDEBAR_WIDTH ? stored : DEFAULT_SIDEBAR_WIDTH;
    } catch {
      return DEFAULT_SIDEBAR_WIDTH;
    }
  }

  private storeSidebarWidth(): void {
    try {
      localStorage.setItem(SIDEBAR_WIDTH_STORAGE_KEY, String(this.sidebarWidth()));
    } catch {
      // The width then simply falls back to the default next time.
    }
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
