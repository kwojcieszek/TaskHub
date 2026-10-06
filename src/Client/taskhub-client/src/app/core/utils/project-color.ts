const ENTITY_COLORS = ['#3b82f6', '#14b8a6', '#f59e0b', '#ec4899', '#8b5cf6', '#06b6d4', '#22c55e', '#f97316'];
const PROJECT_COLOR = '#60a5fa';

/**
 * Colour of a project. Every project shares the same light blue; the id is kept so callers need not change
 * if projects get individual colours again.
 */
export function projectColor(_id: string): string {
  return PROJECT_COLOR;
}

/**
 * Stable colour for any other entity (for example a project member), derived from its id so it stays the same
 * in every view.
 */
export function entityColor(id: string): string {
  let hash = 0;
  for (let i = 0; i < id.length; i++) {
    hash = (hash * 31 + id.charCodeAt(i)) >>> 0;
  }

  return ENTITY_COLORS[hash % ENTITY_COLORS.length];
}
