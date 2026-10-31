const UPPER = 'ABCDEFGHJKLMNPQRSTUVWXYZ';
const LOWER = 'abcdefghijkmnopqrstuvwxyz';
const DIGITS = '23456789';
const SYMBOLS = '@#$%&*!?';

/** A temporary password that meets the usual policies: 14 characters with upper and lower case, digits and symbols. */
export function temporaryPassword(random: (max: number) => number = secureRandom): string {
  const pick = (set: string) => set[random(set.length)];
  const all = UPPER + LOWER + DIGITS + SYMBOLS;
  const chars = [
    pick(UPPER),
    pick(LOWER),
    pick(DIGITS),
    pick(SYMBOLS),
    ...Array.from({ length: 10 }, () => pick(all)),
  ];
  for (let i = chars.length - 1; i > 0; i--) {
    const j = random(i + 1);
    [chars[i], chars[j]] = [chars[j], chars[i]];
  }
  return chars.join('');
}

function secureRandom(max: number): number {
  const value = new Uint32Array(1);
  crypto.getRandomValues(value);
  return value[0] % max;
}
