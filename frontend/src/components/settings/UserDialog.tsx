"use client";

import { useEffect, useMemo, useState } from "react";
import {
  Bot,
  Check,
  ClipboardList,
  Copy,
  DatabaseZap,
  Factory,
  Fingerprint,
  FolderOpen,
  KeyRound,
  Landmark,
  Loader2,
  MapPinned,
  RotateCcw,
  Send,
  Settings,
  ShieldCheck,
  Sparkles,
  TrendingUp,
  UserRound,
  UsersRound,
  Warehouse,
  X,
  type LucideIcon,
} from "lucide-react";
import { button, field, primary } from "@/components/ai/form";
import { Sheet } from "@/components/field/Sheet";
import { useT } from "@/i18n/client";
import { bff } from "@/lib/bff";
import { SalesRegionsPicker, isSalesRegionRole } from "./SalesRegionsPicker";
import { rightsWithSections, sectionPermissions, titleCase, toggleRight, type HrCandidate, type UserRow, type UsersView } from "@/lib/users";

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
  /** Права в разделах отделов и настройках (UserRights). */
  rights: string[];
  /** Отдел для роли без своего отдела («Сотрудник», директор). */
  departmentId: string;
  /** Сотрудник Verifix: по нему «Мой день» показывает рабочее время. */
  hrEmployee: { id: string; name: string } | null;
  /** «Без привязки к Verifix» — пользователь не связан с сотрудником (подрядчик, служебная учётная запись). */
  noVerifix: boolean;
};

