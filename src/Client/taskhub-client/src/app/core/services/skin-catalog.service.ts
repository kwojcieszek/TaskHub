import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Injectable } from '@angular/core';
import { Observable, catchError, map, of } from 'rxjs';
import { Skin } from '../models/skin.model';
import { SkinManifest, SkinManifestEntry } from '../models/skin-manifest.model';

export const SKINS_DIRECTORY = 'skins';

const MANIFEST_URL = `${SKINS_DIRECTORY}/skins.json`;

// Only a plain file name inside the skins directory, so the manifest cannot point the app at another place.
const STYLESHEET_FILE = /^[A-Za-z0-9_-]+\.css$/;

/**
 * Reads the skins deployed with the web client. A browser cannot list a directory, so the skins/ directory holds
 * skins.json next to the stylesheets, naming each of them; an entry whose file is not a plain .css name is skipped.
 */
@Injectable({ providedIn: 'root' })
export class SkinCatalogService {
  constructor(private readonly http: HttpClient) {}

  getAll(): Observable<Skin[]> {
    // Asked again every time, so a skin uploaded to the server is offered without a new build of the app.
    const headers = new HttpHeaders({ 'Cache-Control': 'no-cache' });

    return this.http.get<SkinManifest>(MANIFEST_URL, { headers }).pipe(
      map((manifest) => this.toSkins(manifest)),
      catchError(() => of([]))
    );
  }

  stylesheetUrl(skin: Skin): string {
    const url = `${SKINS_DIRECTORY}/${skin.file}`;

    return skin.version ? `${url}?v=${encodeURIComponent(skin.version)}` : url;
  }

  private toSkins(manifest: SkinManifest | null): Skin[] {
    const entries = Array.isArray(manifest?.skins) ? manifest.skins : [];
    const skins: Skin[] = [];

    for (const entry of entries) {
      const skin = this.toSkin(entry);

      if (skin && !skins.some((existing) => existing.id === skin.id)) {
        skins.push(skin);
      }
    }

    return skins.sort((a, b) => a.name.localeCompare(b.name));
  }

  private toSkin(entry: SkinManifestEntry | null): Skin | null {
    if (!entry || typeof entry.file !== 'string' || !STYLESHEET_FILE.test(entry.file)) {
      return null;
    }

    const id = entry.file.slice(0, -'.css'.length);
    const name = typeof entry.name === 'string' && entry.name.trim() ? entry.name.trim() : id;
    const description = typeof entry.description === 'string' && entry.description.trim() ? entry.description.trim() : null;
    const version = typeof entry.version === 'string' || typeof entry.version === 'number' ? String(entry.version) : null;

    return { id, name, description, file: entry.file, version };
  }
}
