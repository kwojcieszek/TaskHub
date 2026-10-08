import { HttpErrorResponse } from '@angular/common/http';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { ConfirmationService } from 'primeng/api';
import { Button } from 'primeng/button';
import { Card } from 'primeng/card';
import { ConfirmDialog } from 'primeng/confirmdialog';
import { InputText } from 'primeng/inputtext';
import { Select } from 'primeng/select';
import { TranslateModule, TranslateService } from '@ngx-translate/core';
import { ApiToken, CreatedApiToken } from '../../../../core/models/api-token.model';
import { ApiTokenService } from '../../../../core/services/api-token.service';
import { LocalizedDatePipe } from '../../../../shared/pipes/localized-date.pipe';

type Lifetime = '7' | '30' | '90' | '365' | 'custom' | 'never';

const LIFETIMES: Lifetime[] = ['7', '30', '90', '365', 'custom', 'never'];
const MAX_LIFETIME_DAYS = 3650;
const CONFLICT = 409;
const COPIED_MARK_MS = 1500;

@Component({
  selector: 'app-api-tokens',
  standalone: true,
  imports: [LocalizedDatePipe, ReactiveFormsModule, Card, Button, ConfirmDialog, InputText, Select, TranslateModule],
  providers: [ConfirmationService],
  templateUrl: './api-tokens.component.html',
  styleUrl: './api-tokens.component.scss'
})
export class ApiTokensComponent implements OnInit {
  private readonly apiTokenService = inject(ApiTokenService);
  private readonly confirmationService = inject(ConfirmationService);
  private readonly translate = inject(TranslateService);
  private readonly fb = inject(FormBuilder);

  readonly tokens = signal<ApiToken[]>([]);
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly errorKey = signal<string | null>(null);
  readonly copied = signal(false);

  // The token just created. It is shown once and cannot be read again after the page is left.
  readonly created = signal<CreatedApiToken | null>(null);

  readonly lifetimes = LIFETIMES;
  readonly maxLifetimeDays = MAX_LIFETIME_DAYS;

  readonly form = this.fb.nonNullable.group({
    name: ['', [Validators.required, Validators.maxLength(100)]],
    lifetime: this.fb.nonNullable.control<Lifetime>('30'),
    customDays: [30, [Validators.required, Validators.min(1), Validators.max(MAX_LIFETIME_DAYS)]]
  });

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);

    this.apiTokenService.getAll().subscribe({
      next: (tokens) => {
        this.tokens.set(tokens);
        this.loading.set(false);
      },
      error: () => {
        this.errorKey.set('profile.tokens.loadError');
        this.loading.set(false);
      }
    });
  }

  create(): void {
    if (this.form.invalid || this.saving()) {
      return;
    }

    const { name, lifetime, customDays } = this.form.getRawValue();

    this.saving.set(true);
    this.errorKey.set(null);
    this.created.set(null);

    this.apiTokenService.create({ name: name.trim(), expiresInDays: this.daysOf(lifetime, customDays) }).subscribe({
      next: (created) => {
        this.created.set(created);
        this.copied.set(false);
        this.saving.set(false);
        this.form.reset({ name: '', lifetime: '30', customDays: 30 });
        this.load();
      },
      error: (error: HttpErrorResponse) => {
        this.errorKey.set(error.status === CONFLICT ? 'profile.tokens.limitReached' : 'profile.tokens.createError');
        this.saving.set(false);
      }
    });
  }

  revoke(token: ApiToken): void {
    this.confirmationService.confirm({
      key: 'apiTokens',
      message: this.translate.instant('profile.tokens.revokeConfirm', { name: token.name }),
      header: this.translate.instant('profile.tokens.revokeHeader'),
      icon: 'pi pi-exclamation-triangle',
      acceptLabel: this.translate.instant('profile.tokens.revoke'),
      rejectLabel: this.translate.instant('common.cancel'),
      acceptButtonProps: { severity: 'danger' },
      rejectButtonProps: { severity: 'secondary', outlined: true },
      accept: () => {
        this.errorKey.set(null);

        this.apiTokenService.revoke(token.id).subscribe({
          next: () => {
            if (this.created()?.id === token.id) {
              this.created.set(null);
            }

            this.load();
          },
          error: () => this.errorKey.set('profile.tokens.revokeError')
        });
      }
    });
  }

  copy(token: string): void {
    navigator.clipboard.writeText(token).then(() => {
      this.copied.set(true);
      setTimeout(() => this.copied.set(false), COPIED_MARK_MS);
    });
  }

  dismissCreated(): void {
    this.created.set(null);
  }

  private daysOf(lifetime: Lifetime, customDays: number): number | null {
    if (lifetime === 'never') {
      return null;
    }

    return lifetime === 'custom' ? customDays : Number(lifetime);
  }
}
