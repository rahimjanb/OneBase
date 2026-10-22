"use client";

import { useState } from "react";
import { KeyRound, Loader2, Pencil, UserPlus } from "lucide-react";
import { button, field, primary } from "@/components/ai/form";
import { Note, Section } from "@/components/sales/bits";
import { useT } from "@/i18n/client";
import { bff } from "@/lib/bff";
import type { UserRow, UsersView } from "@/lib/users";

type Form = {
  lastName: string;
  firstName: string;
  login: string;
  position: string;
  roleId: string;
  email: string;
  phone: string;
  password: string;
  isActive: boolean;
};

const empty: Form = { lastName: "", firstName: "", login: "", position: "", roleId: "", email: "", phone: "", password: "", isActive: true };

const formOf = (u: UserRow): Form => ({
  lastName: u.lastName,
  firstName: u.firstName,
  login: u.login,
  position: u.position ?? "",
  roleId: u.roleId ?? "",
  email: u.email ?? "",
  phone: u.phone ?? "",
  password: "",
  isActive: u.isActive,
});

/** Пароль из 12 символов без похожих букв и цифр (l/1, O/0). */
function generatePassword() {
  const alphabet = "abcdefghijkmnpqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ23456789";
  const bytes = crypto.getRandomValues(new Uint32Array(12));
  return Array.from(bytes, (b) => alphabet[b % alphabet.length]).join("");
}

