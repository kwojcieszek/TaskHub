import { Component } from '@angular/core';
import { ComponentFixture, TestBed, fakeAsync, tick } from '@angular/core/testing';
import { FormControl, ReactiveFormsModule } from '@angular/forms';
import { TranslateModule } from '@ngx-translate/core';
import { DictationHandlers } from '../../core/models/speech-recognition.model';
import { DictationService } from '../../core/services/dictation.service';
import { DictationDirective } from './dictation.directive';

@Component({
  standalone: true,
  imports: [ReactiveFormsModule, DictationDirective],
  template: `
    @if (visible) {
      <label class="host">
        <input appDictation [formControl]="control" maxlength="20" />
      </label>
    }
  `
})
class HostComponent {
  readonly control = new FormControl('', { nonNullable: true });
  visible = true;
}

describe('DictationDirective', () => {
  let fixture: ComponentFixture<HostComponent>;
  let handlers: DictationHandlers | null;
  let stop: jasmine.Spy;

  function create(supported = true): void {
    handlers = null;
    stop = jasmine.createSpy('stop').and.callFake(() => handlers?.onEnd());

    const dictation = {
      supported,
      start: (given: DictationHandlers) => {
        handlers = given;
        return stop;
      }
    };

    TestBed.configureTestingModule({
      imports: [HostComponent, TranslateModule.forRoot()],
      providers: [{ provide: DictationService, useValue: dictation }]
    });

    fixture = TestBed.createComponent(HostComponent);
    fixture.detectChanges();
  }

  function element(): HTMLElement {
    return fixture.nativeElement as HTMLElement;
  }

  function button(): HTMLButtonElement {
    return element().querySelector('.dictation__button') as HTMLButtonElement;
  }

  it('wraps the field and adds a microphone button that does not submit a form', () => {
    create();

    const wrapper = element().querySelector('.dictation') as HTMLElement;

    expect(wrapper.querySelector('input')).not.toBeNull();
    expect(wrapper.parentElement?.classList.contains('host')).toBeTrue();
    expect(button().type).toBe('button');
    expect(button().getAttribute('title')).toBe('dictation.start');
    expect(button().querySelector('.pi-microphone')).not.toBeNull();
  });

  it('adds nothing when the browser cannot recognise speech', () => {
    create(false);

    expect(element().querySelector('.dictation')).toBeNull();
    expect(element().querySelector('.dictation__button')).toBeNull();
    expect(element().querySelector('input')).not.toBeNull();
  });

  it('starts listening on a click and shows that it is on', () => {
    create();

    button().click();

    expect(handlers).not.toBeNull();
    expect(button().classList.contains('dictation__button--active')).toBeTrue();
    expect(button().getAttribute('aria-pressed')).toBe('true');
    expect(button().getAttribute('title')).toBe('dictation.stop');
  });

  it('puts what was said into the form control, after the text already there', () => {
    create();
    fixture.componentInstance.control.setValue('Kupić ');
    fixture.detectChanges();

    button().click();
    handlers?.onText('mleko');
    handlers?.onText('i chleb');

    expect(fixture.componentInstance.control.value).toBe('Kupić mleko i chleb');
  });

  it('does not put a space before the first words of an empty field', () => {
    create();

    button().click();
    handlers?.onText('pierwsze');

    expect(fixture.componentInstance.control.value).toBe('pierwsze');
  });

  it('never exceeds the maximum length of the field', () => {
    create();

    button().click();
    handlers?.onText('to jest zdecydowanie za długie zdanie');

    expect(fixture.componentInstance.control.value.length).toBe(20);
  });

  it('stops on a second click and shows that it is off', () => {
    create();

    button().click();
    button().click();

    expect(stop).toHaveBeenCalledTimes(1);
    expect(button().classList.contains('dictation__button--active')).toBeFalse();
    expect(button().getAttribute('title')).toBe('dictation.start');
  });

  it('does not start on a disabled field', () => {
    create();
    fixture.componentInstance.control.disable();
    fixture.detectChanges();

    button().click();

    expect(handlers).toBeNull();
  });

  it('keeps a microphone error visible until the next attempt', fakeAsync(() => {
    create();

    button().click();
    handlers?.onError('not-allowed');
    handlers?.onEnd();

    expect(button().classList.contains('dictation__button--error')).toBeTrue();
    expect(button().getAttribute('title')).toBe('dictation.notAllowed');

    tick(4000);

    expect(element().querySelector('.dictation__status')?.textContent).toBe('dictation.notAllowed');
    expect(button().classList.contains('dictation__button--error')).toBeTrue();
    button().click();
    expect(button().classList.contains('dictation__button--error')).toBeFalse();
    expect(element().querySelector('.dictation__status')?.textContent).toBe('dictation.starting');
  }));

  it('explains when no speech could be recognised', () => {
    create();

    button().click();
    handlers?.onError('no-speech');

    expect(element().querySelector('.dictation__status')?.textContent).toBe('dictation.noSpeech');
  });

  it('previews changing hypotheses and inserts only the confirmed phrase', () => {
    create();
    button().click();
    handlers?.onListening?.();
    expect(element().querySelector('.dictation__status')?.textContent).toBe('dictation.listening');
    handlers?.onInterim?.('kupi');
    handlers?.onInterim?.('kupić mleko');
    expect(element().querySelector('.dictation__status')?.textContent).toBe('kupić mleko');
    expect(fixture.componentInstance.control.value).toBe('');
    handlers?.onText('kupić mleko');
    handlers?.onEnd();
    expect(fixture.componentInstance.control.value).toBe('kupić mleko');
    expect(fixture.componentInstance.control.dirty).toBeTrue();
    expect((element().querySelector('.dictation__status') as HTMLElement).hidden).toBeTrue();
  });

  it('distinguishes a recognition service failure from microphone permissions', () => {
    create();
    button().click();
    handlers?.onError('network');
    handlers?.onEnd();
    expect(element().querySelector('.dictation__status')?.textContent).toBe('dictation.network');
  });

  it('does not write results to a field disabled while listening', () => {
    create();
    button().click();
    fixture.componentInstance.control.disable();
    handlers?.onText('tekst');
    expect(fixture.componentInstance.control.value).toBe('');
  });

  it('stops listening and removes its button when the field goes away', () => {
    create();
    button().click();

    fixture.componentInstance.visible = false;
    fixture.detectChanges();

    expect(stop).toHaveBeenCalled();
    expect(element().querySelector('.dictation')).toBeNull();
  });
});
