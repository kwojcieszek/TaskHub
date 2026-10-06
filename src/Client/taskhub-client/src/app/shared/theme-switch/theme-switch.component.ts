import { Component } from '@angular/core';
import { TranslateModule } from '@ngx-translate/core';
import { ThemeService, ThemeMode } from '../../core/services/theme.service';

@Component({
  selector: 'app-theme-switch',
  standalone: true,
  imports: [TranslateModule],
  templateUrl: './theme-switch.component.html',
  styleUrl: './theme-switch.component.scss'
})
export class ThemeSwitchComponent {
  readonly themeModes: ThemeMode[] = ['light', 'dark', 'auto'];
  readonly themeIcons: Record<ThemeMode, string> = { light: 'pi pi-sun', dark: 'pi pi-moon', auto: 'pi pi-desktop' };

  constructor(readonly theme: ThemeService) {}
}
