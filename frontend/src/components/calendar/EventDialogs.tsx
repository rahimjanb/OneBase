"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { CalendarDays, Clock, Loader2, MapPin, Pencil, Trash2, Users, X } from "lucide-react";
import { Modal } from "@/components/files/Modal";
import { useT } from "@/i18n/client";
import { bff } from "@/lib/bff";
import { KIND_LABELS, timeOf, type CalendarAbilities, type CalendarEventInput, type CalendarEventView, type CalendarKind, type CalendarPerson } from "@/lib/calendar";
import { dayLabel } from "@/lib/my-day";

const field =
  "min-h-[44px] w-full rounded-lg border border-line bg-surface px-3 text-[14px] text-ink outline-none focus-visible:ring-2 focus-visible:ring-accent lg:min-h-10";
const label = "mb-1 block text-[12.5px] font-medium text-ink-3";
const button =
  "inline-flex min-h-[44px] items-center justify-center gap-1.5 rounded-lg px-4 text-[13.5px] font-semibold transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent disabled:opacity-60 lg:min-h-10";

/** Событие: вид, когда, где, аудитория, организатор и участники; автор или руководство — «Изменить» и «Удалить». */
export function EventDialog({ id, locale, abilities, onClose }: { id: string; locale: string; abilities: CalendarAbilities | null; onClose: () => void }) {
  const t = useT();
  const router = useRouter();
  const [event, setEvent] = useState<CalendarEventView | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [editing, setEditing] = useState(false);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    bff<CalendarEventView>(`calendar/events/${id}`).then(setEvent, (e: Error) => setError(e.message));
  }, [id]);

  if (editing && event) return <EventForm date={event.date} event={event} abilities={abilities} onClose={onClose} />;

  const remove = async () => {
    if (!event || !window.confirm(t("Удалить событие «{title}»?", { title: event.title }))) return;
    setBusy(true);
    try {
      await bff(`calendar/events/${event.id}`, { method: "DELETE" });
      router.refresh();
      onClose();
    } catch (e) {
      setError((e as Error).message);
      setBusy(false);
    }
  };

  const when = event
    ? `${dayLabel(event.date, locale)}, ${event.allDay ? t("весь день") : `${timeOf(event.start)}${event.end ? `–${timeOf(event.end)}` : ""}`}`
    : "";
  const audience = event
    ? [event.forEveryone ? t("Вся компания") : null, event.departmentName ? t("Отдел: {name}", { name: event.departmentName }) : null].filter(Boolean).join(" · ")
    : "";

  return (
    <Modal title={event?.title ?? t("Событие")} onClose={onClose}>
      <div className="me-ui space-y-4 px-5 py-4 text-[14px]">
        {error && <p className="text-bad">{error}</p>}
        {!event && !error && <Loader2 aria-label={t("Загрузка")} className="size-5 animate-spin text-ink-3" />}
        {event && (
          <>
            <p className="inline-flex rounded-md bg-surface-2 px-2 py-0.5 text-[12px] font-semibold text-ink-2">{t(KIND_LABELS[event.kind])}</p>
            <ul className="space-y-2 text-ink-2">
              <li className="flex items-center gap-2">
                <Clock aria-hidden className="size-4 text-ink-3" />
                {when}
              </li>
              {event.location && (
                <li className="flex items-center gap-2">
                  <MapPin aria-hidden className="size-4 text-ink-3" />
                  {event.location}
                </li>
              )}
              {(audience || event.participants.length > 0) && (
                <li className="flex items-start gap-2">
                  <Users aria-hidden className="mt-0.5 size-4 text-ink-3" />
                  <span>
                    {audience && <span className="block">{audience}</span>}
                    {event.participants.length > 0 && <span className="block">{event.participants.map((p) => p.name).join(", ")}</span>}
                  </span>
                </li>
              )}
            </ul>
            {event.description && <p className="whitespace-pre-wrap text-ink">{event.description}</p>}
            {event.organizer && <p className="text-[12.5px] text-ink-3">{t("Организатор: {name}", { name: event.organizer.name })}</p>}
            {event.canEdit && (
              <div className="flex justify-end gap-2 border-t border-line pt-4">
                <button type="button" onClick={remove} disabled={busy} className={`${button} text-bad hover:bg-bad-soft`}>
                  <Trash2 aria-hidden className="size-4" />
                  {t("Удалить")}
                </button>
                <button type="button" onClick={() => setEditing(true)} className={`${button} border border-line text-ink hover:bg-surface-2`}>
                  <Pencil aria-hidden className="size-4" />
                  {t("Изменить")}
                </button>
              </div>
            )}
          </>
        )}
      </div>
    </Modal>
  );
}

