import { Injectable, inject } from '@angular/core';
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
    recognition.interimResults = false;

    recognition.onresult = (event) => {
      for (let index = event.resultIndex; index < event.results.length; index++) {
        const result = event.results[index];

        if (result.isFinal) {
          handlers.onText(result[0].transcript.trim());
        }
      }
    };

    recognition.onerror = (event) => handlers.onError(event.error);

    recognition.onend = () => {
      if (this.active === recognition) {
        this.active = null;
      }

      handlers.onEnd();
    };

    this.active = recognition;
    recognition.start();

    return () => recognition.stop();
  }

  private resolveFactory(): SpeechRecognitionFactory | null {
    const speechWindow = window as unknown as SpeechRecognitionWindow;

    return speechWindow.SpeechRecognition ?? speechWindow.webkitSpeechRecognition ?? null;
  }
}
