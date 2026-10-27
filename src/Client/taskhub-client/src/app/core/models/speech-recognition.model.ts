// The parts of the browser's speech recognition (Web Speech API) the app uses.
// TypeScript's DOM library does not describe it, and Chromium still exposes it under a prefixed name.
export interface SpeechRecognitionAlternativeLike {
  transcript: string;
}

export interface SpeechRecognitionResultLike {
  isFinal: boolean;
  [index: number]: SpeechRecognitionAlternativeLike;
}

export interface SpeechRecognitionEventLike {
  resultIndex: number;
  results: ArrayLike<SpeechRecognitionResultLike>;
}

export interface SpeechRecognitionErrorLike {
  error: string;
}

export interface SpeechRecognitionLike {
  lang: string;
  continuous: boolean;
  interimResults: boolean;
  onstart: (() => void) | null;
  onresult: ((event: SpeechRecognitionEventLike) => void) | null;
  onerror: ((event: SpeechRecognitionErrorLike) => void) | null;
  onend: (() => void) | null;
  start(): void;
  stop(): void;
}

export type SpeechRecognitionFactory = new () => SpeechRecognitionLike;

export interface SpeechRecognitionWindow {
  SpeechRecognition?: SpeechRecognitionFactory;
  webkitSpeechRecognition?: SpeechRecognitionFactory;
}

export interface DictationHandlers {
  onText: (text: string) => void;
  onListening?: () => void;
  onInterim?: (text: string) => void;
  onEnd: () => void;
  onError: (code: string) => void;
}
