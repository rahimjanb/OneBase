"use client";

import Link from "next/link";
import { useRouter } from "next/navigation";
import { useState } from "react";
import { AlertTriangle, Archive, BadgeCheck, Calculator, CopyPlus, Factory, GitCompare, History, Loader2, Pencil, Send, ShieldAlert, Undo2, Workflow } from "lucide-react";
import { Sheet } from "@/components/field/Sheet";
import { PageBody, PageHeader } from "@/components/shell/PageHeader";
import { ErrorNote } from "@/components/tasks/TaskBadges";
import { useT } from "@/i18n/client";
import { bff } from "@/lib/bff";
import { parseNumber, quantity, stampRu, type Warehouse } from "@/lib/procurement";
import {
  dayRu,
  defectBaseLabel,
  lineBasisLabel,
  recipeStatusLabel,
  stageKindLabel,
  type CalcResult,
  type RecipeComparison,
  type RecipeDetail,
  type RecipeLine,
  type Requirement,
} from "@/lib/production";
import { Fact, RecipeStatusPill, card, field, primary, secondary } from "./ProdKit";
import { RecipeCalcResult } from "./RecipeCalcResult";
import { RecipeChain } from "./RecipeChain";

type Dialog = "submit" | "approve" | "return" | "archive" | null;

/**
 * Технологическая карта (версия рецептуры): схема цепочки с массами на выход рецептуры, состав по этапам (норма, потери, на выход), виды
 * брака, расчёт на нужное количество (замесы, этапы, компоненты, склад), сведения карты, версии со сравнением, история. Технолог правит
 * черновик и отправляет на утверждение; другой технолог или начальник производства утверждает или возвращает с причиной; утверждённую —
 * новая версия или архив; производство создаёт по утверждённой задание.
 */
