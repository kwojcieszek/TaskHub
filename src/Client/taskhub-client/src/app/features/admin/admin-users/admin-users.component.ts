import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { TableModule } from 'primeng/table';
import { Button } from 'primeng/button';
import { Checkbox } from 'primeng/checkbox';
import { TranslateModule } from '@ngx-translate/core';
import { AdminService } from '../../../core/services/admin.service';
import { AdminUser } from '../../../core/models/admin.model';
import { LocalizedDatePipe } from '../../../shared/pipes/localized-date.pipe';

const AVAILABLE_ROLES = ['Admin', 'User'];

@Component({
  selector: 'app-admin-users',
  standalone: true,
  imports: [LocalizedDatePipe, FormsModule, TableModule, Button, Checkbox, TranslateModule],
  templateUrl: './admin-users.component.html',
  styleUrl: './admin-users.component.scss'
})
export class AdminUsersComponent implements OnInit {
  private readonly adminService = inject(AdminService);

  readonly users = signal<AdminUser[]>([]);
  readonly availableRoles = AVAILABLE_ROLES;

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.adminService.getAllUsers().subscribe((users) => this.users.set(users));
  }

  hasRole(user: AdminUser, role: string): boolean {
    return user.roles.includes(role);
  }

  toggleRole(user: AdminUser, role: string): void {
    const roles = this.hasRole(user, role) ? user.roles.filter((r) => r !== role) : [...user.roles, role];
    this.adminService.updateRoles(user.id, { roles }).subscribe(() => this.load());
  }

  toggleLockout(user: AdminUser): void {
    this.adminService.setLockout(user.id, { lockedOut: !user.lockedOut }).subscribe(() => this.load());
  }

  deleteUser(user: AdminUser): void {
    this.adminService.deleteUser(user.id).subscribe(() => this.load());
  }
}
