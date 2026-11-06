"use client";

import { useEffect, useState } from "react";
import { useRouter } from "next/navigation";
import { Loader2, Pencil, Plus, Warehouse as WarehouseIcon } from "lucide-react";
import { button, field, primary } from "@/components/ai/form";
import { Sheet } from "@/components/field/Sheet";
import { ErrorNote } from "@/components/tasks/TaskBadges";
import { useT } from "@/i18n/client";
import { bff } from "@/lib/bff";
import { dataOwnerLabel, dateRu, warehouseKindLabel, type DataOwner, type Warehouse, type WarehouseKind, type WarehousesView } from "@/lib/procurement";

const kinds: WarehouseKind[] = ["Central", "RawMaterials", "Workshop", "Other"];

/**
 * Склады снабжения в «Настройки → Отделы → Снабжение»: список (вид, код, отдел кладовой цеха, кладовщики, позиций с остатком) и форма
 * склада. Заводит и меняет — право «Настройки снабжения». Склад по умолчанию не отключается, склад с остатком — тоже (проверяет сервер).
 */
export function WarehousesSection({ view }: { view: WarehousesView }) {
  const t = useT();
  const [editing, setEditing] = useState<Warehouse | "new" | null>(null);
  return (
    <section className="rounded-xl border border-line bg-surface p-5" aria-labelledby="supply-warehouses">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div className="min-w-0">
          <h2 id="supply-warehouses" className="text-base font-semibold text-ink">
            {t("Склады")}
          </h2>
          <p className="mt-1 text-sm text-ink-2">
            {t(
              "Склад по умолчанию — на нём резервируют заявки и приходуют закупки. Кладовщик, закреплённый за складами, проводит операции только по ним; без закреплений — по всем складам.",
            )}
          </p>
        </div>
        {view.canEdit && (
          <button type="button" onClick={() => setEditing("new")} className={primary}>
            <Plus className="size-4" aria-hidden /> {t("Добавить склад")}
          </button>
        )}
      </div>
      <ul className="mt-4 divide-y divide-line rounded-xl border border-line">
        {view.warehouses.map((w) => (
          <li key={w.id} className={`flex items-start gap-3 px-3 py-3 ${w.isActive ? "" : "opacity-60"}`}>
            <WarehouseIcon className="mt-0.5 size-4 shrink-0 text-ink-3" aria-hidden />
            <div className="min-w-0 flex-1">
              <p className="flex flex-wrap items-center gap-x-2 text-sm font-medium text-ink">
                {w.name}
                {w.isDefault && <span className="rounded-md bg-accent-soft px-1.5 py-0.5 text-[11px] font-semibold text-accent-strong">{t("по умолчанию")}</span>}
                {!w.isActive && <span className="rounded-md bg-muted px-1.5 py-0.5 text-[11px] text-ink-3">{t("отключён")}</span>}
              </p>
              <p className="mt-0.5 text-xs text-ink-3">
                {[t(warehouseKindLabel[w.kind]), w.code, w.departmentName, w.address].filter(Boolean).join(" · ")}
              </p>
              <p className="mt-0.5 text-xs text-ink-3">
                {w.keepers.length > 0 ? t("Кладовщики: {list}", { list: w.keepers.map((k) => k.name).join(", ") }) : t("Кладовщики: все, кто не закреплён за складами")}
                {" · "}
                {t("позиций с остатком: {n}", { n: w.items })}
              </p>
              {w.dataOwner && w.dataOwner !== "None" && (
                <p className={`mt-0.5 text-xs ${w.dataOwner === "Sheet" ? "text-warn" : "text-ink-3"}`}>
                  {w.dataOwner === "Sheet"
                    ? t("Ведётся в таблице с {date}: ручные операции закрыты, остатки приходят загрузкой журнала", { date: dateRu(w.dataOwnerSince ?? null) })
                    : t("Ведётся в OneBase с {date}: загрузка журнала закрыта", { date: dateRu(w.dataOwnerSince ?? null) })}
                </p>
              )}
            </div>
            {view.canEdit && (
              <button type="button" onClick={() => setEditing(w)} className={`${button} h-8 px-2.5`} aria-label={t("Изменить склад «{name}»", { name: w.name })}>
                <Pencil className="size-4" aria-hidden />
              </button>
            )}
          </li>
        ))}
      </ul>
      {editing && <WarehouseForm view={view} warehouse={editing === "new" ? null : editing} onClose={() => setEditing(null)} />}
    </section>
  );
}

