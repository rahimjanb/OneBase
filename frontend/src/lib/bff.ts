/** Запрос из браузера к backend через прокси /bff (токен — в httpOnly-cookie). Ошибка — Error с текстом для человека. */
export async function bff<T>(path: string, init?: RequestInit): Promise<T> {
  let response: Response;
  try {
    response = await fetch(`/bff/api/${path}`, { ...init, headers: { "Content-Type": "application/json", ...init?.headers } });
  } catch {
    // Ответа нет совсем: связь оборвалась по дороге. Сервер мог успеть выполнить действие — ответ потерялся на обратном пути.
    throw new Error("Нет связи с сервером OneBase — ответ не пришёл. Обновите страницу и проверьте, не выполнилось ли действие, затем повторите.");
  }
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
