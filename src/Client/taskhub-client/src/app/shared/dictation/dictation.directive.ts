import { Directive, ElementRef, OnDestroy, OnInit, Renderer2, inject } from '@angular/core';
import { TranslateService } from '@ngx-translate/core';
import { Subscription } from 'rxjs';
import { DictationService } from '../../core/services/dictation.service';

/**
 * Adds a microphone button to a text field. While it is on, what the user says is appended to the field
 * as if it had been typed, so forms and validation notice it the usual way.
 */
@Directive({
  selector: 'input[appDictation], textarea[appDictation]',
  standalone: true
})
export class DictationDirective implements OnInit, OnDestroy {
  private readonly field = inject<ElementRef<HTMLInputElement | HTMLTextAreaElement>>(ElementRef).nativeElement;
  private readonly renderer = inject(Renderer2);
  private readonly dictation = inject(DictationService);
  private readonly translate = inject(TranslateService);

  private wrapper: HTMLElement | null = null;
  private button: HTMLButtonElement | null = null;
  private status: HTMLElement | null = null;
  private stopListening: (() => void) | null = null;
  private removeClickListener: (() => void) | null = null;
  private languageChange: Subscription | null = null;
  private errorKey: string | null = null;
  private statusKey: string | null = null;
  private interim = '';
  private destroyed = false;

  ngOnInit(): void {
    if (!this.dictation.supported) {
      return;
    }

    const parent = this.renderer.parentNode(this.field);
    const wrapper: HTMLElement = this.renderer.createElement('span');
    const button: HTMLButtonElement = this.renderer.createElement('button');
    const icon: HTMLElement = this.renderer.createElement('i');
    const status: HTMLElement = this.renderer.createElement('span');

    this.renderer.addClass(wrapper, 'dictation');
    this.renderer.insertBefore(parent, wrapper, this.field);
    this.renderer.appendChild(wrapper, this.field);
    this.renderer.addClass(this.field, 'dictation__field');

    this.renderer.setAttribute(button, 'type', 'button');
    this.renderer.addClass(button, 'dictation__button');
    this.renderer.addClass(icon, 'pi');
    this.renderer.addClass(icon, 'pi-microphone');
    this.renderer.appendChild(button, icon);
    this.renderer.appendChild(wrapper, button);
    this.renderer.addClass(status, 'dictation__status');
    this.renderer.setAttribute(status, 'role', 'status');
    this.renderer.setAttribute(status, 'aria-live', 'polite');
    this.renderer.appendChild(wrapper, status);

    this.removeClickListener = this.renderer.listen(button, 'click', () => this.toggle());
    this.languageChange = this.translate.onLangChange.subscribe(() => this.updateLabel());

    this.wrapper = wrapper;
    this.button = button;
    this.status = status;
    this.updateLabel();
  }

  ngOnDestroy(): void {
    this.destroyed = true;
    this.stopListening?.();
    this.removeClickListener?.();
    this.languageChange?.unsubscribe();

    if (this.wrapper) {
      this.renderer.removeChild(this.renderer.parentNode(this.wrapper), this.wrapper);
    }
  }

  private toggle(): void {
    if (this.stopListening) {
      this.stopListening();
      return;
    }

    if (this.field.disabled || this.field.readOnly) {
      return;
    }

    this.errorKey = null;
    this.interim = '';
    this.statusKey = 'dictation.starting';
    if (this.button) this.renderer.removeClass(this.button, 'dictation__button--error');
    this.updateLabel();

    let ended = false;

    const stop = this.dictation.start({
      onText: (text) => this.append(text),
      onListening: () => {
        this.statusKey = 'dictation.listening';
        this.updateLabel();
      },
      onInterim: (text) => {
        this.interim = text;
        this.updateLabel();
      },
      onEnd: () => {
        ended = true;
        this.stopListening = null;
        this.statusKey = null;
        this.interim = '';
        this.setActive(false);
      },
      onError: (code) => this.showError(code)
    });

    // Listening may end before it has begun, for example when the browser refuses it outright.
    if (!ended) {
      this.stopListening = stop;
      this.setActive(true);
    }
  }

  private append(text: string): void {
    if (!text || this.destroyed || this.field.disabled || this.field.readOnly) {
      return;
    }

    const current = this.field.value.trimEnd();
    let value = current ? `${current} ${text}` : text;

    if (this.field.maxLength >= 0) {
      value = value.slice(0, this.field.maxLength);
    }

    this.field.value = value;
    // Angular forms listen for this event, exactly as when the text is typed.
    this.field.dispatchEvent(new Event('input', { bubbles: true }));
  }

  private showError(code: string): void {
    if (code === 'aborted' || this.destroyed) {
      return;
    }

    const keys: Record<string, string> = {
      'not-allowed': 'dictation.notAllowed',
      'service-not-allowed': 'dictation.serviceNotAllowed',
      'no-speech': 'dictation.noSpeech',
      'audio-capture': 'dictation.audioCapture',
      'network': 'dictation.network',
      'language-not-supported': 'dictation.languageNotSupported'
    };
    this.errorKey = keys[code] ?? 'dictation.failed';
    this.interim = '';
    this.updateLabel();

    if (this.button) {
      this.renderer.addClass(this.button, 'dictation__button--error');
    }

  }

  private setActive(active: boolean): void {
    if (!this.button || this.destroyed) {
      return;
    }

    if (active) {
      this.renderer.addClass(this.button, 'dictation__button--active');
    } else {
      this.renderer.removeClass(this.button, 'dictation__button--active');
    }

    this.renderer.setAttribute(this.button, 'aria-pressed', String(active));
    this.updateLabel();
  }

  private updateLabel(): void {
    if (!this.button || this.destroyed) {
      return;
    }

    const key = this.errorKey ?? (this.stopListening ? 'dictation.stop' : 'dictation.start');
    const label = this.translate.instant(key);

    this.renderer.setAttribute(this.button, 'title', label);
    this.renderer.setAttribute(this.button, 'aria-label', label);
    if (this.status) {
      const statusKey = this.errorKey ?? this.statusKey;
      const message = this.interim || (statusKey ? this.translate.instant(statusKey) : '');
      this.renderer.setProperty(this.status, 'textContent', message);
      this.renderer.setProperty(this.status, 'hidden', !message);
      if (this.errorKey) {
        this.renderer.addClass(this.status, 'dictation__status--error');
      } else {
        this.renderer.removeClass(this.status, 'dictation__status--error');
      }
    }
  }
}