/** Новый склад или правка: код, название, вид, отдел (у кладовой цеха), адрес, кладовщики, действует. */
function WarehouseForm({ view, warehouse, onClose }: { view: WarehousesView; warehouse: Warehouse | null; onClose: () => void }) {
  const t = useT();
  const router = useRouter();
  const [form, setForm] = useState({
    code: "",
    name: "",
    kind: "RawMaterials" as WarehouseKind,
    departmentId: "",
    address: "",
    isActive: true,
    keepers: [] as string[],
    owner: "None" as DataOwner,
    since: "",
  });
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    setError(null);
    setForm(
      warehouse
        ? {
            code: warehouse.code,
            name: warehouse.name,
            kind: warehouse.kind,
            departmentId: warehouse.departmentId ?? "",
            address: warehouse.address ?? "",
            isActive: warehouse.isActive,
            keepers: warehouse.keepers.map((k) => k.id),
            owner: warehouse.dataOwner ?? "None",
            since: warehouse.dataOwnerSince ?? "",
          }
        : { code: "", name: "", kind: "RawMaterials", departmentId: "", address: "", isActive: true, keepers: [], owner: "None", since: "" },
    );
  }, [warehouse]);

  // Закреплённый, у которого сняли право склада, остаётся в списке — чтобы было видно, кого снять.
  const people = [...view.keeperOptions, ...(warehouse?.keepers ?? []).filter((k) => !view.keeperOptions.some((o) => o.id === k.id))];
  const set = (patch: Partial<typeof form>) => setForm((f) => ({ ...f, ...patch }));
  const toggle = (id: string) => set({ keepers: form.keepers.includes(id) ? form.keepers.filter((k) => k !== id) : [...form.keepers, id] });

  const save = async () => {
    setError(null);
    if (!form.code.trim() || !form.name.trim()) {
      setError(t("Укажите код и название склада."));
      return;
    }
    setBusy(true);
    try {
      const body = JSON.stringify({
        code: form.code.trim(),
        name: form.name.trim(),
        kind: form.kind,
        departmentId: form.kind === "Workshop" && form.departmentId ? form.departmentId : null,
        address: form.address.trim() || null,
        isActive: form.isActive,
        keeperIds: form.keepers,
        version: warehouse?.version ?? null,
        dataOwner: form.owner,
        dataOwnerSince: form.owner === "None" ? null : form.since || null,
      });
      if (warehouse) await bff(`procurement/warehouses/${warehouse.id}`, { method: "PUT", body });
      else await bff("procurement/warehouses", { method: "POST", body });
      onClose();
      router.refresh();
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
    } finally {
      setBusy(false);
    }
  };

  return (
    <Sheet
      open
      onClose={onClose}
      title={warehouse ? t("Изменить склад") : t("Новый склад")}
      subtitle={warehouse?.name}
      size="xl"
      footer={
        <div className="flex justify-end gap-2">
          <button type="button" onClick={onClose} className={`${button} h-10`}>
            {t("Закрыть")}
          </button>
          <button type="button" onClick={() => void save()} disabled={busy} className={`${primary} h-10`}>
            {busy && <Loader2 className="size-4 animate-spin" aria-hidden />}
            {t("Сохранить")}
          </button>
        </div>
      }
    >
      <div className="grid gap-3 sm:grid-cols-2">
        <label className="block">
          <span className="mb-1.5 block text-sm font-medium text-ink">{t("Название")}</span>
          <input value={form.name} onChange={(e) => set({ name: e.target.value })} maxLength={100} className={field} autoFocus={!warehouse} />
        </label>
        <label className="block">
          <span className="mb-1.5 block text-sm font-medium text-ink">{t("Код")}</span>
          <input value={form.code} onChange={(e) => set({ code: e.target.value })} maxLength={32} placeholder="raw-1" className={field} />
        </label>
        <label className="block">
          <span className="mb-1.5 block text-sm font-medium text-ink">{t("Вид")}</span>
          <select value={form.kind} onChange={(e) => set({ kind: e.target.value as WarehouseKind })} className={field}>
            {kinds.map((k) => (
              <option key={k} value={k}>
                {t(warehouseKindLabel[k])}
              </option>
            ))}
          </select>
        </label>
        {form.kind === "Workshop" ? (
          <label className="block">
            <span className="mb-1.5 block text-sm font-medium text-ink">{t("Отдел (чья кладовая)")}</span>
            <select value={form.departmentId} onChange={(e) => set({ departmentId: e.target.value })} className={field}>
              <option value="">{t("Не указан")}</option>
              {view.departments.map((d) => (
                <option key={d.id} value={d.id}>
                  {d.name}
                </option>
              ))}
            </select>
          </label>
        ) : (
          <span className="hidden sm:block" />
        )}
        <label className="block sm:col-span-2">
          <span className="mb-1.5 block text-sm font-medium text-ink">
            {t("Адрес")} <span className="font-normal text-ink-3">{t("необязательно")}</span>
          </span>
          <input value={form.address} onChange={(e) => set({ address: e.target.value })} maxLength={300} className={field} />
        </label>
        <fieldset className="sm:col-span-2">
          <legend className="mb-1.5 text-sm font-medium text-ink">{t("Кладовщики склада")}</legend>
          {people.length === 0 ? (
            <p className="text-sm text-ink-3">{t("Нет пользователей с правом «Склад снабжения».")}</p>
          ) : (
            <div className="grid gap-1.5 sm:grid-cols-2">
              {people.map((p) => (
                <label key={p.id} className="flex items-center gap-2 text-sm text-ink">
                  <input type="checkbox" checked={form.keepers.includes(p.id)} onChange={() => toggle(p.id)} className="size-4 accent-[var(--color-accent)]" />
                  {p.name}
                </label>
              ))}
            </div>
          )}
          <p className="mt-1.5 text-xs text-ink-3">{t("Отмеченные проводят операции только по своим складам. Никого не отметить — склад доступен всем кладовщикам без закрепления.")}</p>
        </fieldset>
        <fieldset className="rounded-xl border border-line p-3 sm:col-span-2">
          <legend className="px-1 text-sm font-medium text-ink">{t("Кто ведёт данные склада")}</legend>
          <div className="grid gap-2 sm:grid-cols-[minmax(0,1fr)_170px]">
            <select value={form.owner} onChange={(e) => set({ owner: e.target.value as DataOwner })} aria-label={t("Кто ведёт данные склада")} className={field}>
              {(["None", "OneBase", "Sheet"] as const).map((o) => (
                <option key={o} value={o}>
                  {t(dataOwnerLabel[o])}
                </option>
              ))}
            </select>
            {form.owner !== "None" && (
              <input type="date" value={form.since} onChange={(e) => set({ since: e.target.value })} aria-label={t("С какой даты")} title={t("С какой даты")} className={field} />
            )}
          </div>
          <p className="mt-1.5 text-xs text-ink-3">
            {t("Переходный период: у склада один владелец данных, чтобы одну выдачу не провели дважды — из таблицы и вручную. «Таблица» — остатки ведёт загрузка журнала (файл или Google-таблица), вручную — только контроль качества. «OneBase» — загрузка закрыта. Дата не указана — с сегодня.")}
          </p>
        </fieldset>
        {warehouse && !warehouse.isDefault && (
          <label className="flex items-center gap-2 text-sm text-ink sm:col-span-2">
            <input type="checkbox" checked={form.isActive} onChange={(e) => set({ isActive: e.target.checked })} className="size-4 accent-[var(--color-accent)]" />
            {t("Склад действует — по нему проводят операции")}
          </label>
        )}
      </div>
      <div className="mt-3">
        <ErrorNote text={error} />
      </div>
    </Sheet>
  );
}
