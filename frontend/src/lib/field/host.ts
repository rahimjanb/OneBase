/**
 * Домен Sales Base. FIELD_HOST — один домен (по умолчанию sales.1base.uz; nginx список не понимает); sales.localhost — для проверки на своём
 * компьютере (Chrome и Firefox сами направляют *.localhost на 127.0.0.1). Модуль без серверных зависимостей — его импортирует proxy.
 */
const configured = (process.env.FIELD_HOST ?? "sales.1base.uz")
  .split(",")
  .map((h) => h.trim().toLowerCase())
  .filter(Boolean);

export const FIELD_HOSTS = [...new Set([...configured, "sales.localhost"])];

export function isFieldHost(host: string | null | undefined): boolean {
  if (!host) return false;
  const name = host.split(",")[0].trim().split(":")[0].toLowerCase();
  return FIELD_HOSTS.includes(name);
}

/** Хост запроса за nginx: X-Forwarded-Host (его ставит nginx), иначе Host. */
export function requestHost(headers: { get(name: string): string | null }): string | null {
  return headers.get("x-forwarded-host") ?? headers.get("host");
}

/** Страница входа Sales Base: на его домене — «/login» (proxy переписывает в /field/login), в OneBase — /field/login. */
export const FIELD_LOGIN_PATH = "/field/login";

export function fieldLoginPath(host: string | null | undefined): string {
  return isFieldHost(host) ? "/login" : FIELD_LOGIN_PATH;
}

/** Главная Sales Base: на его домене — «/», в OneBase — /field. */
export function fieldHomePath(host: string | null | undefined): string {
  return isFieldHost(host) ? "/" : "/field";
}

/** Выход из Sales Base — с возвратом на его страницу входа. */
export const FIELD_LOGOUT_PATH = "/logout?to=field";

/**
 * Ссылка на страницу OneBase. В OneBase — сам путь; с домена Sales Base — на основной домен (PUBLIC_HOST), потому что
 * там путь переписывается в /field. Основной домен не задан — null: ссылку не показываем. Только для серверного кода.
 */
export function oneBaseHref(host: string | null | undefined, path: string): string | null {
  if (!isFieldHost(host)) return path;
  const main = process.env.PUBLIC_HOST?.split(",")[0].trim();
  return main ? `https://${main}${path}` : null;
}
