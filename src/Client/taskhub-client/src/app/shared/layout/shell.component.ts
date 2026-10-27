import { Component, ElementRef, HostListener, OnInit, ViewChild, signal } from '@angular/core';
import { Router, RouterLink, RouterOutlet } from '@angular/router';
import { Menubar } from 'primeng/menubar';
import { MenuItem } from 'primeng/api';
import { TranslateService, TranslateModule } from '@ngx-translate/core';
import { AuthService } from '../../core/services/auth.service';
import { ThemeSwitchComponent } from '../theme-switch/theme-switch.component';
import { ApiFeedbackComponent } from '../api-feedback/api-feedback.component';
import { ApiFeedbackService } from '../../core/services/api-feedback.service';
import { inject } from '@angular/core';

@Component({
  selector: 'app-shell',
  standalone: true,
  imports: [RouterOutlet, RouterLink, Menubar, TranslateModule, ThemeSwitchComponent, ApiFeedbackComponent],
  templateUrl: './shell.component.html',
  styleUrl: './shell.component.scss'
})
export class ShellComponent implements OnInit {
  @ViewChild('userMenu') private readonly userMenu?: ElementRef<HTMLElement>;

  menuItems: MenuItem[] = [];

  readonly userMenuOpen = signal(false);
  readonly feedback = inject(ApiFeedbackService);

  constructor(
    readonly auth: AuthService,
    private readonly translate: TranslateService,
    private readonly router: Router
  ) {}

  ngOnInit(): void {
    this.buildMenus();
    this.translate.onLangChange.subscribe(() => this.buildMenus());
  }

  initials(): string {
    const name = this.auth.currentUser()?.displayName ?? '?';
    return name.trim().charAt(0).toUpperCase();
  }

  private buildMenus(): void {
    this.menuItems = [
      { label: this.translate.instant('nav.dashboard'), icon: 'pi pi-home', routerLink: '/app/dashboard' },
      { label: this.translate.instant('nav.projects'), icon: 'pi pi-folder', routerLink: '/app/projects' },
      { label: this.translate.instant('nav.tasks'), icon: 'pi pi-check-square', routerLink: '/app/tasks' },
      { label: this.translate.instant('nav.calendar'), icon: 'pi pi-calendar', routerLink: '/app/calendar' },
      { label: this.translate.instant('nav.reports'), icon: 'pi pi-chart-bar', routerLink: '/app/reports' },
      { label: this.translate.instant('nav.userGroups'), icon: 'pi pi-users', routerLink: '/app/user-groups' }
    ];

    if (this.auth.isAdmin()) {
      this.menuItems.push({
        label: this.translate.instant('nav.admin'),
        icon: 'pi pi-shield',
        routerLink: '/app/admin'
      });
    }
  }

  toggleUserMenu(): void {
    this.userMenuOpen.update((open) => !open);
  }

  closeUserMenu(): void {
    this.userMenuOpen.set(false);
  }

  @HostListener('document:click', ['$event'])
  onDocumentClick(event: MouseEvent): void {
    if (this.userMenuOpen() && !this.userMenu?.nativeElement.contains(event.target as Node)) {
      this.closeUserMenu();
    }
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.closeUserMenu();
  }

  logout(): void {
    this.closeUserMenu();
    this.auth.endSession().subscribe({ next: () => this.router.navigate(['/login']), error: () => {} });
  }
}
