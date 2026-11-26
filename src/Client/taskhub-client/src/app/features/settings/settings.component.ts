import { Component, OnInit, computed, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { Select } from 'primeng/select';
import { ToggleSwitch } from 'primeng/toggleswitch';
import { AuthService } from '../../core/services/auth.service';
import { AppLanguage, LanguageService } from '../../core/services/language.service';
import { DEFAULT_SKIN_ID, SkinSelectionService } from '../../core/services/skin-selection.service';
import { ThemeMode, ThemeService } from '../../core/services/theme.service';
import { UserPreferencesService } from '../../core/services/user-preferences.service';

@Component({
  selector: 'app-settings',
  standalone: true,
  imports: [FormsModule, RouterLink, TranslateModule, Select, ToggleSwitch],
  templateUrl: './settings.component.html',
  styleUrl: './settings.component.scss'
})
export class SettingsComponent implements OnInit {
  private readonly translate = inject(TranslateService);

  readonly auth = inject(AuthService);
  readonly language = inject(LanguageService);
  readonly theme = inject(ThemeService);
  readonly preferences = inject(UserPreferencesService);
  readonly skins = inject(SkinSelectionService);

  readonly languages: { code: AppLanguage; label: string }[] = [
    { code: 'en', label: 'English' },
    { code: 'pl', label: 'Polski' }
  ];

  readonly themeModes: { mode: ThemeMode; icon: string }[] = [
    { mode: 'light', icon: 'pi pi-sun' },
    { mode: 'dark', icon: 'pi pi-moon' },
    { mode: 'auto', icon: 'pi pi-desktop' }
  ];

  // The default skin is the built-in look; its label follows the interface language.
  readonly skinOptions = computed(() => {
    this.language.current();

    const options = this.skins.skins().map((skin) => ({ value: skin.id, label: skin.name }));

    return [{ value: DEFAULT_SKIN_ID, label: this.translate.instant('settings.skinDefault') }, ...options];
  });

  readonly selectedSkinDescription = computed(() => {
    const selected = this.skins.skins().find((skin) => skin.id === this.skins.selectedId());

    return selected?.description ?? null;
  });

  ngOnInit(): void {
    this.skins.refresh();
  }
}
