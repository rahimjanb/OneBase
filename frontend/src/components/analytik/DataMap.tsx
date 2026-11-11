"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useEffect, useState } from "react";
import { CheckCircle2, ChevronDown, Database, KeyRound, Link2, Loader2, Lock, RefreshCw, Search, ShieldAlert } from "lucide-react";
import { useT } from "@/i18n/client";
import { bff } from "@/lib/bff";
import {
  bytes,
  compact,
  dateTime,
  moduleLabel,
  num,
  roleLabel,
  type AnalytikStatus,
  type CatalogGraph,
  type CatalogTableDetail,
  type CatalogTableRow,
  type QueryLogRow,
  type QueryOutcome,
} from "@/lib/analytik";
import { Card, Empty, SeverityBadge } from "./ui";

function AccessBadge({ row }: { row: Pick<CatalogTableRow, "access" | "available" | "unavailable"> }) {
  const t = useT();
  if (row.access === "closed") return <span className="inline-flex items-center gap-1 text-[11px] text-ink-4" title={row.unavailable ?? ""}><Lock className="size-3" />{t("закрыта")}</span>;
  if (!row.available) return <span className="inline-flex items-center gap-1 text-[11px] text-warn" title={row.unavailable ?? ""}><ShieldAlert className="size-3" />{t("нет доступа")}</span>;
  return <span className="inline-flex items-center gap-1 text-[11px] text-ok"><CheckCircle2 className="size-3" />{t("доступна")}</span>;
}

