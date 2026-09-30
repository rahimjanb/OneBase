/** Запрос из браузера к backend через прокси /bff (токен — в httpOnly-cookie). Ошибка — Error с текстом для человека. */
export async function bff<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`/bff/api/${path}`, { ...init, headers: { "Content-Type": "application/json", ...init?.headers } });
  if (!response.ok) {
    const text = await response.text();
    let message = text;
    try {
      const json = JSON.parse(text);
      message = json.error ?? json.detail ?? json.title ?? text;
    } catch {
      // не JSON
    }
    throw new Error(response.status === 403 ? "Недостаточно прав." : message || `Ошибка ${response.status}`);
  }
  return (response.status === 202 || response.status === 204 ? undefined : await response.json()) as T;
}
