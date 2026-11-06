"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState } from "react";
import { Archive, Calculator, CircleCheckBig, CopyPlus, Factory, History, Loader2, Pencil } from "lucide-react";
import { Sheet } from "@/components/field/Sheet";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { ErrorNote } from "@/components/tasks/TaskBadges";
import { useT } from "@/i18n/client";
import { bff } from "@/lib/bff";
import { parseNumber, quantity, stampRu, type Warehouse } from "@/lib/procurement";
import { dayRu, recipeStatusLabel, requirement, type RecipeDetail, type Requirement } from "@/lib/production";
import { Fact, RecipeStatusPill, card, field, primary, secondary } from "./ProdKit";

/**
 * Карточка версии рецептуры: состав (норма на выход, потери, на 1 единицу выхода), расчёт потребности на нужное количество с доступным
 * на складе, версии кода, история. Технолог правит черновик, вводит его в действие, создаёт новую версию и отправляет действующую в архив;
 * производство создаёт по действующей задание на выпуск.
 */
export function RecipeDetailView({ recipe, warehouses }: { recipe: RecipeDetail; warehouses: Warehouse[] }) {
  const t = useT();
  const router = useRouter();
  const can = recipe.can;
  const [dialog, setDialog] = useState<"activate" | "archive" | null>(null);
  const [note, setNote] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [planned, setPlanned] = useState(String(recipe.outputQuantity).replace(".", ","));
  const [warehouse, setWarehouse] = useState("");
  const [need, setNeed] = useState<Requirement | null>(null);
  const [calcError, setCalcError] = useState<string | null>(null);
  const [calcBusy, setCalcBusy] = useState(false);
  const active = warehouses.filter((w) => w.isActive);

  const act = async (path: string, body?: object) => {
    setBusy(true);
    setError(null);
    try {
      const result = await bff<{ id?: string }>(`production/recipes/${recipe.id}/${path}`, { method: "POST", body: JSON.stringify(body ?? {}) });
      setDialog(null);
      if (path === "new-version" && result?.id) router.push(`/production/recipes/${result.id}/edit`);
      router.refresh();
    } catch (e) {
      setError(e instanceof Error ? e.message : String(e));
    } finally {
      setBusy(false);
    }
  };

  const calculate = async () => {
    const q = parseNumber(planned);
    if (q === null || q <= 0) return setCalcError(t("Укажите, сколько нужно выпустить."));
    setCalcBusy(true);
    setCalcError(null);
    try {
      setNeed(await bff<Requirement>(`production/recipes/${recipe.id}/requirement?quantity=${q}${warehouse ? `&warehouse=${warehouse}` : ""}`));
    } catch (e) {
      setCalcError(e instanceof Error ? e.message : String(e));
    } finally {
      setCalcBusy(false);
    }
  };

  const actions = (
    <div className="flex flex-wrap items-center gap-2">
      {can.archive && (
        <button type="button" onClick={() => setDialog("archive")} className={secondary}>
          <Archive className="size-4" aria-hidden /> {t("В архив")}
        </button>
      )}
      {can.newVersion && (
        <button type="button" onClick={() => void act("new-version")} disabled={busy} className={secondary}>
          <CopyPlus className="size-4" aria-hidden /> {t("Новая версия")}
        </button>
      )}
      {can.edit && (
        <Link href={`/production/recipes/${recipe.id}/edit`} className={secondary}>
          <Pencil className="size-4" aria-hidden /> {t("Изменить")}
        </Link>
      )}
      {can.activate && (
        <button type="button" onClick={() => setDialog("activate")} className={primary}>
          <CircleCheckBig className="size-4" aria-hidden /> {t("Ввести в действие")}
        </button>
      )}
      {can.order && (
        <Link href={`/production/orders/new?recipe=${recipe.id}`} className={primary}>
          <Factory className="size-4" aria-hidden /> {t("Задание на выпуск")}
        </Link>
      )}
    </div>
  );

  return (
    <>
      <PageHeader
        title={recipe.name}
        subtitle={
          <span className="inline-flex flex-wrap items-center gap-2">
            <span className="tabular-nums">
              {recipe.code} · {t("версия {n}", { n: recipe.versionNo })}
            </span>
            <RecipeStatusPill status={recipe.status} />
          </span>
        }
        back="/production/recipes"
        breadcrumbs={[
          { label: "OneBase", href: "/" },
          { label: t("Отделы"), href: "/departments" },
          { label: t("Производство"), href: "/production" },
          { label: t("Рецептуры"), href: "/production/recipes" },
          { label: `${recipe.code} v${recipe.versionNo}` },
        ]}
        actions={<span className="max-lg:hidden">{actions}</span>}
      />
      <PageBody>
        <div className="mx-auto max-w-6xl space-y-4">
          <div className="lg:hidden">{actions}</div>
          {recipe.status === "Draft" && (
            <p className="rounded-xl border border-warn/40 bg-warn-soft px-4 py-2.5 text-sm text-ink">
              {t("Черновик: по нему не создают задания. Проверьте состав и нажмите «Ввести в действие» — прежняя действующая версия уйдёт в архив.")}
            </p>
          )}
          <div className="grid gap-4 lg:grid-cols-[minmax(0,1fr)_340px]">
            <div className="min-w-0 space-y-4">
              <section className={card} aria-labelledby="rc-lines">
                <h2 id="rc-lines" className="mb-3 text-base font-semibold text-ink">
                  {t("Состав на {q}", { q: `${quantity(recipe.outputQuantity)} ${recipe.outputUnit}` })} <span className="font-normal text-ink-3">· {recipe.lines.length}</span>
                </h2>
                {recipe.lines.length === 0 ? (
                  <p className="text-sm text-ink-3">{t("Позиций пока нет — добавьте их в черновике.")}</p>
                ) : (
                  <div className="overflow-x-auto">
                    <table className="w-full min-w-[560px] text-sm">
                      <thead>
                        <tr className="whitespace-nowrap border-b border-line text-left text-[11px] uppercase tracking-wide text-ink-3">
                          <th className="py-2 pr-3 font-semibold">{t("Позиция склада")}</th>
                          <th className="py-2 pr-3 text-right font-semibold">{t("Норма")}</th>
                          <th className="py-2 pr-3 text-right font-semibold">{t("Потери")}</th>
                          <th className="py-2 text-right font-semibold">{t("На 1 {unit}", { unit: recipe.outputUnit })}</th>
                        </tr>
                      </thead>
                      <tbody>
                        {recipe.lines.map((l) => (
                          <tr key={l.id} className="border-b border-line align-top last:border-0">
                            <td className="py-2.5 pr-3 text-ink">
                              {l.name}
                              <span className="block text-[11px] text-ink-3">
                                <span className="tabular-nums">{l.sku}</span>
                                {l.tracksBatches && ` · ${t("по партиям")}`}
                                {!l.itemActive && <span className="text-bad"> · {t("отключена в каталоге")}</span>}
                              </span>
                            </td>
                            <td className="whitespace-nowrap py-2.5 pr-3 text-right tabular-nums">
                              {quantity(l.quantity)} {l.unitCode}
                            </td>
                            <td className="whitespace-nowrap py-2.5 pr-3 text-right tabular-nums text-ink-2">{l.lossPercent > 0 ? `${quantity(l.lossPercent)} %` : "—"}</td>
                            <td className="whitespace-nowrap py-2.5 text-right tabular-nums text-ink-2">
                              {quantity(requirement(l.quantity, 1, recipe.outputQuantity, l.lossPercent))} {l.unitCode}
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                )}
              </section>

              {recipe.lines.length > 0 && (
                <section className={card} aria-labelledby="rc-need">
                  <h2 id="rc-need" className="mb-1 flex items-center gap-2 text-base font-semibold text-ink">
                    <Calculator className="size-4 text-ink-3" aria-hidden /> {t("Потребность на выпуск")}
                  </h2>
                  <p className="mb-3 text-xs text-ink-3">{t("Сколько сырья нужно на нужное количество — с потерями, и сколько годного свободно на складе.")}</p>
                  <div className="flex flex-wrap items-end gap-2">
                    <label className="block">
                      <span className="mb-1 block text-xs font-medium text-ink-2">{t("Выпустить, {unit}", { unit: recipe.outputUnit })}</span>
                      <input value={planned} onChange={(e) => setPlanned(e.target.value)} inputMode="decimal" className={`${field} w-36 text-right tabular-nums`} />
                    </label>
                    {active.length > 1 && (
                      <label className="block">
                        <span className="mb-1 block text-xs font-medium text-ink-2">{t("Склад")}</span>
                        <select value={warehouse} onChange={(e) => setWarehouse(e.target.value)} className={`${field} w-56`}>
                          <option value="">{t("Все склады")}</option>
                          {active.map((w) => (
                            <option key={w.id} value={w.id}>
                              {w.name}
                            </option>
                          ))}
                        </select>
                      </label>
                    )}
                    <button type="button" onClick={() => void calculate()} disabled={calcBusy} className={primary}>
                      {calcBusy && <Loader2 className="size-4 animate-spin" aria-hidden />} {t("Рассчитать")}
                    </button>
                  </div>
                  <div className="mt-3">
                    <ErrorNote text={calcError} />
                  </div>
                  {need && (
                    <div className="mt-3 overflow-x-auto">
                      <table className="w-full min-w-[520px] text-sm">
                        <thead>
                          <tr className="whitespace-nowrap border-b border-line text-left text-[11px] uppercase tracking-wide text-ink-3">
                            <th className="py-2 pr-3 font-semibold">{t("Позиция")}</th>
                            <th className="py-2 pr-3 text-right font-semibold">{t("Нужно")}</th>
                            <th className="py-2 pr-3 text-right font-semibold">{t("Годного свободно")}</th>
                            <th className="py-2 text-right font-semibold">{t("Не хватает")}</th>
                          </tr>
                        </thead>
                        <tbody>
                          {need.lines.map((l) => (
                            <tr key={l.itemId} className="border-b border-line last:border-0">
                              <td className="py-2 pr-3 text-ink">{l.name}</td>
                              <td className="whitespace-nowrap py-2 pr-3 text-right tabular-nums">
                                {quantity(l.required)} {l.unitCode}
                              </td>
                              <td className="whitespace-nowrap py-2 pr-3 text-right tabular-nums text-ink-2">{quantity(l.available)}</td>
                              <td className={`whitespace-nowrap py-2 text-right tabular-nums ${l.shortage > 0 ? "font-semibold text-bad" : "text-ok"}`}>
                                {l.shortage > 0 ? quantity(l.shortage) : t("хватает")}
                              </td>
                            </tr>
                          ))}
                        </tbody>
                      </table>
                      <p className="mt-2 text-xs text-ink-3">{t("Склад: {name}. Годное — допущенные по качеству партии в сроке, за вычетом резерва заявок.", { name: need.warehouseName })}</p>
                    </div>
                  )}
                </section>
              )}
            </div>

            <div className="space-y-4">
              <section className={card}>
                <h2 className="mb-1 text-base font-semibold text-ink">{t("Сведения")}</h2>
                <dl className="divide-y divide-line">
                  <Fact label="Выход">
                    {recipe.outputName}
                    <span className="block text-xs tabular-nums text-ink-3">
                      {t("нормы на {q}", { q: `${quantity(recipe.outputQuantity)} ${recipe.outputUnit}` })}
                    </span>
                  </Fact>
                  {recipe.linkoProductName && <Fact label="Товар Linko">{recipe.linkoProductName}</Fact>}
                  {recipe.activatedAt && (
                    <Fact label="В действии с">
                      {stampRu(recipe.activatedAt)}
                      {recipe.activatedByName && <span className="block text-xs text-ink-3">{recipe.activatedByName}</span>}
                    </Fact>
                  )}
                  <Fact label="Заданий на выпуск">{recipe.orders}</Fact>
                  <Fact label="Создана">{stampRu(recipe.createdAt)}</Fact>
                </dl>
                {recipe.comment && <p className="mt-2 whitespace-pre-line rounded-lg bg-muted px-3 py-2 text-sm text-ink-2">{recipe.comment}</p>}
              </section>

              <section className={card}>
                <h2 className="mb-2 text-base font-semibold text-ink">{t("Версии")}</h2>
                <ul className="divide-y divide-line">
                  {recipe.versions.map((v) => (
                    <li key={v.id}>
                      <Link
                        href={`/production/recipes/${v.id}`}
                        aria-current={v.id === recipe.id ? "page" : undefined}
                        className={`flex items-center justify-between gap-2 py-2 text-sm ${v.id === recipe.id ? "font-semibold text-ink" : "text-accent-strong hover:underline"}`}
                      >
                        <span>
                          {t("Версия {n}", { n: v.versionNo })}
                          <span className="block text-xs font-normal text-ink-3">{v.activatedAt ? t("в действии с {date}", { date: dayRu(v.activatedAt) }) : dayRu(v.createdAt)}</span>
                        </span>
                        <span className="text-xs font-normal text-ink-3">{t(recipeStatusLabel[v.status])}</span>
                      </Link>
                    </li>
                  ))}
                </ul>
              </section>

              <section className={card}>
                <h2 className="mb-3 flex items-center gap-2 text-base font-semibold text-ink">
                  <History className="size-4 text-ink-3" aria-hidden /> {t("История")}
                </h2>
                <ol className="relative space-y-4 border-l border-line pl-4">
                  {[...recipe.history].reverse().map((e) => (
                    <li key={e.id} className="relative">
                      <span className="absolute -left-[21px] top-1.5 size-2.5 rounded-full bg-ink-4 ring-4 ring-surface" aria-hidden />
                      <p className="break-words text-sm text-ink">{t(e.text)}</p>
                      {e.comment && <p className="mt-1 whitespace-pre-line rounded-lg bg-muted px-2.5 py-1.5 text-sm text-ink-2">{e.comment}</p>}
                      <p className="mt-0.5 text-xs text-ink-3">
                        {e.actorName} · {stampRu(e.createdAt)}
                      </p>
                    </li>
                  ))}
                </ol>
              </section>
            </div>
          </div>
        </div>
      </PageBody>

      <Sheet
        open={dialog !== null}
        onClose={() => {
          setDialog(null);
          setError(null);
        }}
        title={dialog === "activate" ? t("Ввести в действие") : t("Отправить в архив")}
        subtitle={`${recipe.code} · ${t("версия {n}", { n: recipe.versionNo })}`}
        footer={
          <div className="flex justify-end gap-2">
            <button type="button" onClick={() => setDialog(null)} className={secondary}>
              {t("Закрыть")}
            </button>
            <button type="button" onClick={() => void act(dialog === "activate" ? "activate" : "archive", dialog === "archive" ? { comment: note.trim() || null } : undefined)} disabled={busy} className={primary}>
              {busy && <Loader2 className="size-4 animate-spin" aria-hidden />}
              {dialog === "activate" ? t("Ввести в действие") : t("В архив")}
            </button>
          </div>
        }
      >
        {dialog === "activate" ? (
          <p className="text-sm text-ink-2">
            {t("Версия {n} станет действующей: по ней создают задания на выпуск. Прежняя действующая версия уйдёт в архив; задания, созданные по ней, сохранят свои нормы.", {
              n: recipe.versionNo,
            })}
          </p>
        ) : (
          <label className="block">
            <span className="mb-1.5 block text-sm text-ink-2">{t("Действующей версии у кода не останется — новые задания по нему не создать, пока не введёте новую версию.")}</span>
            <textarea value={note} onChange={(e) => setNote(e.target.value)} maxLength={1000} rows={3} placeholder={t("Почему (необязательно)")} className="w-full rounded-lg border border-line bg-surface px-3 py-2.5 text-sm text-ink" />
          </label>
        )}
        <div className="mt-3">
          <ErrorNote text={error} />
        </div>
      </Sheet>
    </>
  );
}
