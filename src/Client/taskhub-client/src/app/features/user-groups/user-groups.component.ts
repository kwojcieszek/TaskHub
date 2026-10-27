import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Select } from 'primeng/select';
import { AuthService } from '../../core/services/auth.service';
import { Accordion, AccordionPanel, AccordionHeader, AccordionContent } from 'primeng/accordion';
import { Button } from 'primeng/button';
import { ConfirmationService } from 'primeng/api';
import { ConfirmDialog } from 'primeng/confirmdialog';
import { DialogService } from 'primeng/dynamicdialog';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { UserGroupService } from '../../core/services/user-group.service';
import { UserGroup, UserGroupMember } from '../../core/models/user-group.model';
import { UserGroupDialogComponent } from './user-group-dialog.component';
import { AddUserDialogComponent } from './add-user-dialog.component';

@Component({
  selector: 'app-user-groups',
  standalone: true,
  imports: [FormsModule, Select, Accordion, AccordionPanel, AccordionHeader, AccordionContent, Button, ConfirmDialog, TranslateModule],
  providers: [DialogService, ConfirmationService],
  templateUrl: './user-groups.component.html',
  styleUrl: './user-groups.component.scss'
})
export class UserGroupsComponent implements OnInit {
  private readonly userGroupService = inject(UserGroupService);
  private readonly dialogService = inject(DialogService);
  private readonly confirmationService = inject(ConfirmationService);
  private readonly translate = inject(TranslateService);

  readonly auth = inject(AuthService);
  readonly nextOwners: Record<string, string | null> = {};
  canLeave(group: UserGroup): boolean { return group.ownerId !== this.auth.currentUser()?.id && (this.membersByGroup()[group.id] ?? []).some(m => m.userId === this.auth.currentUser()?.id); }
  transfer(group: UserGroup): void {
    const userId = this.nextOwners[group.id]; if (!userId) return;
    this.confirmationService.confirm({ message: this.translate.instant('userGroups.transferConfirm'), header: this.translate.instant('userGroups.transfer'), acceptLabel: this.translate.instant('common.yes'), rejectLabel: this.translate.instant('common.no'), accept: () => this.userGroupService.transferOwner(group.id, userId).subscribe({next: () => this.load(), error: () => {}}) });
  }
  leave(group: UserGroup): void {
    this.confirmationService.confirm({ message: this.translate.instant('userGroups.leaveConfirm'), header: this.translate.instant('userGroups.leave'), acceptLabel: this.translate.instant('common.yes'), rejectLabel: this.translate.instant('common.no'), accept: () => this.userGroupService.leave(group.id).subscribe({next: () => this.load(), error: () => {}}) });
  }
  readonly groups = signal<UserGroup[]>([]);
  readonly membersByGroup = signal<Record<string, UserGroupMember[]>>({});

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.userGroupService.getAll().subscribe((groups) => this.groups.set(groups));
  }

  loadMembers(group: UserGroup): void {
    this.userGroupService.getMembers(group.id).subscribe((members) => {
      this.membersByGroup.update((current) => ({ ...current, [group.id]: members }));
    });
  }

  createGroup(): void {
    const ref = this.dialogService.open(UserGroupDialogComponent, {
      header: this.translate.instant('userGroups.newGroup'),
      width: '440px'
    });
    ref?.onClose.subscribe((result) => {
      if (!result) {
        return;
      }
      this.load();
    });
  }

  editGroup(group: UserGroup): void {
    const ref = this.dialogService.open(UserGroupDialogComponent, {
      header: this.translate.instant('userGroups.editGroup'),
      width: '440px',
      data: { group }
    });
    ref?.onClose.subscribe((result) => {
      if (!result) {
        return;
      }
      this.load();
    });
  }

  deleteGroup(group: UserGroup): void {
    this.confirmDelete('userGroups.deleteConfirm', () => {
      this.userGroupService.delete(group.id).subscribe(() => this.load());
    });
  }

  addMember(group: UserGroup): void {
    const ref = this.dialogService.open(AddUserDialogComponent, {
      header: this.translate.instant('userGroups.addMember'),
      width: '420px', data: { groupId: group.id }
    });
    ref?.onClose.subscribe((result) => {
      if (!result) {
        return;
      }
      this.loadMembers(group);
      this.load();
    });
  }

  removeMember(group: UserGroup, member: UserGroupMember): void {
    this.confirmRemoval(this.translate.instant('userGroups.removeMemberConfirm', { name: member.displayName }), () => {
      this.userGroupService.removeMember(group.id, member.userId).subscribe(() => {
        this.loadMembers(group);
        this.load();
      });
    });
  }

  private confirmDelete(messageKey: string, accept: () => void): void {
    this.confirmRemoval(this.translate.instant(messageKey), accept);
  }

  private confirmRemoval(message: string, accept: () => void): void {
    this.confirmationService.confirm({
      message,
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
