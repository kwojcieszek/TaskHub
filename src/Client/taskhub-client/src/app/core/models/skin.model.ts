/** A skin listed in skins/skins.json; its id is the name of its stylesheet without the .css extension. */
export interface Skin {
  id: string;
  name: string;
  description: string | null;
  file: string;
  version: string | null;
}
