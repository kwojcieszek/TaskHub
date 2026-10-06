import { Component, ElementRef, EventEmitter, Input, NgZone, Output, ViewChild, effect, inject } from '@angular/core';
import { GoogleIdentityService } from '../../core/services/google-identity.service';
import { LanguageService } from '../../core/services/language.service';
import { GoogleButtonText } from '../../core/models/google-identity.model';

const BUTTON_WIDTH = 280;

@Component({
  selector: 'app-google-button',
  standalone: true,
  templateUrl: './google-button.component.html',
  styleUrl: './google-button.component.scss'
})
export class GoogleButtonComponent {
  @Input() text: GoogleButtonText = 'signin_with';
  @Output() readonly credential = new EventEmitter<string>();

  @ViewChild('host', { static: true }) private readonly host!: ElementRef<HTMLElement>;

  private readonly google = inject(GoogleIdentityService);
  private readonly language = inject(LanguageService);
  private readonly zone = inject(NgZone);

  constructor() {
    this.google.loadConfig();

    // Re-renders when the client ID arrives and whenever the language changes.
    effect(() => {
      const clientId = this.google.clientId();
      const locale = this.language.current();

      if (clientId) {
        this.render(clientId, locale);
      }
    });
  }

  private render(clientId: string, locale: string): void {
    this.google
      .renderButton(
        this.host.nativeElement,
        clientId,
        {
          type: 'standard',
          theme: 'outline',
          size: 'large',
          text: this.text,
          width: BUTTON_WIDTH,
          locale
        },
        // Google calls back outside Angular, so re-enter the zone before emitting.
        (idToken) => this.zone.run(() => this.credential.emit(idToken))
      )
      .catch(() => undefined);
  }
}
