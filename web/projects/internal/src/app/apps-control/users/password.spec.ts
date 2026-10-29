import { temporaryPassword } from './password';

describe('temporaryPassword', () => {
  it('has 14 characters of every kind', () => {
    for (let i = 0; i < 20; i++) {
      const p = temporaryPassword();
      expect(p).toHaveLength(14);
      expect(p).toMatch(/[A-Z]/);
      expect(p).toMatch(/[a-z]/);
      expect(p).toMatch(/[0-9]/);
      expect(p).toMatch(/[@#$%&*!?]/);
    }
  });
});