export function DataMapView({ status, graph, tables }: { status: AnalytikStatus; graph: CatalogGraph; tables: CatalogTableRow[] }) {
  const t = useT();
  const router = useRouter();
  const [q, setQ] = useState("");
  const [module, setModule] = useState<string | null>(null);
  const [found, setFound] = useState<CatalogTableRow[] | null>(null);
  const [onlyAvailable, setOnlyAvailable] = useState(false);
  const [refreshing, setRefreshing] = useState(false);
  const [message, setMessage] = useState<string | null>(null);

  // Поиск по смыслу — на сервере (словарь бизнес-слов, названия и описания), через 300 мс после ввода.
  useEffect(() => {
    const text = q.trim();
    if (!text) {
      setFound(null);
      return;
    }
    let alive = true;
    const timer = setTimeout(() => {
      bff<CatalogTableRow[]>(`analytik/catalog?q=${encodeURIComponent(text)}${module ? `&module=${module}` : ""}`).then((r) => alive && setFound(r)).catch(() => alive && setFound([]));
    }, 300);
    return () => {
      alive = false;
      clearTimeout(timer);
    };
  }, [q, module]);

  const list = (found ?? tables.filter((r) => !module || r.module === module)).filter((r) => !onlyAvailable || r.available);
  const refresh = async () => {
    setRefreshing(true);
    setMessage(null);
    try {
      const r = await bff<{ tables: number; added: number; changed: number; removed: number; verified: number; durationMs: number }>("analytik/catalog/refresh", { method: "POST" });
      setMessage(t("Каталог обновлён: таблиц {n}, новых {a}, изменённых {c}, исчезнувших {r}, проверено связей {v} за {ms} мс", { n: r.tables, a: r.added, c: r.changed, r: r.removed, v: r.verified, ms: r.durationMs }));
      router.refresh();
    } catch (e) {
      setMessage((e as Error).message);
    } finally {
      setRefreshing(false);
    }
  };

  return (
    <div className="space-y-4">
      <div className="grid grid-cols-2 gap-2.5 sm:grid-cols-3 xl:grid-cols-6">
        {[
          ["Таблиц в базе", num(status.tables)],
          ["Доступно вам", num(status.availableTables)],
          ["Связей", num(status.relations)],
          ["Подтверждено данными", num(status.confirmedRelations)],
          ["Каталог обновлён", status.catalogRefreshedAt ? dateTime(status.catalogRefreshedAt) : t("ещё не обновлялся")],
          ["Подключение", t(status.dedicatedReadOnlyRole ? "роль только на чтение" : "транзакции только на чтение")],
        ].map(([label, value]) => (
          <div key={label} className="rounded-xl border border-line bg-surface p-3">
            <p className="text-xs text-ink-3">{t(label)}</p>
            <p className="mt-1 text-[15px] font-semibold text-ink">{value}</p>
          </div>
        ))}
      </div>
      {status.manager && (
        <div className="flex flex-wrap items-center gap-3">
          <button type="button" onClick={() => void refresh()} disabled={refreshing} className="inline-flex h-9 items-center gap-1.5 rounded-lg border border-line bg-surface px-3 text-sm text-ink hover:border-line-strong disabled:opacity-50">
            {refreshing ? <Loader2 className="size-4 animate-spin" /> : <RefreshCw className="size-4" />}
            {t("Обновить каталог сейчас")}
          </button>
          {message && <span className="text-xs text-ink-3">{message}</span>}
        </div>
      )}
      <Card title="Модули базы" subtitle="Таблиц, строк и доступно вам; связи между модулями — подтверждённые">
        <div className="grid grid-cols-2 gap-2 sm:grid-cols-3 xl:grid-cols-6">
          {graph.modules.map((m) => (
            <button
              key={m.module}
              type="button"
              onClick={() => setModule(module === m.module ? null : m.module)}
              className={`rounded-xl border p-3 text-left transition-colors ${module === m.module ? "border-accent bg-accent-soft" : "border-line hover:border-line-strong"}`}
            >
              <p className="text-[13px] font-medium text-ink">{t(moduleLabel[m.module] ?? m.module)}</p>
              <p className="mt-0.5 text-[11px] text-ink-3">
                {m.tables} {t("табл.")} · {compact(m.rows)} {t("строк")}
              </p>
              <p className="text-[11px] text-ink-4">
                {t("доступно")}: {m.available}
              </p>
            </button>
          ))}
        </div>
        {graph.edges.length > 0 && (
          <p className="mt-3 text-[11px] leading-relaxed text-ink-4">
            {graph.edges.slice(0, 12).map((e) => `${t(moduleLabel[e.from] ?? e.from)} ↔ ${t(moduleLabel[e.to] ?? e.to)} (${e.relations})`).join(" · ")}
          </p>
        )}
      </Card>
      <div className="flex flex-wrap items-center gap-2">
        <div className="relative min-w-[240px] flex-1">
          <Search className="pointer-events-none absolute left-3 top-1/2 size-4 -translate-y-1/2 text-ink-4" />
          <input value={q} onChange={(e) => setQ(e.target.value)} placeholder={t("Найти по смыслу: «продажи по точкам», «расход сырья», «опоздания», «xodimlar»…")} className="h-10 w-full rounded-xl border border-line bg-surface pl-9 pr-3 text-sm text-ink placeholder:text-ink-4" />
        </div>
        <label className="inline-flex items-center gap-1.5 text-xs text-ink-3">
          <input type="checkbox" checked={onlyAvailable} onChange={(e) => setOnlyAvailable(e.target.checked)} />
          {t("Только доступные мне")}
        </label>
      </div>
      {list.length === 0 ? (
        <Empty text="Таблиц не найдено" />
      ) : (
        <div className="overflow-x-auto rounded-2xl border border-line bg-surface">
          <table className="w-full min-w-[720px] text-[12.5px]">
            <thead>
              <tr className="border-b border-line text-left text-ink-4">
                <th className="px-3 py-2 font-normal">{t("Таблица")}</th>
                <th className="px-3 py-2 font-normal">{t("Модуль")}</th>
                <th className="px-3 py-2 text-right font-normal">{t("Строк")}</th>
                <th className="px-3 py-2 text-right font-normal">{t("Колонок")}</th>
                <th className="px-3 py-2 text-right font-normal">{t("Связей")}</th>
                <th className="px-3 py-2 font-normal">{t("Доступ")}</th>
              </tr>
            </thead>
            <tbody>
              {list.slice(0, 300).map((r) => (
                <tr key={r.fullName} className="border-b border-line/60 hover:bg-muted/50">
                  <td className="px-3 py-2">
                    <Link href={`/analytik/data/${r.schema}/${r.name}`} className="block">
                      <span className="font-medium text-ink">{r.title ?? r.name}</span>
                      <span className="ml-2 font-mono text-[11px] text-ink-4">{r.fullName}</span>
                      {r.description && <span className="mt-0.5 block line-clamp-1 text-[11px] text-ink-3">{r.description}</span>}
                    </Link>
                  </td>
                  <td className="px-3 py-2 text-ink-3">{t(moduleLabel[r.module] ?? r.module)}</td>
                  <td className="px-3 py-2 text-right tabular-nums text-ink-2">{r.rowsExact ? num(r.rows) : `≈${compact(r.rows)}`}</td>
                  <td className="px-3 py-2 text-right tabular-nums text-ink-3">{r.columns}</td>
                  <td className="px-3 py-2 text-right tabular-nums text-ink-3">{r.relations}</td>
                  <td className="px-3 py-2"><AccessBadge row={r} /></td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}

// ---------- Карточка таблицы ----------

export function TableDetailView({ detail, manager }: { detail: CatalogTableDetail; manager: boolean }) {
  const t = useT();
  const { table } = detail;
  const [sample, setSample] = useState<QueryOutcome | null>(null);
  const [profile, setProfile] = useState<{ column: string; data: Record<string, unknown> | null; error?: string } | null>(null);

  useEffect(() => {
    if (!table.available) return;
    let alive = true;
    fetch("/bff/api/analytik/query", { method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ from: table.fullName, limit: 20 }) })
      .then((r) => r.json())
      .then((r: QueryOutcome) => alive && setSample(r))
      .catch(() => undefined);
    return () => {
      alive = false;
    };
  }, [table.fullName, table.available]);

  const showProfile = async (column: string) => {
    setProfile({ column, data: null });
    try {
      setProfile({ column, data: await bff<Record<string, unknown>>(`analytik/profile?table=${encodeURIComponent(table.fullName)}&column=${encodeURIComponent(column)}`) });
    } catch (e) {
      setProfile({ column, data: null, error: (e as Error).message });
    }
  };

  return (
    <div className="space-y-4">
      <Card title={table.title ?? table.name} subtitle={`${table.fullName} · ${t(moduleLabel[table.module] ?? table.module)} · ${table.rowsExact ? num(table.rows) : `≈${compact(table.rows)}`} ${t("строк")} · ${bytes(table.sizeBytes)}`}>
        <p className="text-[13px] leading-relaxed text-ink-2">{table.description}</p>
        <div className="mt-3 flex flex-wrap gap-3 text-xs text-ink-3">
          <AccessBadge row={table} />
          {table.requiredPermission && <span>{t("Право")}: {table.requiredPermission.replaceAll("|", " / ")}</span>}
          {table.dateColumn && <span>{t("Главная дата")}: {table.dateColumn}</span>}
          {detail.scopeRule && <span>{t("Ограничение строк по области продаж")}: {detail.scopeRule}</span>}
          <span>{t("Описание")}: {t(table.descriptionSource === "manual" ? "администратора" : table.descriptionSource === "knowledge" ? "знания OneBase" : "автоматическое")}</span>
          <span>{t("Обнаружена")}: {dateTime(table.firstSeenAt)}</span>
        </div>
        {table.unavailable && <p className="mt-2 text-xs text-warn">{table.unavailable}</p>}
      </Card>
      {detail.metrics.length > 0 && (
        <Card title="Показатели компании на этих данных" subtitle="Официальные цифры — инструментом-владельцем: у него правила отчётов">
          <ul className="space-y-2 text-[12.5px]">
            {detail.metrics.map((m) => (
              <li key={m.code}>
                <span className="font-medium text-ink">{m.title}</span> — <span className="text-ink-2">{m.definition}</span>
                <span className="block text-[11px] text-ink-4">{t("Владелец")}: {m.authoritativeTool}</span>
              </li>
            ))}
          </ul>
        </Card>
      )}
      <div className="grid gap-4 xl:grid-cols-[minmax(0,1.2fr)_minmax(0,1fr)]">
        <Card title="Колонки" subtitle="Нажмите на колонку — профиль значений">
          {detail.columns.length === 0 ? (
            <Empty text="Колонки закрытой таблицы видит управляющий Аналитиком" />
          ) : (
            <ul className="divide-y divide-line text-[12.5px]">
              {detail.columns.map((c) => (
                <li key={c.name}>
                  <button type="button" disabled={c.sensitivity !== "none" || !table.available} onClick={() => void showProfile(c.name)} className="flex w-full items-center gap-2 py-1.5 text-left disabled:cursor-default">
                    {c.primaryKey ? <KeyRound className="size-3.5 shrink-0 text-warn" /> : c.role === "reference" ? <Link2 className="size-3.5 shrink-0 text-accent-strong" /> : <Database className="size-3.5 shrink-0 text-ink-4" />}
                    <span className="font-mono text-[12px] text-ink">{c.name}</span>
                    <span className="text-[11px] text-ink-4">{c.dataType}</span>
                    <span className="ml-auto flex shrink-0 items-center gap-2 text-[11px]">
                      {c.title && <span className="text-ink-3">{c.title}</span>}
                      <span className="rounded bg-muted px-1.5 py-0.5 text-ink-3">{t(roleLabel[c.role] ?? c.role)}</span>
                      {c.sensitivity !== "none" && <SeverityBadge value={c.sensitivity === "secret" ? "critical" : "warning"} label={c.sensitivity === "secret" ? "секрет" : c.sensitivity === "personal" ? "персональные" : "служебная"} />}
                    </span>
                  </button>
                  {profile?.column === c.name && (
                    <div className="mb-2 rounded-lg bg-muted/60 p-2.5 text-[11.5px] text-ink-2">
                      {profile.error ? (
                        profile.error
                      ) : !profile.data ? (
                        <Loader2 className="size-4 animate-spin text-ink-4" />
                      ) : (
                        <>
                          <p>
                            {t("Строк")}: {num(profile.data.rows as number)} · {t("заполнено")}: {num(profile.data.filled as number)} · {t("различных")}: {num(profile.data.distinct as number)}
                            {profile.data.min != null && ` · ${t("от")} ${String(profile.data.min)} ${t("до")} ${String(profile.data.max)}`}
                          </p>
                          {Array.isArray(profile.data.top) && profile.data.top.length > 0 && (
                            <p className="mt-1 text-ink-3">
                              {(profile.data.top as { value: unknown; count: number }[]).map((x) => `${x.value == null ? "—" : String(x.value)} — ${num(x.count)}`).join(" · ")}
                            </p>
                          )}
                        </>
                      )}
                    </div>
                  )}
                </li>
              ))}
            </ul>
          )}
        </Card>
        <Card title="Связи" subtitle="Внешние ключи, знания OneBase и выведенные по именам — проверены данными">
          {detail.relations.length === 0 ? (
            <Empty text="Связей нет" />
          ) : (
            <ul className="space-y-1.5 text-[12px]">
              {detail.relations.map((r) => {
                const other = r.direction === "out" ? r.to : r.from;
                const [schema, name] = other.split(".");
                return (
                  <li key={`${r.from}.${r.fromColumn}-${r.to}.${r.toColumn}`} className="flex flex-wrap items-center gap-x-2">
                    <span className="text-ink-4">{r.direction === "out" ? "→" : "←"}</span>
                    <Link href={`/analytik/data/${schema}/${name}`} className="font-mono text-ink hover:underline">
                      {other}
                    </Link>
                    <span className="text-ink-4">
                      {t("через")} {r.direction === "out" ? r.fromColumn : `${r.fromColumn}`}
                      {r.targetFilter ? ` (${r.targetFilter})` : ""}
                    </span>
                    <span className={`ml-auto text-[11px] ${r.status === "confirmed" ? "text-ok" : r.status === "rejected" ? "text-bad" : "text-ink-4"}`}>
                      {t(r.kind === "fk" ? "внешний ключ" : r.kind === "manual" ? "знание" : "по имени")} · {t(r.status === "confirmed" ? "подтверждена" : r.status === "rejected" ? "отклонена" : "не проверена")}
                      {r.matchRatio != null && r.kind !== "fk" ? ` ${num(r.matchRatio * 100, 1)}%` : ""}
                    </span>
                  </li>
                );
              })}
            </ul>
          )}
        </Card>
      </div>
      {table.available && (
        <Card title="Пример данных" subtitle={sample?.result ? `${t("Запрос")} ${sample.result.queryId.slice(0, 8)} · ${sample.result.elapsedMs} ${t("мс")}${sample.result.scope ? ` · ${sample.result.scope}` : ""}` : undefined}>
          {!sample ? (
            <Loader2 className="size-4 animate-spin text-ink-4" />
          ) : !sample.result ? (
            <Empty text={sample.error ?? "Нет данных"} />
          ) : sample.result.rows.length === 0 ? (
            <Empty text="Таблица пуста" />
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full text-[11.5px]">
                <thead>
                  <tr className="border-b border-line text-left text-ink-4">
                    {sample.result.columns.map((c) => (
                      <th key={c.name} className="whitespace-nowrap px-2 py-1 font-normal">
                        {c.name}
                      </th>
                    ))}
                  </tr>
                </thead>
                <tbody>
                  {sample.result.rows.map((row, i) => (
                    <tr key={i} className="border-b border-line/50">
                      {row.map((v, j) => (
                        <td key={j} className="max-w-[240px] truncate whitespace-nowrap px-2 py-1 text-ink-2">
                          {v == null ? <span className="text-ink-4">—</span> : String(v)}
                        </td>
                      ))}
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </Card>
      )}
      {manager && <AnnotationForm detail={detail} />}
    </div>
  );
}

function AnnotationForm({ detail }: { detail: CatalogTableDetail }) {
  const t = useT();
  const router = useRouter();
  const [open, setOpen] = useState(false);
  const [title, setTitle] = useState(detail.table.title ?? "");
  const [description, setDescription] = useState(detail.table.description ?? "");
  const [access, setAccess] = useState<string>("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const save = async () => {
    setBusy(true);
    setError(null);
    try {
      await bff("analytik/catalog/annotations", {
        method: "PUT",
        body: JSON.stringify({ kind: "table", target: detail.table.fullName, title, description, access: access || null }),
      });
      router.refresh();
      setOpen(false);
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  };
  return (
    <Card title="Описание для аналитика (управляющий)" action={<button type="button" onClick={() => setOpen(!open)} className="text-xs text-accent-strong"><ChevronDown className={`inline size-3.5 ${open ? "rotate-180" : ""}`} /> {t(open ? "Свернуть" : "Изменить")}</button>}>
      {open ? (
        <div className="space-y-2">
          <input value={title} onChange={(e) => setTitle(e.target.value)} placeholder={t("Название")} className="h-9 w-full rounded-lg border border-line bg-surface px-3 text-sm text-ink" />
          <textarea value={description} onChange={(e) => setDescription(e.target.value)} rows={4} placeholder={t("Смысл таблицы и бизнес-правила: какие строки считаются, какие даты и статусы")} className="w-full rounded-lg border border-line bg-surface px-3 py-2 text-sm text-ink" />
          <select value={access} onChange={(e) => setAccess(e.target.value)} className="h-9 rounded-lg border border-line bg-surface px-2 text-sm text-ink" aria-label={t("Доступ")}>
            <option value="">{t("Доступ — по правилам схемы")}</option>
            <option value="open">{t("Открыть для аналитики (по праву модуля)")}</option>
            <option value="closed">{t("Закрыть для аналитики")}</option>
          </select>
          <div className="flex items-center gap-2">
            <button type="button" onClick={() => void save()} disabled={busy} className="h-9 rounded-lg bg-accent px-4 text-sm font-medium text-white disabled:opacity-50">
              {t("Сохранить")}
            </button>
            {error && <span className="text-xs text-bad">{error}</span>}
          </div>
          <p className="text-[11px] text-ink-4">{t("Ручное описание важнее автоматического и знаний OneBase; секретные колонки открыть нельзя.")}</p>
        </div>
      ) : (
        <p className="text-xs text-ink-3">{t("Добавьте смысл таблицы и правила — их увидят AI-аналитики и пользователи «Карты данных».")}</p>
      )}
    </Card>
  );
}

// ---------- История ----------

export function HistoryView({ queries, chats, manager, all }: { queries: QueryLogRow[]; chats: { id: string; title: string; lastMessageAt: string }[]; manager: boolean; all: boolean }) {
  const t = useT();
  const [open, setOpen] = useState<string | null>(null);
  return (
    <div className="grid gap-4 xl:grid-cols-[minmax(0,1fr)_minmax(0,1.6fr)]">
      <Card title="Исследования (чаты с аналитиком)">
        {chats.length === 0 ? (
          <Empty text="Вопросов аналитику ещё не было" />
        ) : (
          <ul className="space-y-1">
            {chats.map((c) => (
              <li key={c.id}>
                <Link href={`/analytik/chat/${c.id}`} className="flex items-center justify-between gap-2 rounded-lg px-2 py-1.5 text-[12.5px] text-ink-2 hover:bg-muted">
                  <span className="min-w-0 truncate">{c.title}</span>
                  <span className="shrink-0 text-[11px] text-ink-4">{dateTime(c.lastMessageAt)}</span>
                </Link>
              </li>
            ))}
          </ul>
        )}
      </Card>
      <Card
        title="Журнал запросов к базе"
        subtitle="Каждый запрос движка: кто, откуда, таблицы, область, строк, время, план и SQL"
        action={manager ? <Link href={all ? "/analytik/history" : "/analytik/history?all=1"} className="text-xs text-accent-strong">{t(all ? "Только мои" : "Все пользователи")}</Link> : undefined}
      >
        {queries.length === 0 ? (
          <Empty text="Запросов ещё не было" />
        ) : (
          <ul className="divide-y divide-line text-[12px]">
            {queries.map((q) => (
              <li key={q.id} className="py-2">
                <button type="button" onClick={() => setOpen(open === q.id ? null : q.id)} className="flex w-full flex-wrap items-center gap-x-3 gap-y-1 text-left">
                  <SeverityBadge value={q.status === "ok" ? "none" : q.status === "denied" ? "warning" : "critical"} label={q.status} />
                  <span className="min-w-0 flex-1 truncate font-mono text-[11.5px] text-ink">{q.tables ?? "—"}</span>
                  <span className="text-ink-4">
                    {q.origin}
                    {q.agentCode ? ` · ${q.agentCode}` : ""} · {num(q.rowCount)} {t("стр.")} · {q.durationMs} {t("мс")} · {dateTime(q.createdAt)}
                  </span>
                </button>
                {open === q.id && (
                  <div className="mt-2 space-y-2">
                    {q.error && <p className="text-bad">{q.error}</p>}
                    {q.scope && <p className="text-ink-3">{t("Область")}: {q.scope}</p>}
                    <pre className="overflow-x-auto rounded-lg bg-muted/60 p-2 text-[11px] text-ink-2">{JSON.stringify(JSON.parse(q.plan), null, 2)}</pre>
                    {q.sql && <pre className="overflow-x-auto rounded-lg bg-muted/60 p-2 text-[11px] text-ink-2">{q.sql}</pre>}
                  </div>
                )}
              </li>
            ))}
          </ul>
        )}
      </Card>
    </div>
  );
}
