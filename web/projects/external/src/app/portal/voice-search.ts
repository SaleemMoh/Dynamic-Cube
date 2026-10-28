import { DOCUMENT, Injectable, inject, signal } from '@angular/core';
import { Language } from '@dynamic-cube/shared';

interface RecognitionResultEvent {
  results: ArrayLike<ArrayLike<{ transcript: string }>>;
}

interface Recognition {
  lang: string;
  interimResults: boolean;
  maxAlternatives: number;
  onresult: ((event: RecognitionResultEvent) => void) | null;
  onerror: (() => void) | null;
  onend: (() => void) | null;
  start(): void;
}

type RecognitionConstructor = new () => Recognition;

/** Speech-to-text for the search box, where the browser offers it (Chrome, Edge, Safari); otherwise not shown. */
@Injectable({ providedIn: 'root' })
export class VoiceSearch {
  private readonly window = inject(DOCUMENT).defaultView as (Window & { SpeechRecognition?: RecognitionConstructor; webkitSpeechRecognition?: RecognitionConstructor }) | null;
  private readonly recognition = this.window?.SpeechRecognition ?? this.window?.webkitSpeechRecognition;

  readonly supported = !!this.recognition;
  readonly listening = signal(false);

  /** Listens for one phrase and returns it, or null when nothing was understood. */
  listen(language: Language): Promise<string | null> {
    if (!this.recognition || this.listening()) return Promise.resolve(null);
    const recognition = new this.recognition();
    recognition.lang = language === 'ar' ? 'ar-SA' : 'en-US';
    recognition.interimResults = false;
    recognition.maxAlternatives = 1;
    this.listening.set(true);
    return new Promise((resolve) => {
      let heard: string | null = null;
      recognition.onresult = (event) => (heard = event.results[0]?.[0]?.transcript?.trim() || null);
      recognition.onerror = () => (heard = null);
      recognition.onend = () => {
        this.listening.set(false);
        resolve(heard);
      };
      recognition.start();
    });
  }
}