/** Подбор участников: поиск по имени, выбранные — плашками. */
function People({ value, onChange }: { value: CalendarPerson[]; onChange: (v: CalendarPerson[]) => void }) {
  const t = useT();
  const [query, setQuery] = useState("");
  const [found, setFound] = useState<CalendarPerson[]>([]);

  useEffect(() => {
    const q = query.trim();
    if (q.length < 2) return;
    const timer = window.setTimeout(() => {
      bff<CalendarPerson[]>(`calendar/people?q=${encodeURIComponent(q)}`).then(setFound, () => setFound([]));
    }, 250);
    return () => window.clearTimeout(timer);
  }, [query]);

  const results = query.trim().length < 2 ? [] : found.filter((p) => !value.some((v) => v.id === p.id)).slice(0, 6);
  return (
    <div>
      {value.length > 0 && (
        <ul className="mb-2 flex flex-wrap gap-1.5">
          {value.map((p) => (
            <li key={p.id} className="inline-flex items-center gap-1 rounded-full bg-accent-soft py-1 pl-2.5 pr-1 text-[12.5px] font-medium text-link">
              {p.name}
              <button type="button" onClick={() => onChange(value.filter((v) => v.id !== p.id))} aria-label={t("Убрать {name}", { name: p.name })} className="grid size-6 place-items-center rounded-full hover:bg-accent/20">
                <X aria-hidden className="size-3.5" />
              </button>
            </li>
          ))}
        </ul>
      )}
      <input value={query} onChange={(e) => setQuery(e.target.value)} placeholder={t("Найти сотрудника по имени")} className={field} aria-label={t("Участники")} />
      {results.length > 0 && (
        <ul className="mt-1 max-h-48 overflow-y-auto rounded-lg border border-line bg-surface-solid">
          {results.map((p) => (
            <li key={p.id}>
              <button
                type="button"
                onClick={() => {
                  onChange([...value, p]);
                  setQuery("");
                }}
                className="flex min-h-[44px] w-full flex-col justify-center px-3 py-1.5 text-left hover:bg-surface-2"
              >
                <span className="text-[13.5px] text-ink">{p.name}</span>
                {(p.position || p.department) && <span className="text-[12px] text-ink-3">{[p.position, p.department].filter(Boolean).join(" · ")}</span>}
              </button>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}

/**
 * Новое событие или правка: название, вид (встреча, срок отчёта, важное событие), дата, время или «весь день», место, описание,
 * участники и аудитория — только участники, отдел (свой — руководителю, любой — HR и руководству) или вся компания.
 */
export function EventForm({ date, event, abilities, onClose }: { date: string; event?: CalendarEventView; abilities: CalendarAbilities | null; onClose: () => void }) {
  const t = useT();
  const router = useRouter();
  const [title, setTitle] = useState(event?.title ?? "");
  const [kind, setKind] = useState<CalendarKind>(event?.kind ?? "Meeting");
  const [day, setDay] = useState(event?.date ?? date);
  const [allDay, setAllDay] = useState(event?.allDay ?? false);
  const [start, setStart] = useState(timeOf(event?.start) ?? "10:00");
  const [end, setEnd] = useState(timeOf(event?.end) ?? (event ? "" : "10:30"));
  const [location, setLocation] = useState(event?.location ?? "");
  const [description, setDescription] = useState(event?.description ?? "");
  const [people, setPeople] = useState<CalendarPerson[]>(event?.participants ?? []);
  const [audience, setAudience] = useState(event?.forEveryone ? "*" : (event?.departmentCode ?? ""));
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  // Свой прежний отдел события остаётся в списке, даже если права на новый нет.
  const departments = [
    ...(abilities?.departments ?? []),
    ...(event?.departmentCode && !abilities?.departments.some((d) => d.code === event.departmentCode) ? [{ code: event.departmentCode, name: event.departmentName ?? event.departmentCode }] : []),
  ];
  const canEveryone = (abilities?.canAddForEveryone ?? false) || event?.forEveryone;

  const save = async (e: React.FormEvent) => {
    e.preventDefault();
    setBusy(true);
    setError(null);
    const body: CalendarEventInput = {
      title,
      description: description || null,
      kind,
      date: day,
      start: allDay ? null : `${start}:00`,
      end: allDay || !end ? null : `${end}:00`,
      allDay,
      location: location || null,
      participants: people.map((p) => p.id),
      department: audience && audience !== "*" ? audience : null,
      forEveryone: audience === "*",
    };
    try {
      await bff(event ? `calendar/events/${event.id}` : "calendar/events", { method: event ? "PUT" : "POST", body: JSON.stringify(body) });
      router.refresh();
      onClose();
    } catch (err) {
      setError((err as Error).message);
      setBusy(false);
    }
  };

  return (
    <Modal title={event ? t("Изменить событие") : t("Новое событие")} onClose={onClose}>
      <form onSubmit={save} className="me-ui space-y-4 px-5 py-4">
        <div>
          <label htmlFor="ev-title" className={label}>
            {t("Название")}
          </label>
          <input id="ev-title" value={title} onChange={(e) => setTitle(e.target.value)} required maxLength={300} className={field} placeholder={t("Например, планёрка отдела")} />
        </div>
        <div className="grid gap-3 sm:grid-cols-2">
          <div>
            <label htmlFor="ev-kind" className={label}>
              {t("Вид")}
            </label>
            <select id="ev-kind" value={kind} onChange={(e) => setKind(e.target.value as CalendarKind)} className={field}>
              {(Object.keys(KIND_LABELS) as CalendarKind[]).map((k) => (
                <option key={k} value={k}>
                  {t(KIND_LABELS[k])}
                </option>
              ))}
            </select>
          </div>
          <div>
            <label htmlFor="ev-date" className={label}>
              {t("Дата")}
            </label>
            <input id="ev-date" type="date" value={day} onChange={(e) => setDay(e.target.value)} required className={field} />
          </div>
        </div>
        <label className="flex min-h-[44px] items-center gap-2 text-[14px] text-ink-2">
          <input type="checkbox" checked={allDay} onChange={(e) => setAllDay(e.target.checked)} className="size-4 accent-[var(--color-accent)]" />
          {t("Весь день")}
        </label>
        {!allDay && (
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label htmlFor="ev-start" className={label}>
                {t("Начало")}
              </label>
              <input id="ev-start" type="time" value={start} onChange={(e) => setStart(e.target.value)} required className={field} />
            </div>
            <div>
              <label htmlFor="ev-end" className={label}>
                {t("Окончание")}
              </label>
              <input id="ev-end" type="time" value={end} onChange={(e) => setEnd(e.target.value)} className={field} />
            </div>
          </div>
        )}
        <div>
          <label htmlFor="ev-location" className={label}>
            {t("Место")}
          </label>
          <input id="ev-location" value={location} onChange={(e) => setLocation(e.target.value)} maxLength={300} className={field} placeholder={t("Переговорная, ссылка на звонок")} />
        </div>
        <div>
          <span className={label}>{t("Участники")}</span>
          <People value={people} onChange={setPeople} />
        </div>
        {(departments.length > 0 || canEveryone) && (
          <div>
            <label htmlFor="ev-audience" className={label}>
              {t("Кто ещё видит")}
            </label>
            <select id="ev-audience" value={audience} onChange={(e) => setAudience(e.target.value)} className={field}>
              <option value="">{t("Только участники")}</option>
              {departments.map((d) => (
                <option key={d.code} value={d.code}>
                  {t("Весь отдел: {name}", { name: d.name })}
                </option>
              ))}
              {canEveryone && <option value="*">{t("Вся компания")}</option>}
            </select>
          </div>
        )}
        <div>
          <label htmlFor="ev-description" className={label}>
            {t("Описание")}
          </label>
          <textarea id="ev-description" value={description} onChange={(e) => setDescription(e.target.value)} maxLength={4000} rows={3} className={`${field} py-2`} />
        </div>
        {error && (
          <p role="alert" className="text-[13.5px] text-bad">
            {error}
          </p>
        )}
        <div className="flex justify-end gap-2 border-t border-line pt-4">
          <button type="button" onClick={onClose} className={`${button} border border-line text-ink hover:bg-surface-2`}>
            {t("Отмена")}
          </button>
          <button type="submit" disabled={busy} className={`${button} bg-accent text-white`}>
            {busy ? <Loader2 aria-hidden className="size-4 animate-spin" /> : <CalendarDays aria-hidden className="size-4" />}
            {event ? t("Сохранить") : t("Создать")}
          </button>
        </div>
      </form>
    </Modal>
  );
}
