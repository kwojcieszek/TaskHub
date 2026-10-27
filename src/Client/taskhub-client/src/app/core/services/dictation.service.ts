import { Injectable, NgZone, inject } from '@angular/core';
import {
  DictationHandlers,
  SpeechRecognitionFactory,
  SpeechRecognitionLike,
  SpeechRecognitionWindow
} from '../models/speech-recognition.model';
import { AppLanguage, LanguageService } from './language.service';

const RECOGNITION_LOCALES: Record<AppLanguage, string> = { pl: 'pl-PL', en: 'en-US' };

/**
 * Turns speech into text with the recognition built into the browser. Only one field listens at a time.
 * Browsers without the feature report it as unsupported and the microphone buttons are not shown.
 */
@Injectable({ providedIn: 'root' })
export class DictationService {
  private readonly language = inject(LanguageService);
  private readonly zone = inject(NgZone);
  private readonly factory: SpeechRecognitionFactory | null = this.resolveFactory();

  private active: SpeechRecognitionLike | null = null;

  readonly supported = this.factory !== null;

  /** Starts listening in the language of the application and returns the function that stops it. */
  start(handlers: DictationHandlers): () => void {
    this.active?.stop();

    if (!this.factory) {
      handlers.onEnd();
      return () => undefined;
    }

    const recognition = new this.factory();
    recognition.lang = RECOGNITION_LOCALES[this.language.current()];
    recognition.continuous = true;
    recognition.interimResults = true;
    let ended = false;
    let stopped = false;
    let failed = false;
    let receivedText = false;
    const delivered = new Set<number>();

    recognition.onstart = () => this.zone.run(() => {
      if (!ended) handlers.onListening?.();
    });

    // Speech recognition callbacks are not patched by Zone.js in every browser.
    recognition.onresult = (event) => this.zone.run(() => {
      if (ended || this.active !== recognition) return;

      for (let index = event.resultIndex; index < event.results.length; index++) {
        const result = event.results[index];

        if (result.isFinal && !delivered.has(index)) {
          delivered.add(index);
          const text = result[0].transcript.trim();
          if (text) {
            receivedText = true;
            handlers.onText(text);
          }
        }
      }

      const interim = Array.from(event.results)
        .filter(result => !result.isFinal)
        .map(result => result[0].transcript.trim())
        .join(' ');
      handlers.onInterim?.(interim);
    });

    recognition.onerror = (event) => this.zone.run(() => {
      if (ended) return;
      failed = true;
      handlers.onError(event.error);
    });

    recognition.onend = () => this.zone.run(() => {
      if (ended) return;
      ended = true;
      if (this.active === recognition) {
        this.active = null;
      }

      if (!receivedText && !failed && !stopped) handlers.onError('no-speech');
      handlers.onInterim?.('');
      handlers.onEnd();
    });

    this.active = recognition;
    try {
      recognition.start();
    } catch (error) {
      const name = error instanceof DOMException ? error.name : '';
      recognition.onerror({ error: name === 'NotAllowedError' || name === 'SecurityError' ? 'not-allowed' : 'start-failed' });
      recognition.onend();
    }

    return () => {
      if (ended || stopped) return;
      stopped = true;
      recognition.stop();
    };
  }

  private resolveFactory(): SpeechRecognitionFactory | null {
    const speechWindow = window as unknown as SpeechRecognitionWindow;

    return speechWindow.SpeechRecognition ?? speechWindow.webkitSpeechRecognition ?? null;
  }
}
