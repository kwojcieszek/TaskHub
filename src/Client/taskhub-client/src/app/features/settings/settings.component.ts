import { Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { TranslateModule } from '@ngx-translate/core';
import { ToggleSwitch } from 'primeng/toggleswitch';
import { AuthService } from '../../core/services/auth.service';
import { AppLanguage, LanguageService } from '../../core/services/language.service';
import { ThemeMode, ThemeService } from '../../core/services/theme.service';
import { UserPreferencesService } from '../../core/services/user-preferences.service';

@Component({
  selector: 'app-settings',
  standalone: true,
  imports: [FormsModule, RouterLink, TranslateModule, ToggleSwitch],
  templateUrl: './settings.component.html',
  styleUrl: './settings.component.scss'
})
export class SettingsComponent {
  readonly auth = inject(AuthService);
  readonly language = inject(LanguageService);
  readonly theme = inject(ThemeService);
  readonly preferences = inject(UserPreferencesService);

  readonly languages: { code: AppLanguage; label: string }[] = [
    { code: 'en', label: 'English' },
    { code: 'pl', label: 'Polski' }
  ];

  readonly themeModes: { mode: ThemeMode; icon: string }[] = [
    { mode: 'light', icon: 'pi pi-sun' },
    { mode: 'dark', icon: 'pi pi-moon' },
    { mode: 'auto', icon: 'pi pi-desktop' }
  ];
}
