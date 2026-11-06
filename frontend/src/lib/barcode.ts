/**
 * Сканер штрихкода камерой без React: форматы, перевод ошибок камеры в понятный текст, подтверждение кода (линейный — двумя одинаковыми
 * чтениями подряд, QR и DataMatrix — сразу), очистка кода. Проверяется тестами (barcode.test.mjs).
 */

/** Что читаем: товарные коды (EAN, UPC), складские (Code 128, Code 39, ITF), QR и DataMatrix. */
export const scanFormats = ["ean_13", "ean_8", "upc_a", "upc_e", "code_128", "code_39", "code_93", "itf", "codabar", "qr_code", "data_matrix"] as const;

/** Двумерные коды защищены от ошибок чтения сами — их не нужно читать дважды. */
const matrix = new Set(["qr_code", "data_matrix", "aztec", "pdf417"]);

/**
 * Подтверждение кода: линейный штрихкод камера иногда читает с ошибкой в одной полосе — принимаем код, когда он прочитан дважды подряд
 * одинаково; QR и DataMatrix — с первого раза. Пустой кадр между чтениями не сбрасывает счёт.
 */
export function createConfirmer(times = 2) {
  let last = "";
  let count = 0;
  return (code: string, format: string): string | null => {
    if (!code) return null;
    if (matrix.has(format)) return code;
    count = code === last ? count + 1 : 1;
    last = code;
    return count >= times ? code : null;
  };
}

/** Код для поиска: без служебных символов (разделитель GS1 и прочие управляющие), пробелов по краям; пустой — null. */
export function cleanCode(raw: string): string | null {
  // eslint-disable-next-line no-control-regex
  const code = raw.replace(/[\u0000-\u001f\u007f]/g, "").trim();
  return code.length > 0 && code.length <= 64 ? code : null;
}

/**
 * Почему камера не включилась — текст для человека (ключ перевода). Ошибки getUserMedia — DOMException с именем: нет разрешения, нет
 * камеры, камера занята; secure = false — адрес не https (браузер камеру не даёт).
 */
export function cameraProblem(error: unknown, secure = true): string {
  if (!secure) return "Камера работает только на защищённом адресе (https). Откройте OneBase по https.";
  const name = error && typeof error === "object" && "name" in error ? String((error as { name: unknown }).name) : "";
  switch (name) {
    case "NotAllowedError":
    case "SecurityError":
      return "Нет доступа к камере. Разрешите камеру для этого сайта в настройках браузера и попробуйте снова.";
    case "NotFoundError":
    case "OverconstrainedError":
      return "Камера не найдена.";
    case "NotReadableError":
    case "AbortError":
      return "Камера занята другим приложением. Закройте его и попробуйте снова.";
    default:
      return "Камеру не удалось включить.";
  }
}
