import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Button } from 'primeng/button';
import { InputNumber } from 'primeng/inputnumber';
import { ToggleSwitch } from 'primeng/toggleswitch';
import { TranslateModule } from '@ngx-translate/core';
import { SecuritySettingsService } from '../../../core/services/security-settings.service';
import { SecuritySettings } from '../../../core/models/security-settings.model';

const MAX_ATTEMPTS = 100;
const MAX_LOCKOUT_MINUTES = 525600;

@Component({
  selector: 'app-security-settings',
  standalone: true,
  imports: [FormsModule, Button, InputNumber, ToggleSwitch, TranslateModule],
  templateUrl: './security-settings.component.html',
  styleUrl: './security-settings.component.scss'
})
export class SecuritySettingsComponent implements OnInit {
  private readonly securitySettingsService = inject(SecuritySettingsService);

  readonly settings = signal<SecuritySettings | null>(null);
  readonly saving = signal(false);
  readonly saved = signal(false);
  readonly errorKey = signal<string | null>(null);
  readonly maxAttempts = MAX_ATTEMPTS;
  readonly maxLockoutMinutes = MAX_LOCKOUT_MINUTES;

  ngOnInit(): void {
    this.securitySettingsService.get().subscribe({
      next: (settings) => this.settings.set(settings),
      error: () => this.errorKey.set('admin.securityLoadError')
    });
  }

  update(change: Partial<SecuritySettings>): void {
    const current = this.settings();

    if (current) {
      this.settings.set({ ...current, ...change });
      this.saved.set(false);
    }
  }

  isValid(): boolean {
    const current = this.settings();

    return (
      current !== null &&
      current.maxFailedAccessAttempts >= 1 &&
      current.maxFailedAccessAttempts <= MAX_ATTEMPTS &&
      current.lockoutMinutes >= 1 &&
      current.lockoutMinutes <= MAX_LOCKOUT_MINUTES
    );
  }

  save(): void {
    const current = this.settings();

    if (!current || !this.isValid()) {
      return;
    }

    this.saving.set(true);
    this.saved.set(false);
    this.errorKey.set(null);

    this.securitySettingsService.update(current).subscribe({
      next: (settings) => {
        this.settings.set(settings);
        this.saving.set(false);
        this.saved.set(true);
      },
      error: () => {
        this.saving.set(false);
        this.errorKey.set('admin.securitySaveError');
      }
    });
  }
}
