/** The content of skins/skins.json as written by the administrator, before it is checked. */
export interface SkinManifest {
  skins?: SkinManifestEntry[];
}

export interface SkinManifestEntry {
  file?: unknown;
  name?: unknown;
  description?: unknown;
  version?: unknown;
}
