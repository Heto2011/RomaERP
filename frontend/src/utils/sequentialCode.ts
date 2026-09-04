/** Builds a `PREFIX-000N` code generator that continues from the highest existing number for that prefix. */
export function makeSequentialCodeGenerator(existingCodes: string[], prefix: string, pad = 3) {
  const pattern = new RegExp(`^${prefix}-(\\d+)$`);
  let next = existingCodes.reduce((max, code) => {
    const match = pattern.exec(code);
    if (!match) return max;
    const n = parseInt(match[1], 10);
    return n > max ? n : max;
  }, 0);

  return () => {
    next += 1;
    return `${prefix}-${String(next).padStart(pad, "0")}`;
  };
}