export function RecipeDetailView({ recipe, warehouses, base }: { recipe: RecipeDetail; warehouses: Warehouse[]; base: CalcResult | null }) {
  const t = useT();
  const router = useRouter();
  const can = recipe.can;
  const [dialog, setDialog] = useState<Dialog>(null);
  const [note, setNote] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [planned, setPlanned] = useState(String(recipe.outputQuantity).replace(".", ","));
  const [warehouse, setWarehouse] = useState("");
  const [need, setNeed] = useState<Requirement | null>(null);
  const [calcError, setCalcError] = useState<string | null>(null);
  const [calcBusy, setCalcBusy] = useState(false);
  const [compare, setCompare] = useState<RecipeComparison | null>(null);
  const [compareError, setCompareError] = useState<string | null>(null);
  const active = warehouses.filter((w) => w.isActive);
  const own = recipe.status === "PendingApproval" && !can.approve && can.return;
  const staged = recipe.stages.length > 0;
  const perBase = (l: RecipeLine) => base?.lines.find((x) => x.key === l.id);

  const act = async (path: string, body?: object) => {
    setBusy(true);
    setError(null);
    try {
      const result = await bff<{ id?: string }>(`production/recipes/${recipe.id}/${path}`, { method: "POST", body: JSON.stringify(body ?? {}) });
      setDialog(null);
      setNote("");
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

  // Слева — более ранняя версия: «было → стало».
  const openCompare = async (other: string) => {
    setCompareError(null);
    const otherNo = recipe.versions.find((v) => v.id === other)?.versionNo ?? 0;
    const [left, right] = otherNo < recipe.versionNo ? [other, recipe.id] : [recipe.id, other];
    try {
      setCompare(await bff<RecipeComparison>(`production/recipes/${left}/compare/${right}`));
    } catch (e) {
      setCompareError(e instanceof Error ? e.message : String(e));
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
      {can.return && (
        <button type="button" onClick={() => setDialog("return")} className={secondary}>
          <Undo2 className="size-4" aria-hidden /> {own ? t("Отозвать") : t("Вернуть")}
        </button>
      )}
      {can.submit && (
        <button type="button" onClick={() => setDialog("submit")} className={primary}>
          <Send className="size-4" aria-hidden /> {t("На утверждение")}
        </button>
      )}
      {can.approve && (
        <button type="button" onClick={() => setDialog("approve")} className={primary}>
          <BadgeCheck className="size-4" aria-hidden /> {t("Утвердить")}
        </button>
      )}
      {can.order && (
        <Link href={`/production/orders/new?recipe=${recipe.id}`} className={primary}>
          <Factory className="size-4" aria-hidden /> {t("Задание на выпуск")}
        </Link>
      )}
    </div>
  );

  const banner = (() => {
    if (recipe.status === "Draft" && recipe.problems.length > 0)
      return (
        <div className="rounded-xl border border-warn/40 bg-warn-soft px-4 py-2.5 text-sm text-ink">
          <p className="flex items-center gap-2 font-semibold">
            <AlertTriangle className="size-4 text-warn" aria-hidden /> {t("Карта пока не считается — на утверждение не отправить")}
          </p>
          <ul className="mt-1 list-disc space-y-0.5 pl-5">
            {recipe.problems.map((p) => (
              <li key={p}>{t(p)}</li>
            ))}
          </ul>
        </div>
      );
    if (recipe.status === "Draft")
      return (
        <p className="rounded-xl border border-warn/40 bg-warn-soft px-4 py-2.5 text-sm text-ink">
          {t("Черновик: по нему не создают задания. Проверьте карту и отправьте на утверждение — утвердит другой технолог или начальник производства.")}
        </p>
      );
    if (recipe.status === "PendingApproval")
      return (
        <div className="rounded-xl border border-violet/30 bg-violet-soft px-4 py-2.5 text-sm text-ink">
          <p>
            {t("На утверждении с {date}", { date: recipe.submittedAt ? stampRu(recipe.submittedAt) : "—" })}
            {recipe.submittedByName && ` · ${t("отправил(а) {name}", { name: recipe.submittedByName })}`}
          </p>
          {recipe.approvers.length > 0 ? (
            <p className="mt-0.5 text-xs text-ink-2">{t("Утвердить могут: {names}", { names: recipe.approvers.join(", ") })}</p>
          ) : (
            <p className="mt-0.5 text-xs text-warn">
              {t("Утвердить некому: нужен второй технолог или руководитель отдела «Производство».")}{" "}
              <Link href="/settings/users" className="font-medium underline">
                {t("Пользователи")}
              </Link>
            </p>
          )}
        </div>
      );
    return null;
  })();

  return (
    <>
      <PageHeader
        title={recipe.name}
        subtitle={
          <span className="inline-flex flex-wrap items-center gap-2">
            <span className="tabular-nums">
              {recipe.code} · {t("версия {n}", { n: recipe.versionNo })}
              {recipe.techCardNo && ` · ${t("карта {no}", { no: recipe.techCardNo })}`}
            </span>
            <RecipeStatusPill status={recipe.status} />
          </span>
        }
        back="/production/recipes"
        breadcrumbs={[
          { label: "OneBase", href: "/" },
          { label: t("Отделы"), href: "/departments" },
          { label: t("Производство"), href: "/production" },
          { label: t("Технолог"), href: "/production/technologist" },
          { label: t("Техкарты"), href: "/production/recipes" },
          { label: `${recipe.code} v${recipe.versionNo}` },
        ]}
        actions={<span className="max-lg:hidden">{actions}</span>}
      />
      <PageBody>
        <div className="mx-auto max-w-6xl space-y-4">
          <div className="lg:hidden">{actions}</div>
          {banner}

          {staged && (
            <section className={card} aria-labelledby="rc-chain">
              <h2 id="rc-chain" className="mb-1 flex items-center gap-2 text-base font-semibold text-ink">
                <Workflow className="size-4 text-ink-3" aria-hidden /> {t("Технологическая цепочка")}
              </h2>
              <p className="mb-3 text-xs text-ink-3">{t("Массы — на {q} готовой продукции, по нормативам выхода и брака каждого этапа.", { q: `${quantity(recipe.outputQuantity)} ${recipe.outputUnit}` })}</p>
              <RecipeChain recipe={recipe} calc={base} />
            </section>
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
                    <table className="w-full min-w-[600px] text-sm">
                      <thead>
                        <tr className="whitespace-nowrap border-b border-line text-left text-[11px] text-ink-3">
                          <th className="py-2 pr-3 font-semibold">{t("Компонент")}</th>
                          <th className="py-2 pr-3 text-right font-semibold">{t("Норма")}</th>
                          <th className="py-2 pr-3 text-right font-semibold">{t("Потери")}</th>
                          <th className="py-2 text-right font-semibold">{t("На {q}", { q: `${quantity(recipe.outputQuantity)} ${recipe.outputUnit}` })}</th>
                        </tr>
                      </thead>
                      {(staged ? recipe.stages : [null]).map((s) => {
                        const rows = recipe.lines.filter((l) => (s ? l.stageId === s.id : true));
                        return (
                          <tbody key={s?.id ?? "all"}>
                            {s && (
                              <tr>
                                <th colSpan={4} scope="rowgroup" className="pb-1 pt-3 text-left text-xs font-semibold text-ink-2">
                                  {s.stageNo}. {s.name}
                                  <span className="font-normal text-ink-3">
                                    {t(stageKindLabel[s.kind]) !== s.name && ` · ${t(stageKindLabel[s.kind])}`}
                                    {s.kind === "Mixing" && recipe.standardBatchKg !== null && ` · ${t("замес {kg} кг", { kg: quantity(recipe.standardBatchKg) })}`}
                                  </span>
                                </th>
                              </tr>
                            )}
                            {rows.length === 0 && s && (
                              <tr>
                                <td colSpan={4} className="py-1.5 text-xs text-ink-3">
                                  {t("Без компонентов — обрабатывается поток предыдущего этапа.")}
                                </td>
                              </tr>
                            )}
                            {rows.map((l) => {
                              const calc = perBase(l);
                              const material = recipe.defectTypes.filter((d) => d.itemId === l.itemId && d.stageId === l.stageId).reduce((sum, d) => sum + d.normPercent, 0);
                              return (
                                <tr key={l.id} className="border-b border-line align-top last:border-0">
                                  <td className="py-2.5 pr-3 text-ink">
                                    {l.name}
                                    <span className="block text-[11px] text-ink-3">
                                      <span className="tabular-nums">{l.sku}</span>
                                      {l.group && ` · ${l.group}`}
                                      {l.category && ` · ${l.category}`}
                                      {l.tracksBatches && ` · ${t("по партиям")}`}
                                      {!l.itemActive && <span className="text-bad"> · {t("отключена в каталоге")}</span>}
                                    </span>
                                  </td>
                                  <td className="whitespace-nowrap py-2.5 pr-3 text-right tabular-nums">
                                    {quantity(l.quantity)} {l.lineUnit}
                                    {l.basis !== "Output" && <span className="block text-[11px] text-ink-3">{t(lineBasisLabel[l.basis])}</span>}
                                  </td>
                                  <td className="whitespace-nowrap py-2.5 pr-3 text-right tabular-nums text-ink-2">
                                    {l.lossPercent + material > 0 ? `${quantity(l.lossPercent + material)} %` : "—"}
                                    {material > 0 && <span className="block text-[11px] text-ink-3">{t("в т. ч. брак {p} %", { p: quantity(material) })}</span>}
                                  </td>
                                  <td className="whitespace-nowrap py-2.5 text-right tabular-nums text-ink-2">
                                    {calc ? (
                                      <>
                                        {quantity(calc.gross)} {calc.unit}
                                        {calc.unit !== calc.itemUnit && <span className="block text-[11px] text-ink-3">= {quantity(calc.itemGross)} {calc.itemUnit}</span>}
                                      </>
                                    ) : (
                                      "—"
                                    )}
                                  </td>
                                </tr>
                              );
                            })}
                          </tbody>
                        );
                      })}
                    </table>
                  </div>
                )}
              </section>

              {staged && (
                <section className={card} aria-labelledby="rc-defects">
                  <h2 id="rc-defects" className="mb-1 flex items-center gap-2 text-base font-semibold text-ink">
                    <ShieldAlert className="size-4 text-ink-3" aria-hidden /> {t("Брак по этапам")}
                  </h2>
                  <p className="mb-3 text-xs text-ink-3">{t("Брак продукта на этапе учитывает материалы этого и прежних этапов; брак материала — только расход этого материала.")}</p>
                  <ul className="divide-y divide-line">
                    {recipe.stages.map((s) => {
                      const types = recipe.defectTypes.filter((d) => d.stageId === s.id);
                      return (
                        <li key={s.id} className="py-2.5 text-sm">
                          <div className="flex flex-wrap items-baseline justify-between gap-2">
                            <span className="font-medium text-ink">
                              {s.stageNo}. {s.name}
                            </span>
                            <span className="text-xs text-ink-3">
                              {s.defectPercent > 0 ? t("брак продукта {p} %", { p: quantity(s.defectPercent) }) : t("брака продукта нет")} · {t(defectBaseLabel[s.defectBase])}
                              {s.reworkable && ` · ${t("перерабатывается")}`}
                            </span>
                          </div>
                          {types.length > 0 && (
                            <ul className="mt-1 flex flex-wrap gap-1.5">
                              {types.map((d) => (
                                <li key={d.id} className="rounded-md bg-muted px-2 py-0.5 text-xs text-ink-2">
                                  <span className="text-ink">{d.name}</span> · {quantity(d.normPercent)} %{d.itemName && ` · ${t("материал: {name}", { name: d.itemName })}`}
                                  {d.reworkable && ` · ${t("в переработку")}`}
                                </li>
                              ))}
                            </ul>
                          )}
                        </li>
                      );
                    })}
                  </ul>
                </section>
              )}

              {recipe.lines.length > 0 && (
                <section className={card} aria-labelledby="rc-need">
                  <h2 id="rc-need" className="mb-1 flex items-center gap-2 text-base font-semibold text-ink">
                    <Calculator className="size-4 text-ink-3" aria-hidden /> {t("Расчёт на выпуск")}
                  </h2>
                  <p className="mb-3 text-xs text-ink-3">
                    {t("Сколько сырья, замесов, начинки, глазури и упаковки нужно на нужное количество годной продукции — по цепочке, и сколько годного свободно на складе.")}
                  </p>
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
                  {need?.calculation && (
                    <div className="mt-3">
                      <RecipeCalcResult calc={need.calculation} need={need.lines} warehouseName={need.warehouseName} />
                    </div>
                  )}
                </section>
              )}
            </div>

            <div className="space-y-4">
              <section className={card}>
                <h2 className="mb-1 text-base font-semibold text-ink">{t("Карточка продукта")}</h2>
                <dl className="divide-y divide-line">
                  <Fact label="Выход">
                    {recipe.outputName}
                    <span className="block text-xs tabular-nums text-ink-3">{t("нормы на {q}", { q: `${quantity(recipe.outputQuantity)} ${recipe.outputUnit}` })}</span>
                  </Fact>
                  {recipe.linkoProductName && (
                    <Fact label="Товар Linko">
                      {recipe.linkoProductName}
                      {(recipe.productCode || recipe.productGroup) && (
                        <span className="block text-xs text-ink-3">{[recipe.productCode, recipe.productGroup].filter(Boolean).join(" · ")}</span>
                      )}
                    </Fact>
                  )}
                  {recipe.techCardNo && <Fact label="Номер карты">{recipe.techCardNo}</Fact>}
                  {recipe.outputItemName && (
                    <Fact label="Выход на склад">
                      {recipe.outputItemName}
                      <span className="block text-xs text-ink-3">{t("приходуется при закрытии задания")}</span>
                    </Fact>
                  )}
                  <Fact label="Технолог">{recipe.technologistName ?? "—"}</Fact>
                  <Fact label="Действует с">{recipe.effectiveFrom ? dayRu(recipe.effectiveFrom) : "—"}</Fact>
                  {recipe.activatedAt && (
                    <Fact label="Утверждена">
                      {stampRu(recipe.activatedAt)}
                      {recipe.activatedByName && <span className="block text-xs text-ink-3">{recipe.activatedByName}</span>}
                    </Fact>
                  )}
                  {recipe.standardBatchKg !== null && (
                    <Fact label="Замес">
                      {quantity(recipe.standardBatchKg)} {t("кг")}
                      <span className="block text-xs text-ink-3">
                        {t("от {min} до {max} кг", { min: quantity(recipe.batchMinKg ?? recipe.standardBatchKg), max: quantity(recipe.batchMaxKg ?? recipe.standardBatchKg) })}
                        {recipe.partialBatches && ` · ${t("частичный — можно")}`}
                      </span>
                    </Fact>
                  )}
                  {(recipe.packKg !== null || recipe.pieceKg !== null) && (
                    <Fact label="Упаковка">
                      {recipe.packKg !== null && t("упаковка {kg} кг", { kg: quantity(recipe.packKg) })}
                      {recipe.packsPerBox !== null && ` · ${t("в коробке {n}", { n: recipe.packsPerBox })}`}
                      {recipe.pieceKg !== null && <span className="block text-xs text-ink-3">{t("штука {kg} кг", { kg: quantity(recipe.pieceKg) })}</span>}
                    </Fact>
                  )}
                  <Fact label="Заданий на выпуск">{recipe.orders}</Fact>
                  <Fact label="Создана">{stampRu(recipe.createdAt)}</Fact>
                </dl>
                {!staged && recipe.comment?.includes("Требуется распределение по этапам технологом") && (
                  <p className="mt-2 rounded-lg bg-warn-soft px-3 py-2 text-sm text-warn">
                    {t("Импортирована из Excel: состав без этапов. Распределите компоненты по этапам (замес, выпечка, начинка, глазурь, упаковка…) — в черновике или новой версии.")}
                  </p>
                )}
                {recipe.comment && <p className="mt-2 whitespace-pre-line rounded-lg bg-muted px-3 py-2 text-sm text-ink-2">{recipe.comment}</p>}
              </section>

              <section className={card}>
                <h2 className="mb-2 text-base font-semibold text-ink">{t("Версии")}</h2>
                <ul className="divide-y divide-line">
                  {recipe.versions.map((v) => (
                    <li key={v.id} className="flex items-center justify-between gap-2 py-2 text-sm">
                      <Link
                        href={`/production/recipes/${v.id}`}
                        aria-current={v.id === recipe.id ? "page" : undefined}
                        className={v.id === recipe.id ? "font-semibold text-ink" : "text-accent-strong hover:underline"}
                      >
                        {t("Версия {n}", { n: v.versionNo })}
                        <span className="block text-xs font-normal text-ink-3">
                          {t(recipeStatusLabel[v.status])} · {v.activatedAt ? t("утверждена {date}", { date: dayRu(v.activatedAt) }) : dayRu(v.createdAt)}
                        </span>
                      </Link>
                      {v.id !== recipe.id && (
                        <button type="button" onClick={() => void openCompare(v.id)} className="inline-flex items-center gap-1 rounded-md px-2 py-1 text-xs text-ink-2 hover:bg-muted">
                          <GitCompare className="size-3.5" aria-hidden /> {t("Сравнить")}
                        </button>
                      )}
                    </li>
                  ))}
                </ul>
                <ErrorNote text={compareError} />
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
                      {e.comment && <p className="mt-1 max-h-40 overflow-y-auto whitespace-pre-line rounded-lg bg-muted px-2.5 py-1.5 text-xs text-ink-2">{e.comment}</p>}
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
        title={
          dialog === "submit"
            ? t("Отправить на утверждение")
            : dialog === "approve"
              ? t("Утвердить версию")
              : dialog === "return"
                ? own
                  ? t("Отозвать с утверждения")
                  : t("Вернуть на доработку")
                : t("Отправить в архив")
        }
        subtitle={`${recipe.code} · ${t("версия {n}", { n: recipe.versionNo })}`}
        footer={
          <div className="flex justify-end gap-2">
            <button type="button" onClick={() => setDialog(null)} className={secondary}>
              {t("Закрыть")}
            </button>
            <button
              type="button"
              onClick={() =>
                void act(dialog === "submit" ? "submit" : dialog === "approve" ? "approve" : dialog === "return" ? "return" : "archive", dialog === "return" || dialog === "archive" ? { comment: note.trim() || null } : undefined)
              }
              disabled={busy || (dialog === "return" && !own && !note.trim())}
              className={primary}
            >
              {busy && <Loader2 className="size-4 animate-spin" aria-hidden />}
              {dialog === "submit" ? t("Отправить") : dialog === "approve" ? t("Утвердить") : dialog === "return" ? (own ? t("Отозвать") : t("Вернуть")) : t("В архив")}
            </button>
          </div>
        }
      >
        {dialog === "submit" && (
          <p className="text-sm text-ink-2">{t("Версия перестанет правиться. Утвердит другой технолог или начальник производства — им придёт уведомление.")}</p>
        )}
        {dialog === "approve" && (
          <p className="text-sm text-ink-2">
            {t("Версия {n} станет действующей: по ней создают задания на выпуск. Прежняя утверждённая версия уйдёт в архив; задания, созданные по ней, сохранят свои расчёты.", { n: recipe.versionNo })}
          </p>
        )}
        {(dialog === "return" || dialog === "archive") && (
          <label className="block">
            <span className="mb-1.5 block text-sm text-ink-2">
              {dialog === "archive"
                ? t("Утверждённой версии у кода не останется — новые задания по нему не создать, пока не утвердят новую версию.")
                : own
                  ? t("Версия вернётся в черновик — её можно будет снова править.")
                  : t("Версия вернётся автору в черновик. Напишите, что исправить.")}
            </span>
            <textarea value={note} onChange={(e) => setNote(e.target.value)} maxLength={1000} rows={3}
              placeholder={dialog === "return" && !own ? t("Что исправить") : t("Почему (необязательно)")} className="w-full rounded-lg border border-line bg-surface px-3 py-2.5 text-sm text-ink" />
          </label>
        )}
        <div className="mt-3">
          <ErrorNote text={error} />
        </div>
      </Sheet>

      <Sheet
        open={compare !== null}
        onClose={() => setCompare(null)}
        title={t("Сравнение версий")}
        subtitle={compare ? t("версия {a} → версия {b}", { a: compare.leftVersion, b: compare.rightVersion }) : undefined}
      >
        {compare && compare.changes.length === 0 && <p className="text-sm text-ink-3">{t("Версии не отличаются.")}</p>}
        {compare && compare.changes.length > 0 && (
          <ul className="divide-y divide-line">
            {compare.changes.map((c, i) => (
              <li key={i} className="py-2.5 text-sm">
                <p className="text-xs text-ink-3">
                  {t(c.section)} · {c.subject}
                </p>
                <p className="font-medium text-ink">{t(c.field)}</p>
                <p className="mt-0.5 grid grid-cols-2 gap-2 text-xs">
                  <span className="rounded-md bg-bad-soft px-2 py-1 text-ink-2 line-through decoration-bad/50">{c.before ?? "—"}</span>
                  <span className="rounded-md bg-ok-soft px-2 py-1 text-ink">{c.after ?? "—"}</span>
                </p>
              </li>
            ))}
          </ul>
        )}
      </Sheet>
    </>
  );
}
