"use client";

import { useRouter } from "next/navigation";
import { useCallback, useEffect, useMemo, useState } from "react";
import { Building2, Loader2, Pencil, Trash2, UserRound, Users } from "lucide-react";
import { Button } from "@/components/ui";
import { useT } from "@/i18n/client";
import { filesApi, sharedAccessLabel, type Access, type DepartmentRef, type PrincipalType, type SharedAccessView } from "@/lib/files";
import { Modal } from "./Modal";

const levels: Exclude<Access, "none">[] = ["read", "write", "manage"];

/** «Доступ» у общей папки — для администратора и сотрудников с правом «Управление». */
export function SharedAccessButton({ space }: { space: DepartmentRef }) {
  const t = useT();
  const [open, setOpen] = useState(false);
  return (
    <>
      <Button onClick={() => setOpen(true)}>
        <Users className="size-4" />
        {t("Доступ")}
      </Button>
      {open && <SharedAccessDialog space={space} onClose={() => setOpen(false)} />}
    </>
  );
}

/**
 * Кому видна общая папка: сотрудникам и отделам — просмотр, редактирование или управление (управление — ещё и настройка доступа).
 * Изменения действуют сразу: права проверяются на сервере при каждом обращении, в том числе с диска Windows.
 */
function SharedAccessDialog({ space, onClose }: { space: DepartmentRef; onClose: () => void }) {
  const t = useT();
  const router = useRouter();
  const [view, setView] = useState<SharedAccessView | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [type, setType] = useState<"user" | "department">("user");
  const [principal, setPrincipal] = useState("");
  const [level, setLevel] = useState<Exclude<Access, "none">>("read");
  const [name, setName] = useState(space.name);

  const load = useCallback(() => {
    filesApi.sharedAccess(space.id).then(setView, (e: Error) => setError(e.message));
  }, [space.id]);
  useEffect(load, [load]);

  const run = async (action: () => Promise<unknown>) => {
    setBusy(true);
    setError(null);
    try {
      await action();
      load();
    } catch (e) {
      setError((e as Error).message);
    } finally {
      setBusy(false);
    }
  };

  const options = useMemo(() => {
    const taken = new Set(view?.grants.filter((g) => g.type === type).map((g) => g.id));
    return (type === "user" ? view?.users : view?.departments)?.filter((o) => !taken.has(o.id)) ?? [];
  }, [view, type]);

  const set = (grantType: PrincipalType, id: string, access: Access) => run(() => filesApi.setSharedAccess(space.id, grantType, id, access));

  return (
    <Modal title={t("Доступ к папке «{name}»", { name: space.name })} onClose={onClose} wide>
      {error && <p className="mb-3 rounded-lg bg-bad/10 px-3 py-2 text-sm text-bad">{t(error)}</p>}
      {!view && !error && <Loader2 className="size-5 animate-spin text-ink-3" />}
      {view && (
        <div className="grid gap-6 lg:grid-cols-[minmax(0,1.3fr)_minmax(0,1fr)]">
          <div className="space-y-3">
            <p className="text-sm text-ink-2">
              {t("Папку видят только те, кому выдан доступ. Права проверяются при каждом обращении — и на сайте, и с диска Windows.")}
            </p>
            {view.grants.length === 0 ? (
              <p className="rounded-lg border border-dashed border-line px-3 py-4 text-center text-sm text-ink-3">{t("Доступ пока никому не выдан")}</p>
            ) : (
              <div className="divide-y divide-line rounded-lg border border-line">
                {view.grants.map((g) => (
                  <div key={`${g.type}:${g.id}`} className="flex items-center gap-3 px-3 py-2 text-sm">
                    {g.type === "department" ? <Building2 className="size-4 shrink-0 text-ink-3" /> : <UserRound className="size-4 shrink-0 text-ink-3" />}
                    <span className="min-w-0 flex-1">
                      <span className="block truncate">{t(g.name)}</span>
                      {g.detail && <span className="block truncate text-xs text-ink-3">{t(g.detail)}</span>}
                    </span>
                    <select
                      className="rounded-md border border-line bg-surface px-2 py-1 text-xs"
                      value={g.access}
                      disabled={busy}
                      onChange={(e) => set(g.type, g.id, e.target.value as Access)}
                      aria-label={t("Доступ: {name}", { name: g.name })}
                    >
                      {levels.map((l) => (
                        <option key={l} value={l}>{t(sharedAccessLabel[l])}</option>
                      ))}
                      <option value="none">{t("убрать доступ")}</option>
                    </select>
                  </div>
                ))}
              </div>
            )}

            <div className="space-y-2 rounded-lg bg-muted/50 p-3">
              <p className="text-xs font-semibold uppercase tracking-wide text-ink-3">{t("Выдать доступ")}</p>
              <div className="flex flex-wrap gap-2">
                <select className="rounded-md border border-line bg-surface px-2 py-1.5 text-sm" value={type} onChange={(e) => (setType(e.target.value as "user" | "department"), setPrincipal(""))}>
                  <option value="user">{t("Сотруднику")}</option>
                  <option value="department">{t("Отделу")}</option>
                </select>
                <select className="min-w-0 flex-1 rounded-md border border-line bg-surface px-2 py-1.5 text-sm" value={principal} onChange={(e) => setPrincipal(e.target.value)}>
                  <option value="">{type === "user" ? t("Выберите сотрудника") : t("Выберите отдел")}</option>
                  {options.map((o) => (
                    <option key={o.id} value={o.id}>{o.detail ? `${t(o.name)} — ${t(o.detail)}` : t(o.name)}</option>
                  ))}
                </select>
                <select className="rounded-md border border-line bg-surface px-2 py-1.5 text-sm" value={level} onChange={(e) => setLevel(e.target.value as Exclude<Access, "none">)}>
                  {levels.map((l) => (
                    <option key={l} value={l}>{t(sharedAccessLabel[l])}</option>
                  ))}
                </select>
                <Button variant="primary" disabled={busy || !principal} onClick={() => set(type, principal, level).then(() => setPrincipal(""))}>
                  {t("Выдать")}
                </Button>
              </div>
            </div>
          </div>

          <div className="space-y-4">
            <ul className="space-y-1.5 rounded-lg bg-muted/60 p-3 text-xs text-ink-2">
              <li>{t("Просмотр — открыть и скачать файлы.")}</li>
              <li>{t("Редактирование — ещё загружать, менять, переносить и удалять в корзину (нужно право на запись файлов в роли).")}</li>
              <li>{t("Управление — ещё настраивать доступ к этой папке.")}</li>
              <li>{t("Отдел получает доступ для всех своих сотрудников и для подключения Windows этого отдела.")}</li>
            </ul>
            <div className="space-y-2">
              <p className="text-xs font-semibold uppercase tracking-wide text-ink-3">{t("Название папки")}</p>
              <div className="flex gap-2">
                <input className="min-w-0 flex-1 rounded-md border border-line bg-surface px-2 py-1.5 text-sm" value={name} onChange={(e) => setName(e.target.value)} />
                <Button disabled={busy || !name.trim() || name.trim() === space.name} onClick={() => run(async () => (await filesApi.renameShared(space.id, name.trim()), router.refresh()))}>
                  <Pencil className="size-4" />
                  {t("Переименовать")}
                </Button>
              </div>
              <p className="text-xs text-ink-3">{t("После переименования диск Windows открывает папку по новому имени.")}</p>
            </div>
            <Button
              disabled={busy}
              onClick={() =>
                window.confirm(t("Удалить общую папку «{name}»? Удалить можно только пустую — без файлов и папок, в том числе в корзине.", { name: space.name })) &&
                run(async () => (await filesApi.deleteShared(space.id), router.push("/base")))
              }
            >
              <Trash2 className="size-4" />
              {t("Удалить пустую папку")}
            </Button>
          </div>
        </div>
      )}
    </Modal>
  );
}
