import { Component, signal } from '@angular/core';
import { Tab, TabList, Tabs } from 'primeng/tabs';
import { TranslateModule } from '@ngx-translate/core';
import { ActivityLogsComponent } from './activity-logs/activity-logs.component';
import { AdminUsersComponent } from './admin-users/admin-users.component';
import { LoginLogsComponent } from './login-logs/login-logs.component';
import { SecuritySettingsComponent } from './security-settings/security-settings.component';
import { UserGroupsComponent } from '../user-groups/user-groups.component';

@Component({
  selector: 'app-admin',
  standalone: true,
  imports: [
    Tabs,
    TabList,
    Tab,
    TranslateModule,
    AdminUsersComponent,
    UserGroupsComponent,
    LoginLogsComponent,
    ActivityLogsComponent,
    SecuritySettingsComponent
  ],
  templateUrl: './admin.component.html',
  styleUrl: './admin.component.scss'
})
export class AdminComponent {
  readonly activeTab = signal<string | number>('users');
}
