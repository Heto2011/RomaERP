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
