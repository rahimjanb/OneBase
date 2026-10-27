"use client";

import { useEffect, useMemo, useState } from "react";
import {
  Bot,
  Check,
  ClipboardList,
  Copy,
  DatabaseZap,
  FolderOpen,
  KeyRound,
  Landmark,
  Loader2,
  MapPinned,
  RotateCcw,
  Send,
  ShieldCheck,
  Sparkles,
  TrendingUp,
  UserRound,
  UsersRound,
  type LucideIcon,
} from "lucide-react";
import { button, field, primary } from "@/components/ai/form";
import { Sheet } from "@/components/field/Sheet";
import { useT } from "@/i18n/client";
import { bff } from "@/lib/bff";
import { sectionPermissions, type UserRow, type UsersView } from "@/lib/users";

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
  sections: string[];
};

const icons: Record<string, LucideIcon> = {
  sales: TrendingUp,
  finance: Landmark,
  hr: UsersRound,
  tasks: ClipboardList,
  files: FolderOpen,
  consultant: Sparkles,
  field: MapPinned,
  data: DatabaseZap,
  campaigns: Send,
};

/** По каким данным отвечает AI, если раздел отмечен (раздела нет — AI-сотрудник и инструменты этих данных молчат). */
const aiCoverage: Record<string, string> = {
  sales: "продажи, ассортимент и клиенты — Sales AI, а также маркетинг, производство и снабжение по данным продаж",
  finance: "оплаты, P&L и долги — Finance AI",
  hr: "сотрудники и посещаемость — HR AI",
  tasks: "задачи отделов и просрочки",
  files: "документы базы знаний",
  field: "полевая команда Sales Base",
};

/** Пароль из 12 символов без похожих букв и цифр (l/1, O/0). */
function generatePassword() {
  const alphabet = "abcdefghijkmnpqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ23456789";
  const bytes = crypto.getRandomValues(new Uint32Array(12));
  return Array.from(bytes, (b) => alphabet[b % alphabet.length]).join("");
}

const sameSet = (a: readonly string[], b: readonly string[]) => a.length === b.length && a.every((x) => b.includes(x));

/**
 * Окно «Новый пользователь» / «Изменить пользователя»: слева профиль и вход, справа доступ — роль (права на управление и настройки,
 * отдел) и разделы галочками. Роль подставляет свои разделы, их можно изменить; итоговые права считает сервер (UserSections) — по ним
 * открываются разделы и отвечают AI-сотрудники. Выдать можно только раздел, к которому есть доступ у вас.
 */
