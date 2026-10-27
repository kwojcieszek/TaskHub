import { NgZone, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { SpeechRecognitionEventLike, SpeechRecognitionLike, SpeechRecognitionWindow } from '../models/speech-recognition.model';
import { DictationService } from './dictation.service';
import { AppLanguage, LanguageService } from './language.service';

class FakeRecognition implements SpeechRecognitionLike {
  static instances: FakeRecognition[] = [];

  lang = '';
  continuous = false;
  interimResults = true;
  started = false;
  stopped = false;
  onstart: (() => void) | null = null;
  onresult: ((event: SpeechRecognitionEventLike) => void) | null = null;
  onerror: ((event: { error: string }) => void) | null = null;
  onend: (() => void) | null = null;

  constructor() {
    FakeRecognition.instances.push(this);
  }

  start(): void {
    this.started = true;
  }

  stop(): void {
    this.stopped = true;
    this.onend?.();
  }

  say(transcript: string, isFinal = true): void {
    this.onresult?.({ resultIndex: 0, results: [{ isFinal, 0: { transcript } }] });
  }
}

describe('DictationService', () => {
  const speechWindow = window as unknown as SpeechRecognitionWindow;
  const language = signal<AppLanguage>('pl');

  let original: SpeechRecognitionWindow;

  function create(): DictationService {
    TestBed.configureTestingModule({ providers: [{ provide: LanguageService, useValue: { current: language } }] });

    return TestBed.inject(DictationService);
  }

  beforeEach(() => {
    original = { SpeechRecognition: speechWindow.SpeechRecognition, webkitSpeechRecognition: speechWindow.webkitSpeechRecognition };
    FakeRecognition.instances = [];
    language.set('pl');
    speechWindow.SpeechRecognition = undefined;
    speechWindow.webkitSpeechRecognition = FakeRecognition;
  });

  afterEach(() => {
    speechWindow.SpeechRecognition = original.SpeechRecognition;
    speechWindow.webkitSpeechRecognition = original.webkitSpeechRecognition;
  });

  it('reports the browser as unsupported when it has no speech recognition, and ends at once', () => {
    speechWindow.webkitSpeechRecognition = undefined;
    const service = create();
    const onEnd = jasmine.createSpy();

    const stop = service.start({ onText: () => undefined, onEnd, onError: () => undefined });
    stop();

    expect(service.supported).toBeFalse();
    expect(onEnd).toHaveBeenCalledTimes(1);
    expect(FakeRecognition.instances.length).toBe(0);
  });

  it('listens continuously in the language of the application', () => {
    const service = create();

    service.start({ onText: () => undefined, onEnd: () => undefined, onError: () => undefined });
    const recognition = FakeRecognition.instances[0];

    expect(service.supported).toBeTrue();
    expect(recognition.started).toBeTrue();
    expect(recognition.lang).toBe('pl-PL');
    expect(recognition.continuous).toBeTrue();
    expect(recognition.interimResults).toBeTrue();
  });

  it('uses English recognition when the application is in English', () => {
    language.set('en');

    create().start({ onText: () => undefined, onEnd: () => undefined, onError: () => undefined });

    expect(FakeRecognition.instances[0].lang).toBe('en-US');
  });

  it('passes on finished phrases without the surrounding spaces and ignores unfinished ones', () => {
    const texts: string[] = [];

    create().start({ onText: (text) => texts.push(text), onEnd: () => undefined, onError: () => undefined });
    FakeRecognition.instances[0].say(' zrobić zakupy ');
    FakeRecognition.instances[0].say('zrob', false);

    expect(texts).toEqual(['zrobić zakupy']);
  });

  it('stops listening through the returned function and reports the end', () => {
    const onEnd = jasmine.createSpy();

    const stop = create().start({ onText: () => undefined, onEnd, onError: () => undefined });
    stop();

    expect(FakeRecognition.instances[0].stopped).toBeTrue();
    expect(onEnd).toHaveBeenCalledTimes(1);
  });

  it('lets only one field listen at a time', () => {
    const service = create();
    const firstEnded = jasmine.createSpy();

    service.start({ onText: () => undefined, onEnd: firstEnded, onError: () => undefined });
    service.start({ onText: () => undefined, onEnd: () => undefined, onError: () => undefined });

    expect(FakeRecognition.instances.length).toBe(2);
    expect(FakeRecognition.instances[0].stopped).toBeTrue();
    expect(FakeRecognition.instances[1].stopped).toBeFalse();
    expect(firstEnded).toHaveBeenCalledTimes(1);
  });

  it('reports the reason when recognition fails', () => {
    const onError = jasmine.createSpy();

    create().start({ onText: () => undefined, onEnd: () => undefined, onError });
    FakeRecognition.instances[0].onerror?.({ error: 'not-allowed' });

    expect(onError).toHaveBeenCalledWith('not-allowed');
  });

  it('reports recognition progress and replaces interim text without duplicating final phrases', () => {
    const onText = jasmine.createSpy();
    const onInterim = jasmine.createSpy();
    const onListening = jasmine.createSpy();
    create().start({ onText, onInterim, onListening, onEnd: () => undefined, onError: () => undefined });
    const recognition = FakeRecognition.instances[0];
    recognition.onstart?.();
    recognition.say('kupi', false);
    recognition.say('kupić mleko', false);
    expect(onText).not.toHaveBeenCalled();
    expect(onInterim).toHaveBeenCalledWith('kupić mleko');
    recognition.say('kupić mleko');
    recognition.say('kupić mleko');
    expect(onText).toHaveBeenCalledOnceWith('kupić mleko');
    expect(onInterim).toHaveBeenCalledWith('');
    expect(onListening).toHaveBeenCalledTimes(1);
  });

  it('delivers native callbacks inside Angular even when the browser invokes them outside it', () => {
    const zones: boolean[] = [];
    create().start({ onText: () => zones.push(NgZone.isInAngularZone()), onEnd: () => undefined, onError: () => undefined });
    TestBed.inject(NgZone).runOutsideAngular(() => FakeRecognition.instances[0].say('tekst'));
    expect(zones).toEqual([true]);
  });

  it('reports an empty recognition session instead of silently stopping', () => {
    const onError = jasmine.createSpy();
    create().start({ onText: () => undefined, onEnd: () => undefined, onError });
    FakeRecognition.instances[0].onend?.();
    expect(onError).toHaveBeenCalledOnceWith('no-speech');
  });

  it('handles synchronous startup failures and allows a subsequent attempt', () => {
    const service = create();
    const onError = jasmine.createSpy();
    const onEnd = jasmine.createSpy();
    const start = spyOn(FakeRecognition.prototype, 'start').and.throwError(new DOMException('Denied', 'NotAllowedError'));
    const stop = service.start({ onText: () => undefined, onEnd, onError });
    stop();
    expect(onError).toHaveBeenCalledOnceWith('not-allowed');
    expect(onEnd).toHaveBeenCalledTimes(1);
    start.and.callThrough();
    service.start({ onText: () => undefined, onEnd: () => undefined, onError });
    expect(FakeRecognition.instances[1].started).toBeTrue();
  });

  it('ignores results that arrive after a session ends', () => {
    const onText = jasmine.createSpy();
    const stop = create().start({ onText, onEnd: () => undefined, onError: () => undefined });
    stop();
    FakeRecognition.instances[0].say('spóźniony tekst');
    expect(onText).not.toHaveBeenCalled();
  });
});
