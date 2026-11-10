/**
 * Builds a code generator that continues the existing numbering, e.g. if the highest existing
 * code ends in "003" the next ones are "004", "005", ... Reads the trailing digits of every
 * existing code (regardless of any prefix already used), so it stays a single continuous
 * sequence no matter how past codes were entered.
 */
export function makeSequentialCodeGenerator(existingCodes: string[], pad = 3) {
  const pattern = /(\d+)$/;
  let next = existingCodes.reduce((max, code) => {
    const match = pattern.exec(code.trim());
    if (!match) return max;
    const n = parseInt(match[1], 10);
    return n > max ? n : max;
  }, 0);

  return () => {
    next += 1;
    return String(next).padStart(pad, "0");
  };
}

/**
 * The next code for a form that opens empty: a fixed prefix plus the next number after the highest one already
 * used (whatever prefix those carry), e.g. ["DEP-001", "7"] -> "DEP-008". The user can still type their own.
 */
export function nextPrefixedCode(existingCodes: string[], prefix: string, pad = 3) {
  const next = makeSequentialCodeGenerator(existingCodes, pad)();
  return `${prefix}${next}`;
}

/**
 * Next code for a chart-of-accounts entry. Children continue the parent's numbering at the next level, matching the
 * standard chart: 1000 -> 1100, 1100 -> 1110, 1110 -> 1111. Siblings continue from the highest sibling (1111 -> 1112).
 * A parent with no trailing zero gets "<parent>1". Without a parent it is the next thousand (1000, 2000, ...).
 */
export function suggestAccountCode(parent: { id: string; code: string } | null, all: { code: string; parentAccountId: string | null }[]): string {
  const num = (c: string) => (/^\d+$/.test(c.trim()) ? parseInt(c.trim(), 10) : NaN);
  if (!parent) {
    const roots = all.filter((a) => a.parentAccountId === null).map((a) => num(a.code)).filter((n) => !Number.isNaN(n));
    return String((roots.length ? Math.max(...roots) : 0) + 1000);
  }
  const parentNum = num(parent.code);
  if (Number.isNaN(parentNum)) return "";
  const trailingZeros = /0+$/.exec(parent.code.trim())?.[0].length ?? 0;
  const step = trailingZeros > 0 ? Math.pow(10, trailingZeros - 1) : 0;
  if (step === 0) return `${parent.code.trim()}1`;
  const siblings = all.filter((a) => (a as { parentAccountId: string | null }).parentAccountId === parent.id).map((a) => num(a.code)).filter((n) => !Number.isNaN(n));
  return String(siblings.length ? Math.max(...siblings) + step : parentNum + step);
}