export function UserDialog({
  target,
  data,
  onClose,
  onSaved,
}: {
  target: UserRow | "new" | null;
  data: UsersView;
  onClose: () => void;
  onSaved: (message: string, view: UsersView) => void;
}) {
  const t = useT();
  const isNew = target === "new";
  const user = target && target !== "new" ? target : null;
  const isMe = !!user?.isMe;
  const [form, setForm] = useState<Form | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [copied, setCopied] = useState(false);

  useEffect(() => {
    if (!target) return;
    setError(null);
    setCopied(false);
    setForm(
      target === "new"
        ? { lastName: "", firstName: "", login: "", position: "", roleId: "", email: "", phone: "", password: "", isActive: true, sections: [] }
        : {
            lastName: target.lastName,
            firstName: target.firstName,
            login: target.login,
            position: target.position ?? "",
            roleId: target.roleId ?? "",
            email: target.email ?? "",
            phone: target.phone ?? "",
            password: "",
            isActive: target.isActive,
            sections: target.sections,
          },
    );
  }, [target]);

  const role = data.roles.find((r) => r.id === form?.roleId) ?? null;
  const roleSections = role?.sections ?? [];
  const asRole = !!form && !!role && sameSet(form.sections, roleSections);
  const extraRights = useMemo(() => (role?.permissions ?? []).filter((p) => !sectionPermissions.has(p.code)), [role]);

  if (!target || !form) return null;

  const set = <K extends keyof Form>(key: K, value: Form[K]) => {
    setForm((f) => (f ? { ...f, [key]: value } : f));
    setError(null);
  };

  const pickRole = (roleId: string) => {
    const next = data.roles.find((r) => r.id === roleId);
    // Новая роль — её разделы: сотрудник продаж получает «Продажи», «Задачи», «Файлы», консультанта; дальше галочки меняются вручную.
    setForm((f) => (f ? { ...f, roleId, sections: next?.sections ?? [] } : f));
    setError(null);
  };

  const toggle = (key: string) =>
    set("sections", form.sections.includes(key) ? form.sections.filter((k) => k !== key) : [...form.sections, key]);

  const copyPassword = async () => {
    try {
      await navigator.clipboard.writeText(form.password);
      setCopied(true);
      setTimeout(() => setCopied(false), 1500);
    } catch {
      // буфер обмена недоступен (http) — пароль виден в поле
    }
  };

  const save = async () => {
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
      else if (user) await bff(`users/${user.id}`, { method: "PUT", body });
      const view = await bff<UsersView>("users");
      onSaved(isNew ? t("Пользователь {login} создан.", { login: form.login.trim().toLowerCase() }) : t("Изменения сохранены."), view);
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  };

  const dataSections = data.sections.filter((s) => aiCoverage[s.key]);
  const answered = dataSections.filter((s) => form.sections.includes(s.key));
  const silent = dataSections.filter((s) => !form.sections.includes(s.key));
  const consultant = form.sections.includes("consultant");

  return (
    <Sheet
      open
      onClose={busy ? () => {} : onClose}
      size="xl"
      title={isNew ? t("Новый пользователь") : t("Изменить пользователя")}
      subtitle={user ? `${user.fullName || user.login} · ${user.login}` : t("Профиль, роль и разделы, к которым будет доступ")}
      footer={
        <div className="flex flex-col-reverse gap-2 sm:flex-row sm:items-center">
          <p className={`min-h-5 flex-1 text-sm ${error ? "text-bad" : "hidden text-ink-3 sm:block"}`} role={error ? "alert" : undefined}>
            {error ?? (isNew ? t("Пароль передайте сотруднику — после входа он сможет работать сразу.") : t("Роль, разделы и пароль действуют сразу."))}
          </p>
          <div className="flex gap-2">
            <button type="button" className={`${button} flex-1 justify-center sm:flex-none`} onClick={onClose} disabled={busy}>
              {t("Отмена")}
            </button>
            <button type="button" className={`${primary} flex-1 justify-center sm:flex-none`} onClick={save} disabled={busy}>
              {busy && <Loader2 className="size-4 animate-spin" />}
              {isNew ? t("Создать пользователя") : t("Сохранить")}
            </button>
          </div>
        </div>
      }
    >
      <form
        className="grid gap-6 pb-1 lg:grid-cols-[minmax(0,0.8fr)_minmax(0,1.45fr)] lg:gap-8"
        onSubmit={(e) => {
          e.preventDefault();
          void save();
        }}
      >
        <section aria-labelledby="user-profile" className="space-y-4">
          <Heading id="user-profile" icon={UserRound} title={t("Профиль")} />
          <div className="grid gap-3 sm:grid-cols-2">
            <Field label={t("Фамилия")} required>
              <input className={field} value={form.lastName} onChange={(e) => set("lastName", e.target.value)} maxLength={100} autoComplete="off" autoFocus={isNew} />
            </Field>
            <Field label={t("Имя")} required>
              <input className={field} value={form.firstName} onChange={(e) => set("firstName", e.target.value)} maxLength={100} autoComplete="off" />
            </Field>
            <Field label={t("Логин")} required hint={t("Латиница, цифры, точка, дефис; от 3 символов")}>
              <input className={field} value={form.login} onChange={(e) => set("login", e.target.value)} maxLength={64} autoComplete="off" spellCheck={false} autoCapitalize="none" />
            </Field>
            <Field label={t("Должность")} required>
              <input className={field} value={form.position} onChange={(e) => set("position", e.target.value)} maxLength={150} autoComplete="off" />
            </Field>
            <Field label={t("Почта")}>
              <input className={field} type="email" value={form.email} onChange={(e) => set("email", e.target.value)} maxLength={256} autoComplete="off" />
            </Field>
            <Field label={t("Номер телефона")}>
              <input className={field} type="tel" value={form.phone} onChange={(e) => set("phone", e.target.value)} maxLength={32} placeholder="+998 90 123-45-67" autoComplete="off" />
            </Field>
          </div>
          <Field
            label={isNew ? t("Пароль") : t("Новый пароль")}
            required={isNew}
            hint={isNew ? t("Не короче 8 символов — передайте сотруднику") : t("Пусто — пароль не меняется; новый закроет входы на других устройствах")}
          >
            <div className="flex gap-2">
              <input className={`${field} font-mono`} value={form.password} onChange={(e) => set("password", e.target.value)} autoComplete="new-password" spellCheck={false} autoCapitalize="none" />
              <button type="button" className={button} onClick={() => set("password", generatePassword())} title={t("Сгенерировать пароль")} aria-label={t("Сгенерировать пароль")}>
                <KeyRound className="size-4" />
              </button>
              {form.password && (
                <button type="button" className={button} onClick={copyPassword} title={t("Скопировать пароль")} aria-label={t("Скопировать пароль")}>
                  {copied ? <Check className="size-4 text-ok" /> : <Copy className="size-4" />}
                </button>
              )}
            </div>
          </Field>
          {!isNew && (
            <label className={`flex items-start gap-3 rounded-xl border border-line p-3 ${isMe ? "opacity-60" : "cursor-pointer hover:bg-muted/50"}`}>
              <input type="checkbox" checked={form.isActive} onChange={(e) => set("isActive", e.target.checked)} disabled={isMe} className="mt-0.5 size-4 accent-[var(--color-accent)]" />
              <span>
                <span className="block text-sm font-medium text-ink">{t("Активен — может входить")}</span>
                <span className="block text-xs text-ink-3">
                  {isMe ? t("Себя отключить нельзя.") : t("Отключённый не входит в OneBase и Telegram-агент; история сохраняется.")}
                </span>
              </span>
            </label>
          )}
        </section>

        <section aria-labelledby="user-access" className="space-y-4">
          <Heading id="user-access" icon={ShieldCheck} title={t("Доступ")} />
          <Field label={t("Роль")} required hint={isMe ? t("Свои роль и разделы меняет другой администратор") : undefined}>
            <select className={field} value={form.roleId} onChange={(e) => pickRole(e.target.value)} disabled={isMe}>
              <option value="">{t("— выберите роль —")}</option>
              {data.roles.map((r) => (
                <option key={r.id} value={r.id} disabled={!r.assignable}>
                  {t(r.name)}
                  {r.assignable ? "" : t(" — только администратор")}
                </option>
              ))}
            </select>
          </Field>
          {role && (
            <div className="rounded-xl bg-muted/60 px-3 py-2.5 text-xs text-ink-2">
              {role.description && <p>{t(role.description)}</p>}
              {role.department && <p className="mt-1 text-ink-3">{t("Отдел: {name}", { name: t(role.department) })}</p>}
              {extraRights.length > 0 && (
                <div className="mt-2 flex flex-wrap gap-1.5">
                  <span className="text-ink-3">{t("Роль также даёт:")}</span>
                  {extraRights.map((p) => (
                    <span key={p.code} className="rounded-md bg-surface px-1.5 py-0.5 text-[11px] font-medium text-ink-2 ring-1 ring-line">
                      {t(p.label)}
                    </span>
                  ))}
                </div>
              )}
            </div>
          )}

          <div>
            <div className="mb-2 flex flex-wrap items-center gap-2">
              <span className="text-sm font-medium text-ink">{t("Разделы")}</span>
              {role &&
                (asRole ? (
                  <span className="rounded-full bg-muted px-2 py-0.5 text-[11px] font-medium text-ink-3">{t("как у роли")}</span>
                ) : (
                  <>
                    <span className="rounded-full bg-accent-soft px-2 py-0.5 text-[11px] font-semibold text-accent-strong">{t("настроены вручную")}</span>
                    {!isMe && (
                      <button type="button" onClick={() => set("sections", roleSections)} className="ml-auto inline-flex items-center gap-1 text-xs font-medium text-accent-strong hover:underline">
                        <RotateCcw className="size-3.5" />
                        {t("Как у роли")}
                      </button>
                    )}
                  </>
                ))}
            </div>
            <div className="grid gap-2 sm:grid-cols-2" role="group" aria-label={t("Разделы")}>
              {data.sections.map((s) => {
                const on = form.sections.includes(s.key);
                const locked = isMe || (!on && !s.grantable);
                const Icon = icons[s.key] ?? Check;
                return (
                  <label
                    key={s.key}
                    title={!isMe && !on && !s.grantable ? t("У вас нет доступа к этому разделу — выдать его может администратор") : undefined}
                    className={`relative flex gap-3 rounded-xl border p-3 transition-colors ${
                      on ? "border-accent/40 bg-accent/10" : "border-line bg-surface"
                    } ${locked ? "cursor-not-allowed opacity-55" : "cursor-pointer hover:border-accent/40"}`}
                  >
                    <input type="checkbox" className="peer sr-only" checked={on} disabled={locked} onChange={() => toggle(s.key)} />
                    <span className={`grid size-9 shrink-0 place-items-center rounded-lg transition-colors ${on ? "bg-accent text-white" : "bg-muted text-ink-3"}`} aria-hidden>
                      <Icon className="size-[18px]" />
                    </span>
                    <span className="min-w-0 flex-1">
                      <span className="block text-sm font-semibold text-ink">{t(s.title)}</span>
                      <span className="mt-0.5 block text-xs leading-snug text-ink-3">{t(s.description)}</span>
                    </span>
                    <span
                      aria-hidden
                      className={`mt-0.5 grid size-5 shrink-0 place-items-center rounded-md border transition-colors peer-focus-visible:ring-2 peer-focus-visible:ring-accent ${
                        on ? "border-accent bg-accent text-white" : "border-line-strong bg-surface"
                      }`}
                    >
                      {on && <Check className="size-3.5" strokeWidth={3} />}
                    </span>
                  </label>
                );
              })}
            </div>
          </div>

          <div className={`flex gap-3 rounded-xl border p-3 text-xs ${consultant ? "border-line bg-muted/40" : "border-warn/30 bg-warn/10"}`}>
            <Bot className={`mt-0.5 size-4 shrink-0 ${consultant ? "text-accent" : "text-warn"}`} aria-hidden />
            <div className="space-y-1 text-ink-2">
              {consultant ? (
                <>
                  <p className="font-medium text-ink">{t("AI-сотрудники отвечают только по отмеченным разделам")}</p>
                  {answered.length > 0 ? (
                    <p>{t("Ответят: {list}.", { list: answered.map((s) => t(aiCoverage[s.key])).join("; ") })}</p>
                  ) : (
                    <p>{t("Разделов с данными не отмечено — консультант ответит только на общие вопросы.")}</p>
                  )}
                  {silent.length > 0 && <p className="text-ink-3">{t("Не ответят по: {list}.", { list: silent.map((s) => t(s.title)).join(", ") })}</p>}
                </>
              ) : (
                <p className="font-medium text-ink">{t("AI-консультант не отмечен — чат с ИИ и Telegram-агент от имени сотрудника ему недоступны.")}</p>
              )}
            </div>
          </div>
        </section>
        <button type="submit" hidden aria-hidden tabIndex={-1} />
      </form>
    </Sheet>
  );
}

function Heading({ id, icon: Icon, title }: { id: string; icon: LucideIcon; title: string }) {
  return (
    <h3 id={id} className="flex items-center gap-2 text-[13px] font-semibold uppercase tracking-wide text-ink-3">
      <Icon className="size-4" aria-hidden />
      {title}
    </h3>
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
