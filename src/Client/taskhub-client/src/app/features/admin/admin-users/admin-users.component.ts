import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TableModule } from 'primeng/table';
import { Button } from 'primeng/button';
import { Checkbox } from 'primeng/checkbox';
import { ConfirmationService } from 'primeng/api';
import { ConfirmDialog } from 'primeng/confirmdialog';
import { DialogService } from 'primeng/dynamicdialog';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { AdminService } from '../../../core/services/admin.service';
import { AuthService } from '../../../core/services/auth.service';
import { AdminUser } from '../../../core/models/admin.model';
import { LocalizedDatePipe } from '../../../shared/pipes/localized-date.pipe';
import { PasswordDialogComponent } from './password-dialog/password-dialog.component';
import { UserDialogComponent } from './user-dialog/user-dialog.component';

const ADMIN_ROLE = 'Admin';
const AVAILABLE_ROLES = [ADMIN_ROLE, 'User'];

@Component({
  selector: 'app-admin-users',
  standalone: true,
  imports: [LocalizedDatePipe, FormsModule, TableModule, Button, Checkbox, ConfirmDialog, TranslateModule],
  providers: [ConfirmationService],
  templateUrl: './admin-users.component.html',
  styleUrl: './admin-users.component.scss'
})
export class AdminUsersComponent implements OnInit {
  private readonly adminService = inject(AdminService);
  private readonly dialogService = inject(DialogService);
  private readonly confirmationService = inject(ConfirmationService);
  private readonly translate = inject(TranslateService);
  private readonly auth = inject(AuthService);

  readonly users = signal<AdminUser[]>([]);
  readonly errorKey = signal<string | null>(null);
  readonly availableRoles = AVAILABLE_ROLES;

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.adminService.getAllUsers().subscribe((users) => this.users.set(users));
  }

  createUser(): void {
    const ref = this.dialogService.open(UserDialogComponent, {
      header: this.translate.instant('admin.newUser'),
      width: '480px'
    });
    ref?.onClose.subscribe((result) => {
      if (!result) {
        return;
      }

      this.errorKey.set(null);
      this.load();
    });
  }

  resetPassword(user: AdminUser): void {
    const ref = this.dialogService.open(PasswordDialogComponent, {
      header: `${this.translate.instant('admin.resetPassword')}: ${user.userName}`,
      width: '420px', data: { userId: user.id }
    });
    ref?.onClose.subscribe((newPassword) => {
      if (!newPassword) {
        return;
      }

      this.errorKey.set(null);
      this.load();
    });
  }

  // The server refuses to lock or delete the account that is signed in, so the buttons are not offered for it.
  isSelf(user: AdminUser): boolean {
    return user.id === this.auth.currentUser()?.id;
  }

  hasRole(user: AdminUser, role: string): boolean {
    return user.roles.includes(role);
  }

  // The click is cancelled, so the box changes only when the server has saved the role, not when the
  // administrator backs out of the confirmation.
  onRoleClick(event: Event, user: AdminUser, role: string): void {
    event.preventDefault();
    this.toggleRole(user, role);
  }

  toggleRole(user: AdminUser, role: string): void {
    if (this.hasRole(user, role)) {
      this.saveRoles(user, user.roles.filter((r) => r !== role));

      return;
    }

    const roles = [...user.roles, role];

    if (role !== ADMIN_ROLE) {
      this.saveRoles(user, roles);

      return;
    }

    this.confirmationService.confirm({
      key: 'adminUsers',
      message: this.translate.instant('admin.grantAdminConfirm', { name: user.displayName }),
      header: this.translate.instant('admin.grantAdminHeader'),
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: this.translate.instant('admin.grantAdmin'),
      rejectLabel: this.translate.instant('common.cancel'),
      acceptButtonProps: { severity: 'danger' },
      rejectButtonProps: { severity: 'secondary', outlined: true },
      accept: () => this.saveRoles(user, roles)
    });
  }

  private saveRoles(user: AdminUser, roles: string[]): void {
    this.adminService.updateRoles(user.id, { roles }).subscribe(() => this.load());
  }

  toggleLockout(user: AdminUser): void {
    this.adminService.setLockout(user.id, { lockedOut: !user.lockedOut }).subscribe(() => this.load());
  }

  deleteUser(user: AdminUser): void {
    this.confirmationService.confirm({
      key: 'adminUsers',
      message: this.translate.instant('admin.deleteUserConfirm', { name: user.displayName }),
      header: this.translate.instant('common.deleteConfirmHeader'),
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: this.translate.instant('common.delete'),
      rejectLabel: this.translate.instant('common.cancel'),
      acceptButtonProps: { severity: 'danger' },
      rejectButtonProps: { severity: 'secondary', outlined: true },
      accept: () => this.adminService.deleteUser(user.id).subscribe(() => this.load())
    });
  }
}
