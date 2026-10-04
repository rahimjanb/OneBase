"use client";

import { useActionState } from "react";
import { login, type LoginState } from "./actions";

const field =
  "h-11 w-full rounded-lg border border-line bg-surface px-3.5 text-sm text-ink placeholder:text-ink-3 focus:border-accent focus:outline-none focus:ring-2 focus:ring-accent/20";

/** Форма входа OneBase; product="field" — вход Sales Base (свой заголовок и главная Sales Base по умолчанию). */
export function LoginForm({ next, product, title = "Вход", hint }: { next: string; product?: "field"; title?: string; hint?: string }) {
  const [state, action, pending] = useActionState<LoginState, FormData>(login, {});

  return (
    <form action={action} className="rounded-xl border border-line bg-surface p-6">
      <h1 className="text-lg font-semibold text-ink">{title}</h1>
      {hint && <p className="mt-1 text-sm text-ink-3">{hint}</p>}
      <input type="hidden" name="next" value={next} />
      {product && <input type="hidden" name="product" value={product} />}
      <label className="mt-5 block">
        <span className="mb-1.5 block text-sm text-ink-2">Логин или почта</span>
        <input name="login" type="text" required autoComplete="username" className={field} />
      </label>
      <label className="mt-4 block">
        <span className="mb-1.5 block text-sm text-ink-2">Пароль</span>
        <input name="password" type="password" required autoComplete="current-password" className={field} />
      </label>
      {state.error && <p className="mt-4 rounded-lg bg-bad-soft px-3 py-2 text-sm text-bad">{state.error}</p>}
      <button
        type="submit"
        disabled={pending}
        className="mt-5 h-11 w-full rounded-lg bg-accent text-sm font-semibold text-white hover:bg-accent-strong disabled:opacity-60"
      >
        {pending ? "Входим…" : "Войти"}
      </button>
    </form>
  );
}