export function UsersSettings({ initial }: { initial: UsersView }) {
  const t = useT();
  const [data, setData] = useState(initial);
  const [editing, setEditing] = useState<UserRow | "new" | null>(null);
  const [form, setForm] = useState<Form>(empty);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [message, setMessage] = useState<string | null>(null);

  const open = (target: UserRow | "new") => {
    setEditing(target);
    setForm(target === "new" ? empty : formOf(target));
    setError(null);
    setMessage(null);
  };

  const set = <K extends keyof Form>(key: K, value: Form[K]) => {
    setForm((f) => ({ ...f, [key]: value }));
    setError(null);
  };

  const save = async () => {
    const isNew = editing === "new";
    if (!form.login.trim() || !form.firstName.trim() || !form.lastName.trim() || !form.position.trim() || !form.roleId) {
      setError(t("Заполните обязательные поля: фамилия, имя, логин, должность и роль."));
      return;
    }
    if (isNew && form.password.length < 8) {
      setError(t("Пароль — не короче 8 символов."));
      return;
    }
    setBusy(true);
    setError(null);
    try {
      const body = JSON.stringify({ ...form, password: form.password || null });
      if (isNew) await bff("users", { method: "POST", body });
      else if (editing) await bff(`users/${editing.id}`, { method: "PUT", body });
      setData(await bff<UsersView>("users"));
      setMessage(isNew ? t("Пользователь {login} создан.", { login: form.login.trim().toLowerCase() }) : t("Изменения сохранены."));
      setEditing(null);
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  };

  const active = data.users.filter((u) => u.isActive).length;
  const me = editing !== "new" && editing?.isMe;

  return (
    <>
      <Section
        title={t("Пользователи")}
        hint={t("{total} · активных {active}", { total: data.users.length, active })}
        actions={
          <button className={primary} onClick={() => open("new")} disabled={busy}>
            <UserPlus className="size-4" />
            {t("Добавить пользователя")}
          </button>
        }
      >
        {message && <p className="mb-3 rounded-lg bg-ok-soft px-3 py-2 text-sm text-ok">{message}</p>}

        {editing && (
          <div className="mb-5 rounded-lg border border-accent/30 bg-accent-soft/30 p-4">
            <h3 className="text-sm font-semibold text-ink">{editing === "new" ? t("Новый пользователь") : t("Изменить: {name}", { name: editing.fullName || editing.login })}</h3>
            <div className="mt-3 grid gap-4 sm:grid-cols-2 xl:grid-cols-3">
              <Field label={t("Фамилия")} required>
                <input className={field} value={form.lastName} onChange={(e) => set("lastName", e.target.value)} maxLength={100} autoComplete="off" />
              </Field>
              <Field label={t("Имя")} required>
                <input className={field} value={form.firstName} onChange={(e) => set("firstName", e.target.value)} maxLength={100} autoComplete="off" />
              </Field>
              <Field label={t("Логин")} required hint={t("Латиница, цифры, точка, дефис; от 3 символов")}>
                <input className={field} value={form.login} onChange={(e) => set("login", e.target.value)} maxLength={64} autoComplete="off" spellCheck={false} />
              </Field>
              <Field label={t("Должность")} required>
                <input className={field} value={form.position} onChange={(e) => set("position", e.target.value)} maxLength={150} autoComplete="off" />
              </Field>
              <Field label={t("Роль")} required hint={me ? t("Свою роль изменить нельзя") : t("Определяет доступ и отдел")}>
                <select className={field} value={form.roleId} onChange={(e) => set("roleId", e.target.value)} disabled={!!me}>
                  <option value="">{t("— выберите роль —")}</option>
                  {data.roles.map((r) => (
                    <option key={r.id} value={r.id} disabled={!r.assignable}>
                      {t(r.name)}
                      {r.assignable ? "" : t(" — только администратор")}
                    </option>
                  ))}
                </select>
              </Field>
              <Field label={t("Почта")}>
                <input className={field} type="email" value={form.email} onChange={(e) => set("email", e.target.value)} maxLength={256} autoComplete="off" />
              </Field>
              <Field label={t("Номер телефона")}>
                <input className={field} type="tel" value={form.phone} onChange={(e) => set("phone", e.target.value)} maxLength={32} placeholder="+998 90 123-45-67" autoComplete="off" />
              </Field>
              <Field
                label={editing === "new" ? t("Пароль") : t("Новый пароль")}
                required={editing === "new"}
                hint={editing === "new" ? t("Не короче 8 символов — передайте сотруднику") : t("Пусто — пароль не меняется")}
              >
                <div className="flex gap-2">
                  <input className={field} value={form.password} onChange={(e) => set("password", e.target.value)} autoComplete="new-password" spellCheck={false} />
                  <button type="button" className={button} onClick={() => set("password", generatePassword())} title={t("Сгенерировать пароль")}>
                    <KeyRound className="size-4" />
                  </button>
                </div>
              </Field>
              {editing !== "new" && (
                <label className="flex items-center gap-2.5 self-end pb-2 text-sm text-ink">
                  <input type="checkbox" checked={form.isActive} onChange={(e) => set("isActive", e.target.checked)} disabled={!!me} className="size-4 accent-[var(--color-accent)]" />
                  {t("Активен — может входить")}
                </label>
              )}
            </div>
            {error && <p className="mt-3 rounded-lg bg-bad-soft px-3 py-2 text-sm text-bad">{error}</p>}
            <div className="mt-4 flex gap-2">
              <button className={primary} onClick={save} disabled={busy}>
                {busy && <Loader2 className="size-4 animate-spin" />}
                {editing === "new" ? t("Создать") : t("Сохранить")}
              </button>
              <button className={button} onClick={() => setEditing(null)} disabled={busy}>
                {t("Отмена")}
              </button>
            </div>
          </div>
        )}

        <div className="overflow-x-auto">
          <table className="w-full min-w-[820px] text-sm">
            <thead>
              <tr className="border-b border-line text-left text-[11px] uppercase tracking-wide text-ink-3">
                <th className="py-2 pr-3 font-semibold">{t("Сотрудник")}</th>
                <th className="py-2 pr-3 font-semibold">{t("Должность")}</th>
                <th className="py-2 pr-3 font-semibold">{t("Роль")}</th>
                <th className="py-2 pr-3 font-semibold">{t("Контакты")}</th>
                <th className="py-2 pr-3 font-semibold">{t("Статус")}</th>
                <th className="py-2" />
              </tr>
            </thead>
            <tbody>
              {data.users.map((u) => (
                <tr key={u.id} className={`border-b border-line last:border-b-0 ${u.isActive ? "" : "opacity-60"}`}>
                  <td className="py-2 pr-3">
                    <span className="block font-medium text-ink">
                      {u.fullName || "—"}
                      {u.isMe && <span className="ml-2 rounded-full bg-muted px-1.5 py-0.5 text-[10px] font-medium text-ink-2">{t("это вы")}</span>}
                    </span>
                    <span className="text-xs text-ink-3">{u.login}</span>
                  </td>
                  <td className="py-2 pr-3 text-ink-2">{u.position ?? "—"}</td>
                  <td className="py-2 pr-3 text-ink">{u.roleName ? u.roleName.split(", ").map((r) => t(r)).join(", ") : <span className="text-warn">{t("без роли")}</span>}</td>
                  <td className="py-2 pr-3 text-xs text-ink-2">
                    {u.email && <span className="block">{u.email}</span>}
                    {u.phone && <span className="block">{u.phone}</span>}
                    {!u.email && !u.phone && "—"}
                  </td>
                  <td className="py-2 pr-3">{u.isActive ? <span className="text-ok">{t("активен")}</span> : <span className="text-ink-3">{t("отключён")}</span>}</td>
                  <td className="py-2 text-right">
                    {u.canEdit ? (
                      <button className={button} onClick={() => open(u)} disabled={busy}>
                        <Pencil className="size-3.5" />
                        {t("Изменить")}
                      </button>
                    ) : (
                      <span className="text-xs text-ink-3">{t("только администратор")}</span>
                    )}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        <Note>
          {t(
            "Удаления нет: отключённый пользователь не может войти, а его история в OneBase сохраняется. Роль, отключение и новый пароль действуют сразу. Назначить роль можно, только если у вас есть все её права, — поэтому роль «Администратор» назначает и администраторов меняет только администратор.",
          )}
        </Note>
      </Section>
    </>
  );
}

function Field({ label, required, hint, children }: { label: string; required?: boolean; hint?: string; children: React.ReactNode }) {
  return (
    <label className="block">
      <span className="mb-1.5 block text-sm font-medium text-ink">
        {label}
        {required && <span className="ml-0.5 text-bad">*</span>}
      </span>
      {children}
      {hint && <span className="mt-1 block text-xs text-ink-3">{hint}</span>}
    </label>
  );
}
