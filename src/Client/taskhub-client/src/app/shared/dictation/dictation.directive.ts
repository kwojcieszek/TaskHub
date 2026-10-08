import { Directive, ElementRef, OnDestroy, OnInit, Renderer2, inject } from '@angular/core';
import { TranslateService } from '@ngx-translate/core';
import { Subscription } from 'rxjs';
import { DictationService } from '../../core/services/dictation.service';

const ERROR_MARK_MS = 4000;

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
  private stopListening: (() => void) | null = null;
  private removeClickListener: (() => void) | null = null;
  private languageChange: Subscription | null = null;
  private errorKey: string | null = null;

  ngOnInit(): void {
    if (!this.dictation.supported) {
      return;
    }

    const parent = this.renderer.parentNode(this.field);
    const wrapper: HTMLElement = this.renderer.createElement('span');
    const button: HTMLButtonElement = this.renderer.createElement('button');
    const icon: HTMLElement = this.renderer.createElement('i');

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

    this.removeClickListener = this.renderer.listen(button, 'click', () => this.toggle());
    this.languageChange = this.translate.onLangChange.subscribe(() => this.updateLabel());

    this.wrapper = wrapper;
    this.button = button;
    this.updateLabel();
  }

  ngOnDestroy(): void {
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

    let ended = false;

    const stop = this.dictation.start({
      onText: (text) => this.append(text),
      onEnd: () => {
        ended = true;
        this.stopListening = null;
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
    if (!text) {
      return;
    }

    const current = this.field.value.trimEnd();
    let value = current ? `${current} ${text}` : text;

    if (this.field.maxLength > 0) {
      value = value.slice(0, this.field.maxLength);
    }

    this.field.value = value;
    // Angular forms listen for this event, exactly as when the text is typed.
    this.field.dispatchEvent(new Event('input', { bubbles: true }));
  }

  private showError(code: string): void {
    // "no-speech" and "aborted" only mean that nothing was said or that listening was stopped.
    if (code === 'no-speech' || code === 'aborted') {
      return;
    }

    this.errorKey = code === 'not-allowed' || code === 'service-not-allowed' ? 'dictation.notAllowed' : 'dictation.failed';
    this.updateLabel();

    if (this.button) {
      this.renderer.addClass(this.button, 'dictation__button--error');
    }

    setTimeout(() => {
      this.errorKey = null;
      this.updateLabel();

      if (this.button) {
        this.renderer.removeClass(this.button, 'dictation__button--error');
      }
    }, ERROR_MARK_MS);
  }

  private setActive(active: boolean): void {
    if (!this.button) {
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
    if (!this.button) {
      return;
    }

    const key = this.errorKey ?? (this.stopListening ? 'dictation.stop' : 'dictation.start');
    const label = this.translate.instant(key);

    this.renderer.setAttribute(this.button, 'title', label);
    this.renderer.setAttribute(this.button, 'aria-label', label);
  }
}
