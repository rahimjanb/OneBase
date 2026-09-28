export class ApiError extends Error {
  constructor(
    public readonly status: number,
    message: string,
  ) {
    super(message);
  }
}

/** Запрос к backend. Путь относительный (/api/...) — проксируется Next.js в dev и Nginx в Docker. */
export async function api<T>(path: string, init: RequestInit & { token?: string } = {}): Promise<T> {
  const { token, headers, ...rest } = init;
  const response = await fetch(path, {
    ...rest,
    headers: {
      "Content-Type": "application/json",
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
      ...headers,
    },
  });

  if (!response.ok) {
    throw new ApiError(response.status, await response.text());
  }

  return (response.status === 204 ? undefined : await response.json()) as T;
}
