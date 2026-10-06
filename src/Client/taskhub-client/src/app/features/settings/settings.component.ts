import { Component, inject } from '@angular/core';
import { RouterLink } from '@angular/router';
import { TranslateModule } from '@ngx-translate/core';
import { AuthService } from '../../core/services/auth.service';
import { AppLanguage, LanguageService } from '../../core/services/language.service';
import { ThemeMode, ThemeService } from '../../core/services/theme.service';

@Component({
  selector: 'app-settings',
  standalone: true,
  imports: [RouterLink, TranslateModule],
  templateUrl: './settings.component.html',
  styleUrl: './settings.component.scss'
})
export class SettingsComponent {
  readonly auth = inject(AuthService);
  readonly language = inject(LanguageService);
  readonly theme = inject(ThemeService);

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
