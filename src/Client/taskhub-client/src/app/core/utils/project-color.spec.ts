import { entityColor, projectColor } from './project-color';

describe('project colours', () => {
  it('gives every project the same colour', () => {
    expect(projectColor('a')).toBe(projectColor('b'));
  });

  it('gives an entity the same colour every time', () => {
    const id = '4f0c2f0e-8f0a-4c56-9a53-2d1f4a5b6c7d';

    expect(entityColor(id)).toBe(entityColor(id));
  });

  it('returns a hex colour also for an empty id', () => {
    expect(entityColor('')).toMatch(/^#[0-9a-f]{6}$/);
    expect(entityColor('some-id')).toMatch(/^#[0-9a-f]{6}$/);
  });

  it('spreads different ids over more than one colour', () => {
    const colors = new Set(Array.from({ length: 40 }, (_, index) => entityColor(`user-${index}`)));

    expect(colors.size).toBeGreaterThan(3);
  });
});
