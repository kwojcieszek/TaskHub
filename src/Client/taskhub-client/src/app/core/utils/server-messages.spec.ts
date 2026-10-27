import { serverMessageKey } from './server-messages';

describe('serverMessageKey', () => {
  it('finds the translation of a known server message', () => {
    expect(serverMessageKey('User is already a member of this project.')).toBe('serverErrors.alreadyProjectMember');
  });

  it('returns nothing for a message that has no translation', () => {
    expect(serverMessageKey('Something the interface does not know.')).toBeNull();
  });

  it('does not treat object members as messages', () => {
    expect(serverMessageKey('constructor')).toBeNull();
  });
});
