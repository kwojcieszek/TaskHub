import { Injectable, signal } from '@angular/core';
import { ApiNotice } from '../models/api-notice.model';

const MAX_NOTICES = 3;
const SUCCESS_VISIBLE_MS = 3000;
const ERROR_VISIBLE_MS = 8000;

@Injectable({ providedIn: 'root' })
export class ApiFeedbackService {
  readonly pending = signal(0);
  readonly notices = signal<ApiNotice[]>([]);

  private sequence = 0;

  show(kind: ApiNotice['kind'], key: string, details?: string): void {
    const notice: ApiNotice = { id: ++this.sequence, kind, key, details };

    // The same message is shown once: a repeat takes the place of the one already on the screen.
    const others = this.notices().filter((item) => item.kind !== kind || item.key !== key || item.details !== details);

    this.notices.set([...others, notice].slice(-MAX_NOTICES));

    setTimeout(() => this.dismiss(notice.id), kind === 'error' ? ERROR_VISIBLE_MS : SUCCESS_VISIBLE_MS);
  }

  dismiss(id: number): void {
    this.notices.update((items) => items.filter((item) => item.id !== id));
  }
}
