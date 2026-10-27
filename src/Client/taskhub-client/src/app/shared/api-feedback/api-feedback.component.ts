import { Component, inject } from '@angular/core';
import { TranslateModule } from '@ngx-translate/core';
import { ApiFeedbackService } from '../../core/services/api-feedback.service';

@Component({
  selector: 'app-api-feedback', standalone: true, imports: [TranslateModule],
  template: `
    <div class="notices" aria-live="polite">
      @if (feedback.pending() > 0) { <div class="notice working" role="status"><i class="pi pi-spin pi-spinner"></i> {{ 'feedback.working' | translate }}</div> }
      @for (notice of feedback.notices(); track notice.id) {
        <div class="notice" [class.error]="notice.kind === 'error'" [attr.role]="notice.kind === 'error' ? 'alert' : 'status'">
          <i class="notice__icon" [class]="notice.kind === 'error' ? 'pi pi-times-circle' : 'pi pi-check-circle'"></i>
          <div><strong>{{ notice.key | translate }}</strong>@if (notice.details) { <p>{{ notice.details }}</p> }</div>
          <button type="button" (click)="feedback.dismiss(notice.id)" [attr.aria-label]="'common.close' | translate"><i class="pi pi-times"></i></button>
        </div>
      }
    </div>`,
  styles: [`:host { position: fixed; bottom: 1rem; right: 1rem; z-index: 1300; max-width: min(28rem, calc(100vw - 2rem)); }
    .notices { display: grid; gap: .5rem; } .notice { display: flex; align-items: start; gap: .75rem; padding: .75rem 1rem; background: var(--app-surface); color: var(--p-text-color); border: 1px solid var(--app-border); border-radius: 8px; box-shadow: var(--app-shadow); }
    .notice__icon { margin-top: .15rem; color: var(--tone-green-solid); } .error .notice__icon { color: var(--tone-red-solid); }
    p { margin: .3rem 0 0; font-size: .85rem; overflow-wrap: anywhere; } button { margin-left: auto; border: 0; background: transparent; color: inherit; cursor: pointer; padding: .2rem; }`]
})
export class ApiFeedbackComponent { readonly feedback = inject(ApiFeedbackService); }
