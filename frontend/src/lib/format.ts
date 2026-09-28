/** Русское склонение: plural(5, ["файл", "файла", "файлов"]) → "файлов". */
export function plural(n: number, forms: [one: string, few: string, many: string]): string {
  const mod10 = n % 10;
  const mod100 = n % 100;
  if (mod10 === 1 && mod100 !== 11) return forms[0];
  if (mod10 >= 2 && mod10 <= 4 && (mod100 < 12 || mod100 > 14)) return forms[1];
  return forms[2];
}

export const filesLabel = (n: number) => `${n} ${plural(n, ["файл", "файла", "файлов"])}`;
export const foldersLabel = (n: number) => `${n} ${plural(n, ["папка", "папки", "папок"])}`;

export const percent = (value: number, digits = 0) =>
  `${value.toLocaleString("ru-RU", { minimumFractionDigits: digits, maximumFractionDigits: digits })}%`;

export function fileSize(bytes: number): string {
  if (bytes >= 1024 * 1024) return `${(bytes / 1024 / 1024).toFixed(1)} MB`;
  return `${Math.max(1, Math.round(bytes / 1024))} KB`;
}
