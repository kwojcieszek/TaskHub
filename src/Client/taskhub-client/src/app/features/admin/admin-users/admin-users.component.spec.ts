import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Confirmation, ConfirmationService } from 'primeng/api';
import { DialogService } from 'primeng/dynamicdialog';
import { TranslateModule } from '@ngx-translate/core';
import { of } from 'rxjs';
import { AdminUser } from '../../../core/models/admin.model';
import { AdminService } from '../../../core/services/admin.service';
import { AuthService } from '../../../core/services/auth.service';
import { AdminUsersComponent } from './admin-users.component';

function user(roles: string[], id = 'u1'): AdminUser {
  return {
    id,
    userName: 'jan',
    email: 'jan@example.com',
    displayName: 'Jan Kowalski',
    lockedOut: false,
    twoFactorEnabled: false,
    lastLoginAt: null,
    roles
  };
}

describe('AdminUsersComponent', () => {
  let fixture: ComponentFixture<AdminUsersComponent>;
  let adminService: jasmine.SpyObj<AdminService>;
  let confirmations: Confirmation[];

  beforeEach(async () => {
    adminService = jasmine.createSpyObj<AdminService>('AdminService', ['getAllUsers', 'updateRoles', 'deleteUser']);
    adminService.deleteUser.and.returnValue(of(undefined));
    adminService.getAllUsers.and.returnValue(of([]));
    adminService.updateRoles.and.returnValue(of(user(['User'])));
    confirmations = [];

    const confirmationService = {
      confirm: (confirmation: Confirmation) => {
        confirmations.push(confirmation);
      }
    };

    await TestBed.configureTestingModule({
      imports: [AdminUsersComponent, TranslateModule.forRoot()],
      providers: [
        { provide: AdminService, useValue: adminService },
        { provide: AuthService, useValue: { currentUser: () => ({ id: 'me' }) } }
      ]
    })
      .overrideComponent(AdminUsersComponent, {
        set: {
          template: '',
          providers: [
            { provide: ConfirmationService, useValue: confirmationService },
            { provide: DialogService, useValue: {} }
          ]
        }
      })
      .compileComponents();

    fixture = TestBed.createComponent(AdminUsersComponent);
  });

  it('asks before granting the administrator role and saves only after the confirmation', () => {
    fixture.componentInstance.toggleRole(user(['User']), 'Admin');

    expect(confirmations.length).toBe(1);
    expect(adminService.updateRoles).not.toHaveBeenCalled();

    confirmations[0].accept?.();

    expect(adminService.updateRoles).toHaveBeenCalledOnceWith('u1', { roles: ['User', 'Admin'] });
  });

  it('leaves the roles unchanged when the confirmation is dismissed', () => {
    fixture.componentInstance.toggleRole(user(['User']), 'Admin');

    confirmations[0].reject?.();

    expect(adminService.updateRoles).not.toHaveBeenCalled();
  });

  it('takes the administrator role away without asking', () => {
    fixture.componentInstance.toggleRole(user(['Admin', 'User']), 'Admin');

    expect(confirmations.length).toBe(0);
    expect(adminService.updateRoles).toHaveBeenCalledOnceWith('u1', { roles: ['User'] });
  });

  it('grants an ordinary role without asking', () => {
    fixture.componentInstance.toggleRole(user([]), 'User');

    expect(confirmations.length).toBe(0);
    expect(adminService.updateRoles).toHaveBeenCalledOnceWith('u1', { roles: ['User'] });
  });

  it('asks before deleting an account and deletes only after the confirmation', () => {
    fixture.componentInstance.deleteUser(user(['User']));

    expect(confirmations.length).toBe(1);
    expect(adminService.deleteUser).not.toHaveBeenCalled();

    confirmations[0].accept?.();

    expect(adminService.deleteUser).toHaveBeenCalledOnceWith('u1');
  });

  it('recognises the row of the signed-in administrator', () => {
    expect(fixture.componentInstance.isSelf(user(['Admin'], 'me'))).toBeTrue();
    expect(fixture.componentInstance.isSelf(user(['Admin']))).toBeFalse();
  });

  it('cancels the click, so the box follows the saved roles', () => {
    const event = new Event('click', { cancelable: true });

    fixture.componentInstance.onRoleClick(event, user(['User']), 'Admin');

    expect(event.defaultPrevented).toBeTrue();
  });
});
