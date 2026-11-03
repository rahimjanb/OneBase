"use client";

import { useMemo, useState } from "react";
import { Pencil, Search, UserPlus } from "lucide-react";
import { button, field, primary } from "@/components/ai/form";
import { Note, Section } from "@/components/sales/bits";
import { UserDialog } from "@/components/settings/UserDialog";
import { useT } from "@/i18n/client";
import { initials, seenOf, type UserRow, type UsersView } from "@/lib/users";

/**
 * Пользователи OneBase: таблица (на телефоне — карточки) с ролью, открытыми разделами и тем, когда человек был в сети («в сети» — OneBase
 * открыт у него сейчас). Сортировка — по имени или «недавно в сети». Добавление и изменение — в отдельном окне (UserDialog): профиль, роль
 * и разделы галочками.
 */
export function UsersSettings({ initial }: { initial: UsersView }) {
  const t = useT();
  const [data, setData] = useState(initial);
  const [editing, setEditing] = useState<UserRow | "new" | null>(null);
  const [message, setMessage] = useState<string | null>(null);
  const [query, setQuery] = useState("");
  const [sort, setSort] = useState<"name" | "seen">("name");

  const titles = useMemo(() => new Map(data.sections.map((s) => [s.key, s.title])), [data.sections]);
  const active = data.users.filter((u) => u.isActive).length;
  const online = data.users.filter((u) => u.online).length;
  const users = useMemo(() => {
    const q = query.trim().toLowerCase();
    const found = q ? data.users.filter((u) => [u.fullName, u.login, u.position, u.roleName, u.email, u.phone].some((x) => x?.toLowerCase().includes(q))) : data.users;
    if (sort === "name") return found;
    // Недавно в сети: сейчас в сети → по времени последнего запроса (или входа) → не заходившие.
    const at = (u: UserRow) => (u.online ? Number.MAX_SAFE_INTEGER : new Date(u.lastSeenAt ?? u.lastLoginAt ?? 0).getTime());
    return [...found].sort((a, b) => at(b) - at(a));
  }, [data.users, query, sort]);

  const open = (target: UserRow | "new") => {
    setMessage(null);
    setEditing(target);
  };

  const sectionChips = (u: UserRow) =>
    u.sections.length === 0 ? (
      <span className="text-xs text-warn">{t("нет разделов")}</span>
    ) : (
      <div className="flex flex-wrap gap-1">
        {u.sections.map((key) => (
          <span key={key} className="rounded-md bg-muted px-1.5 py-0.5 text-[11px] font-medium text-ink-2">
            {t(titles.get(key) ?? key)}
          </span>
        ))}
        {u.sectionsCustom && (
          <span className="rounded-md bg-accent-soft px-1.5 py-0.5 text-[11px] font-semibold text-accent-strong" title={t("Разделы отмечены вручную, не как у роли")}>
            {t("вручную")}
          </span>
        )}
      </div>
    );

  const roleOf = (u: UserRow) => (u.roleName ? u.roleName.split(", ").map((r) => t(r)).join(", ") : <span className="text-warn">{t("без роли")}</span>);
  const status = (u: UserRow) =>
    u.isActive ? (
      <span className="inline-flex items-center gap-1.5 text-xs font-medium text-ok">
        <span className="size-1.5 rounded-full bg-ok" aria-hidden />
        {t("активен")}
      </span>
    ) : (
      <span className="inline-flex items-center gap-1.5 text-xs font-medium text-ink-3">
        <span className="size-1.5 rounded-full bg-ink-4" aria-hidden />
        {t("отключён")}
      </span>
    );
  const seen = (u: UserRow) => {
    const s = seenOf(u, t);
    return (
      <span title={s.title} className={`inline-flex items-center gap-1.5 whitespace-nowrap text-xs ${s.tone === "online" ? "font-medium text-ok" : s.tone === "recent" ? "text-ink-2" : "text-ink-3"}`}>
        {s.tone === "online" && <span className="size-1.5 rounded-full bg-ok" aria-hidden />}
        {s.text}
      </span>
    );
  };
  const edit = (u: UserRow) =>
    u.canEdit ? (
      <button className={button} onClick={() => open(u)} aria-label={t("Изменить {name}", { name: u.fullName || u.login })}>
        <Pencil className="size-3.5" />
        {t("Изменить")}
      </button>
    ) : (
      <span className="text-xs text-ink-3">{t("только администратор")}</span>
    );

  return (
    <>
      <Section
        title={t("Пользователи")}
        hint={`${t("{total} · активных {active}", { total: data.users.length, active })}${online > 0 ? ` · ${t("в сети {n}", { n: online })}` : ""}`}
        actions={
          <button className={primary} onClick={() => open("new")}>
            <UserPlus className="size-4" />
            {t("Добавить пользователя")}
          </button>
        }
      >
        {message && (
          <p className="mb-3 rounded-lg bg-ok-soft px-3 py-2 text-sm text-ok" role="status">
            {message}
          </p>
        )}

        <div className="mb-3 flex flex-wrap items-center gap-2">
          {data.users.length > 6 && (
            <div className="relative min-w-0 max-w-sm flex-1 basis-60">
              <Search className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-ink-3" aria-hidden />
              <input
                className={`${field} pl-9`}
                value={query}
                onChange={(e) => setQuery(e.target.value)}
                placeholder={t("Поиск: имя, логин, должность, роль")}
                aria-label={t("Поиск пользователей")}
                type="search"
              />
            </div>
          )}
          <select value={sort} onChange={(e) => setSort(e.target.value as "name" | "seen")} aria-label={t("Сортировка")} className="h-10 rounded-lg border border-line bg-surface px-3 text-sm text-ink focus:border-accent focus:outline-none focus:ring-2 focus:ring-accent/20">
            <option value="name">{t("По имени")}</option>
            <option value="seen">{t("Недавно в сети")}</option>
          </select>
        </div>

        {/* Телефон — карточки */}
        <ul className="space-y-2 md:hidden">
          {users.map((u) => (
            <li key={u.id} className={`rounded-xl border border-line p-3 ${u.isActive ? "" : "opacity-60"}`}>
              <div className="flex items-start gap-3">
                <Avatar user={u} />
                <div className="min-w-0 flex-1">
                  <p className="truncate font-medium text-ink">
                    {u.fullName || "—"}
                    {u.isMe && <span className="ml-2 rounded-full bg-muted px-1.5 py-0.5 text-[10px] font-medium text-ink-2">{t("это вы")}</span>}
                  </p>
                  <p className="truncate text-xs text-ink-3">
                    {u.login}
                    {u.position && ` · ${u.position}`}
                  </p>
                  <p className="mt-1 text-sm text-ink">{roleOf(u)}</p>
                  <p className="mt-0.5">{seen(u)}</p>
                </div>
                {status(u)}
              </div>
              <div className="mt-2.5">{sectionChips(u)}</div>
              <div className="mt-3 flex justify-end">{edit(u)}</div>
            </li>
          ))}
        </ul>

        {/* Компьютер — таблица */}
        <div className="hidden overflow-x-auto md:block">
          <table className="w-full min-w-[960px] text-sm">
            <thead>
              <tr className="border-b border-line text-left text-[11px] uppercase tracking-wide text-ink-3">
                <th className="py-2 pr-3 font-semibold">{t("Сотрудник")}</th>
                <th className="py-2 pr-3 font-semibold">{t("Должность")}</th>
                <th className="py-2 pr-3 font-semibold">{t("Роль")}</th>
                <th className="py-2 pr-3 font-semibold">{t("Разделы")}</th>
                <th className="py-2 pr-3 font-semibold">{t("Был в сети")}</th>
                <th className="py-2 pr-3 font-semibold">{t("Статус")}</th>
                <th className="py-2" />
              </tr>
            </thead>
            <tbody>
              {users.map((u) => (
                <tr key={u.id} className={`border-b border-line align-top last:border-b-0 ${u.isActive ? "" : "opacity-60"}`}>
                  <td className="py-2.5 pr-3">
                    <div className="flex items-center gap-3">
                      <Avatar user={u} />
                      <div className="min-w-0">
                        <span className="block font-medium text-ink">
                          {u.fullName || "—"}
                          {u.isMe && <span className="ml-2 rounded-full bg-muted px-1.5 py-0.5 text-[10px] font-medium text-ink-2">{t("это вы")}</span>}
                        </span>
                        <span className="block text-xs text-ink-3">
                          {u.login}
                          {u.phone && ` · ${u.phone}`}
                        </span>
                      </div>
                    </div>
                  </td>
                  <td className="py-2.5 pr-3 text-ink-2">{u.position ?? "—"}</td>
                  <td className="py-2.5 pr-3 text-ink">{roleOf(u)}</td>
                  <td className="max-w-[320px] py-2.5 pr-3">{sectionChips(u)}</td>
                  <td className="py-2.5 pr-3">{seen(u)}</td>
                  <td className="whitespace-nowrap py-2.5 pr-3">{status(u)}</td>
                  <td className="py-2.5 text-right">{edit(u)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
        {users.length === 0 && <p className="py-6 text-center text-sm text-ink-3">{t("Никого не нашли")}</p>}

        <Note>
          {t(
            "Удаления нет: отключённый пользователь не может войти, а его история в OneBase сохраняется. Роль задаёт права на управление и настройки и разделы по умолчанию; разделы можно изменить галочками — по ним открываются страницы и отвечают AI-сотрудники. Выдать роль или раздел можно, только если у вас самих есть эти права, — поэтому администраторов меняет только администратор.",
          )}
        </Note>
      </Section>

      <UserDialog
        target={editing}
        data={data}
        onClose={() => setEditing(null)}
        onSaved={(text, view) => {
          setData(view);
          setMessage(text);
          setEditing(null);
        }}
      />
    </>
  );
}

function Avatar({ user }: { user: UserRow }) {
  return (
    <span
      aria-hidden
      className={`grid size-9 shrink-0 place-items-center rounded-full text-xs font-semibold ${user.isActive ? "bg-accent-soft text-accent-strong" : "bg-muted text-ink-3"}`}
    >
      {initials(user)}
    </span>
  );
}
