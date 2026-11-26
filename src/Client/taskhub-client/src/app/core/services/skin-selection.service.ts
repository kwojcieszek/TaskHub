import { Injectable, inject, signal } from '@angular/core';
import { Observable, of, tap } from 'rxjs';
import { Skin } from '../models/skin.model';
import { SKINS_DIRECTORY, SkinCatalogService } from './skin-catalog.service';

export const DEFAULT_SKIN_ID = 'default';

const STORAGE_KEY = 'taskhub.skin';
const LINK_ELEMENT_ID = 'taskhub-skin';

interface StoredSkin {
  id: string;
  href: string;
}

/**
 * Applies the skin chosen by the user: the stylesheet of a skin from the skins/ directory is linked after the styles
 * of the app, so it overrides their design tokens. The default skin is the look of the app without one.
 * The applied skin is kept in localStorage, so it is shown at once on start-up and on the sign-in pages.
 */
@Injectable({ providedIn: 'root' })
export class SkinSelectionService {
  private readonly catalog = inject(SkinCatalogService);

  private readonly skinsSignal = signal<Skin[]>([]);
  private readonly selectedIdSignal = signal<string>(DEFAULT_SKIN_ID);

  private loaded = false;
  private requestId = 0;
  private headObserver: MutationObserver | null = null;

  readonly skins = this.skinsSignal.asReadonly();
  readonly selectedId = this.selectedIdSignal.asReadonly();

  constructor() {
    const stored = this.readStored();

    if (stored) {
      this.selectedIdSignal.set(stored.id);
      this.applyStylesheet(stored.href);
    }
  }

  /** Reads the skins directory again and re-checks the current choice against it. */
  refresh(): void {
    this.loadSkins(true).subscribe(() => this.select(this.selectedIdSignal()));
  }

  /** Chooses the skin at once and links its stylesheet; a skin that is no longer deployed falls back to the default. */
  select(id: string): void {
    const request = ++this.requestId;

    if (id === DEFAULT_SKIN_ID) {
      this.useDefault();

      return;
    }

    this.selectedIdSignal.set(id);

    this.loadSkins(false).subscribe((skins) => {
      if (request !== this.requestId) {
        return;
      }

      const skin = skins.find((candidate) => candidate.id === id);

      if (!skin) {
        this.useDefault();

        return;
      }

      const href = this.catalog.stylesheetUrl(skin);

      this.applyStylesheet(href);
      this.writeStored({ id: skin.id, href });
    });
  }

  private loadSkins(force: boolean): Observable<Skin[]> {
    if (this.loaded && !force) {
      return of(this.skinsSignal());
    }

    return this.catalog.getAll().pipe(
      tap((skins) => {
        this.skinsSignal.set(skins);
        this.loaded = true;
      })
    );
  }

  private useDefault(): void {
    this.selectedIdSignal.set(DEFAULT_SKIN_ID);
    this.removeStylesheet();
    this.removeStored();
  }

  private applyStylesheet(href: string): void {
    let element = document.getElementById(LINK_ELEMENT_ID) as HTMLLinkElement | null;

    if (!element) {
      element = document.createElement('link');
      element.id = LINK_ELEMENT_ID;
      element.rel = 'stylesheet';
      // A stylesheet removed from the server leaves the default look instead of a broken choice.
      element.addEventListener('error', () => this.useDefault());
    }

    if (element.getAttribute('href') !== href) {
      element.setAttribute('href', href);
    }

    this.keepLast(element);
    this.watchHead();
  }

  private removeStylesheet(): void {
    this.headObserver?.disconnect();
    this.headObserver = null;

    document.getElementById(LINK_ELEMENT_ID)?.remove();
  }

  // The component library adds its styles to <head> while the app runs, so the skin is moved behind them each time;
  // being last, it wins over the app styles of the same specificity.
  private watchHead(): void {
    if (this.headObserver) {
      return;
    }

    this.headObserver = new MutationObserver(() => {
      const element = document.getElementById(LINK_ELEMENT_ID);

      if (element) {
        this.keepLast(element);
      }
    });

    this.headObserver.observe(document.head, { childList: true });
  }

  private keepLast(element: HTMLElement): void {
    if (document.head.lastElementChild !== element) {
      document.head.appendChild(element);
    }
  }

  private readStored(): StoredSkin | null {
    try {
      const raw = localStorage.getItem(STORAGE_KEY);

      if (!raw) {
        return null;
      }

      const parsed = JSON.parse(raw) as Partial<StoredSkin>;

      if (typeof parsed.id !== 'string' || typeof parsed.href !== 'string' || !parsed.href.startsWith(`${SKINS_DIRECTORY}/`)) {
        return null;
      }

      return { id: parsed.id, href: parsed.href };
    } catch {
      return null;
    }
  }

  private writeStored(skin: StoredSkin): void {
    try {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(skin));
    } catch {
      // The skin still applies for this visit when the browser refuses to store it.
    }
  }

  private removeStored(): void {
    try {
      localStorage.removeItem(STORAGE_KEY);
    } catch {
      // Nothing to clean up when the storage is not available.
    }
  }
}