/** Значки групп прав: снабжение и склад, производство, настройки. */
const groupIcons: Record<string, LucideIcon> = { supply: Warehouse, production: Factory, settings: Settings };

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
 * Окно «Новый пользователь» / «Изменить пользователя»: слева профиль и вход, справа доступ — роль (отдел), разделы галочками и права в
 * разделах отделов и настройках галочками по группам (снабжение и склад, производство, настройки). Роль подставляет свои разделы и права,
 * их можно изменить; итоговые права считает сервер (UserSections, UserRights) — по ним открываются меню, страницы и действия, отвечают
 * AI-сотрудники и приходят уведомления. Выдать можно только то, что есть у вас; право, которому нужно другое, включает его, снятое — снимает
 * зависящие от него.
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
  /** Регионы РМ / супервайзера продаж (null — ещё не загружены); уходят вместе с пользователем. */
  const [salesRegions, setSalesRegions] = useState<string[] | null>(null);

  useEffect(() => {
    if (!target) return;
    setError(null);
    setCopied(false);
    setSalesRegions(null);
    setForm(
      target === "new"
        ? {
            lastName: "",
            firstName: "",
            login: "",
            position: "",
            roleId: "",
            email: "",
            phone: "",
            password: "",
            isActive: true,
            sections: [],
            rights: [],
            departmentId: "",
            hrEmployee: null,
            noVerifix: false,
          }
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
            rights: target.rights ?? [],
            departmentId: target.departmentId ?? "",
            hrEmployee: target.hrEmployee ?? null,
            noVerifix: !target.hrEmployee,
          },
    );
  }, [target]);

  const role = data.roles.find((r) => r.id === form?.roleId) ?? null;
  const roleSections = role?.sections ?? [];
  const asRole = !!form && !!role && sameSet(form.sections, roleSections);
  const catalog = useMemo(() => data.rights ?? [], [data.rights]);
  const roleRights = role?.rights ?? [];
  const asRoleRights = !!form && !!role && sameSet(form.rights, roleRights);
  const extraRights = useMemo(
    () => (role?.permissions ?? []).filter((p) => !sectionPermissions.has(p.code) && !catalog.some((r) => r.key === p.code)),
    [role, catalog],
  );

  if (!target || !form) return null;

  const set = <K extends keyof Form>(key: K, value: Form[K]) => {
    setForm((f) => (f ? { ...f, [key]: value } : f));
    setError(null);
  };

  const pickRole = (roleId: string) => {
    const next = data.roles.find((r) => r.id === roleId);
    // Новая роль — её разделы и права: сотрудник продаж получает «Продажи», «Задачи», «Файлы», консультанта, кладовщик — склад и свои
    // заявки; дальше галочки меняются вручную.
    setForm((f) => (f ? { ...f, roleId, sections: next?.sections ?? [], rights: next?.rights ?? [] } : f));
    setError(null);
  };

  const toggle = (key: string) => {
    const sections = form.sections.includes(key) ? form.sections.filter((k) => k !== key) : [...form.sections, key];
    // Снятый раздел снимает и права, которым он нужен (настройки продаж без «Продаж»).
    setForm((f) => (f ? { ...f, sections, rights: rightsWithSections(f.rights, sections, catalog) } : f));
    setError(null);
  };

  const toggleRightKey = (key: string, on: boolean) => set("rights", toggleRight(form.rights, key, on, catalog));

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
    if (isNew && data.verifixAvailable && !form.hrEmployee && !form.noVerifix) {
      setError(t("Выберите сотрудника в Verifix или отметьте «Без привязки к Verifix»."));
      return;
    }
    if (role && isSalesRegionRole(role.name) && !isMe && (salesRegions ?? []).length === 0) {
      setError(t(role.name === "РМ" ? "Выберите регионы РМ." : "Выберите регионы супервайзера."));
      return;
    }
    if (role?.isEmployee && !form.departmentId) {
      setError(t("Выберите отдел сотрудника."));
      return;
    }
    setBusy(true);
    setError(null);
    try {
      const { hrEmployee, departmentId, ...rest } = form;
      const body = JSON.stringify({
        ...rest,
        password: form.password || null,
        departmentId: role?.departmentId ? null : departmentId || null,
        hrEmployeeId: form.noVerifix ? null : (hrEmployee?.id ?? null),
        // РМ / супервайзер продаж — его регионы; свои не меняются (null — сервер не трогает охват). Другим ролям не нужны.
        salesRegionIds: role && isSalesRegionRole(role.name) && !isMe ? (salesRegions ?? []) : null,
      });
      if (isNew) await bff("users", { method: "POST", body });
      else if (user) await bff(`users/${user.id}`, { method: "PUT", body });
      // Охват продаж — отдельным запросом (проверка, аудит, действует сразу); у нового пользователя — после создания, при следующем открытии.
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
      subtitle={user ? `${user.fullName || user.login} · ${user.login}` : t("Профиль, роль, разделы и права в них")}
      footer={
        <div className="flex flex-col-reverse gap-2 sm:flex-row sm:items-center">
          <p className={`min-h-5 flex-1 text-sm ${error ? "text-bad" : "hidden text-ink-3 sm:block"}`} role={error ? "alert" : undefined}>
            {error ?? (isNew ? t("Пароль передайте сотруднику — после входа он сможет работать сразу.") : t("Роль, разделы, права и пароль действуют сразу."))}
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
          {data.verifixAvailable && (
            <VerifixLink
              value={form.hrEmployee}
              off={form.noVerifix}
              userId={user?.id ?? null}
              onPick={(c) =>
                setForm((f) =>
                  f
                    ? {
                        ...f,
                        hrEmployee: c ? { id: c.id, name: c.name } : null,
                        // Пустые поля профиля — из Verifix: имя, фамилия, телефон, должность и отдел подразделения.
                        ...(c
                          ? {
                              lastName: f.lastName || titleCase(c.lastName),
                              firstName: f.firstName || titleCase(c.firstName),
                              phone: f.phone || (c.phone ?? ""),
                              position: f.position || (c.job ?? ""),
                              departmentId: f.departmentId || (c.departmentId ?? ""),
                            }
                          : {}),
                      }
                    : f,
                )
              }
              onOff={(off) => setForm((f) => (f ? { ...f, noVerifix: off, hrEmployee: off ? null : f.hrEmployee } : f))}
            />
          )}
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
          <Field label={t("Роль")} required hint={isMe ? t("Свои роль, разделы и права меняет другой администратор") : undefined}>
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
          {/* Роль «РМ» или «Супервайзер продаж» — сразу его регионы (обязательно): по ним «Продажи» показывают данные. */}
          {role && isSalesRegionRole(role.name) && !isMe && (
            <SalesRegionsPicker key={user?.id ?? "new"} roleName={role.name} userId={user?.id ?? null} value={salesRegions} onChange={setSalesRegions} />
          )}
          {role && !role.departmentId && (data.departments?.length ?? 0) > 0 && (
            <Field label={t("Отдел")} required={role.isEmployee} hint={role.isEmployee ? t("По отделу сотрудник видит задачи и файлы отдела") : undefined}>
              <select className={field} value={form.departmentId} onChange={(e) => set("departmentId", e.target.value)} disabled={isMe}>
                <option value="">{role.isEmployee ? t("— выберите отдел —") : t("Без отдела")}</option>
                {data.departments!.map((d) => (
                  <option key={d.id} value={d.id}>
                    {t(d.name)}
                  </option>
                ))}
              </select>
            </Field>
          )}
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
              {/* Разделы, которых у пользователя нет и которые редактор выдать не может, не показываются (не «нет доступа»). */}
              {data.sections.filter((s) => form.sections.includes(s.key) || (!isMe && s.grantable)).map((s) => {
                const on = form.sections.includes(s.key);
                const locked = isMe || (!on && !s.grantable);
                const Icon = icons[s.key] ?? Check;
                return (
                  <label
                    key={s.key}
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

          {catalog.length > 0 && (
            <div>
              <div className="mb-2 flex flex-wrap items-center gap-2">
                <span className="text-sm font-medium text-ink">{t("Действия в отделах и настройки")}</span>
                {role &&
                  (asRoleRights ? (
                    <span className="rounded-full bg-muted px-2 py-0.5 text-[11px] font-medium text-ink-3">{t("как у роли")}</span>
                  ) : (
                    <>
                      <span className="rounded-full bg-accent-soft px-2 py-0.5 text-[11px] font-semibold text-accent-strong">{t("настроены вручную")}</span>
                      {!isMe && (
                        <button
                          type="button"
                          onClick={() => set("rights", rightsWithSections(roleRights, form.sections, catalog))}
                          className="ml-auto inline-flex items-center gap-1 text-xs font-medium text-accent-strong hover:underline"
                        >
                          <RotateCcw className="size-3.5" />
                          {t("Как у роли")}
                        </button>
                      )}
                    </>
                  ))}
              </div>
              <p className="mb-2 text-xs text-ink-3">
                {t("Отмеченное сотрудник видит в меню и может делать: принимать на склад, проверять качество, вести закупки и задания, открывать страницы настроек.")}
              </p>
              <div className="space-y-3">
                {(data.rightGroups ?? []).map((g) => {
                  const items = catalog.filter((r) => r.group === g.key);
                  if (items.length === 0) return null;
                  const GroupIcon = groupIcons[g.key] ?? Check;
                  return (
                    <fieldset key={g.key} className="rounded-xl border border-line p-3" aria-describedby={`rights-${g.key}`}>
                      <legend className="flex items-center gap-1.5 px-1 text-[13px] font-semibold text-ink">
                        <GroupIcon className="size-4 text-ink-3" aria-hidden />
                        {t(g.title)}
                      </legend>
                      <p id={`rights-${g.key}`} className="mb-2 text-xs text-ink-3">
                        {t(g.description)}
                      </p>
                      <div className="grid gap-1 sm:grid-cols-2">
                        {items.map((r) => {
                          const on = form.rights.includes(r.key);
                          const section = r.needsSection ? data.sections.find((x) => x.key === r.needsSection) : null;
                          const blocked = !!section && !form.sections.includes(section.key);
                          const locked = isMe || (!on && (!r.grantable || blocked));
                          const neededBy = catalog.filter((x) => x.requires.includes(r.key) && form.rights.includes(x.key));
                          return (
                            <label
                              key={r.key}
                              title={!isMe && !on && !r.grantable ? t("У вас самих нет этого права — выдать его может администратор") : undefined}
                              className={`flex gap-2.5 rounded-lg px-2.5 py-2 transition-colors ${on ? "bg-accent/10" : ""} ${
                                locked ? "cursor-not-allowed opacity-55" : "cursor-pointer hover:bg-muted/60"
                              }`}
                            >
                              <input
                                type="checkbox"
                                className="mt-0.5 size-4 shrink-0 accent-[var(--color-accent)]"
                                checked={on}
                                disabled={locked}
                                onChange={(e) => toggleRightKey(r.key, e.target.checked)}
                              />
                              <span className="min-w-0">
                                <span className="block text-sm font-medium text-ink">{t(r.title)}</span>
                                <span className="block text-xs leading-snug text-ink-3">{t(r.description)}</span>
                                {blocked && section && <span className="mt-0.5 block text-xs text-warn">{t("Нужен раздел «{name}»", { name: t(section.title) })}</span>}
                                {on && neededBy.length > 0 && (
                                  <span className="mt-0.5 block text-xs text-ink-3">
                                    {t("Нужно для: {list}", { list: neededBy.map((x) => t(x.title)).join(", ") })}
                                  </span>
                                )}
                              </span>
                            </label>
                          );
                        })}
                      </div>
                    </fieldset>
                  );
                })}
              </div>
            </div>
          )}
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

/**
 * Сотрудник Verifix: поиск по ФИО или телефону среди работающих; выбор подставляет пустые поля профиля. Ниже — «Без привязки к Verifix»
 * (подрядчик, служебная учётная запись): тогда «Мой день» не показывает рабочее время.
 */
function VerifixLink({
  value,
  off,
  userId,
  onPick,
  onOff,
}: {
  value: { id: string; name: string } | null;
  off: boolean;
  userId: string | null;
  onPick: (c: HrCandidate | null) => void;
  onOff: (off: boolean) => void;
}) {
  const t = useT();
  const [query, setQuery] = useState("");
  const [found, setFound] = useState<HrCandidate[] | null>(null);

  useEffect(() => {
    const q = query.trim();
    if (q.length < 2) return;
    const timer = window.setTimeout(() => {
      bff<HrCandidate[]>(`users/hr-employees?q=${encodeURIComponent(q)}`).then(setFound, () => setFound([]));
    }, 250);
    return () => window.clearTimeout(timer);
  }, [query]);

  const results = query.trim().length < 2 ? null : found;
  return (
    <div className="space-y-2 rounded-xl border border-line p-3">
      <div className="flex items-center gap-2 text-sm font-medium text-ink">
        <Fingerprint aria-hidden className="size-4 text-ink-3" />
        {t("Сотрудник в Verifix")}
        {!off && <span className="text-bad">*</span>}
      </div>
      {value && !off ? (
        <div className="flex items-center justify-between gap-2 rounded-lg bg-accent/10 px-3 py-2">
          <span className="min-w-0 truncate text-sm font-medium text-ink">{value.name}</span>
          <button type="button" onClick={() => onPick(null)} aria-label={t("Убрать привязку")} className="grid size-8 shrink-0 place-items-center rounded-md text-ink-3 hover:bg-muted hover:text-ink">
            <X className="size-4" />
          </button>
        </div>
      ) : (
        !off && (
          <div>
            <input
              className={field}
              value={query}
              onChange={(e) => setQuery(e.target.value)}
              placeholder={t("ФИО или телефон")}
              aria-label={t("Сотрудник в Verifix")}
              autoComplete="off"
            />
            {results && (
              <ul className="mt-1 max-h-56 overflow-y-auto rounded-lg border border-line bg-surface-solid">
                {results.length === 0 && <li className="px-3 py-2 text-xs text-ink-3">{t("Работающих сотрудников с таким именем нет")}</li>}
                {results.map((c) => {
                  const taken = c.linkedUserId !== null && c.linkedUserId !== userId;
                  return (
                    <li key={c.id}>
                      <button
                        type="button"
                        disabled={taken}
                        onClick={() => {
                          onPick(c);
                          setQuery("");
                        }}
                        className="flex min-h-[44px] w-full flex-col justify-center px-3 py-1.5 text-left hover:bg-muted disabled:cursor-not-allowed disabled:opacity-55"
                      >
                        <span className="text-sm text-ink">{c.name}</span>
                        <span className="text-xs text-ink-3">
                          {[c.job, c.division, c.phone].filter(Boolean).join(" · ")}
                          {taken && ` · ${t("привязан к {name}", { name: c.linkedTo ?? "" })}`}
                        </span>
                      </button>
                    </li>
                  );
                })}
              </ul>
            )}
          </div>
        )
      )}
      <label className="flex cursor-pointer items-center gap-2 pt-1 text-sm text-ink-2">
        <input type="checkbox" checked={off} onChange={(e) => onOff(e.target.checked)} className="size-4 accent-[var(--color-accent)]" />
        {t("Без привязки к Verifix")}
      </label>
    </div>
  );
}
