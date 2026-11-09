import { tNow } from "@/i18n/client";
import { bff } from "./bff";
import type { ImportAction, ImportRecipeStatus } from "./recipe-import-view";

export * from "./recipe-import-view";

/** Импорт техкарт из Excel (/api/production/recipe-imports): загрузки, предпросмотр, решения технолога, создание черновиков. */

export type ImportListRow = {
  id: string;
  fileName: string;
  fileSize: number;
  status: "Analyzed" | "Applied";
  createdAt: string;
  createdBy: string | null;
  appliedAt: string | null;
  recipes: number;
  ready: number;
  needsReview: number;
  applied: number;
};

export type ImportSheet = { name: string; hidden: boolean; hasRecipes: boolean; products: number; withComposition: number; notes: string[] };

export type ImportTotals = {
  recipes: number;
  withComposition: number;
  empty: number;
  duplicates: number;
  ready: number;
  needsReview: number;
  unchanged: number;
  applied: number;
  excluded: number;
  norms: number;
  zeros: number;
  negatives: number;
  errorCells: number;
  textCells: number;
  linesMatched: number;
  linesUnmatched: number;
  articlesUnmatched: number;
  semiFinished: number;
  blockers: number;
  warnings: number;
};

export type ImportRecipeRow = {
  id: string;
  sheet: string;
  column: string;
  article: string | null;
  name: string;
  code: string | null;
  baseQuantity: number | null;
  baseUnit: string | null;
  components: number;
  matched: number;
  unmatched: number;
  errors: number;
  status: ImportRecipeStatus;
  action: ImportAction;
  include: boolean;
  isSemiFinished: boolean;
  blockers: number;
  warnings: number;
  firstIssue: string | null;
  recipeId: string | null;
};

export type ImportView = {
  id: string;
  fileName: string;
  fileSize: number;
  fileHash: string;
  status: "Analyzed" | "Applied";
  createdAt: string;
  createdBy: string | null;
  appliedAt: string | null;
  sheets: ImportSheet[];
  totals: ImportTotals;
  recipes: ImportRecipeRow[];
  canEdit: boolean;
};

export type ImportIssue = { level: "block" | "warn" | "info"; code: string; text: string; cell: string | null };

export type ImportDecision = "None" | "Exclude" | "ManualValue" | "UnitConfirmed";

export type ImportLine = {
  id: string;
  row: number;
  cell: string;
  article: string | null;
  name: string | null;
  unit: string | null;
  weight: string | null;
  kind: "Number" | "Zero" | "Negative" | "Error" | "Text";
  raw: string | null;
  formula: string | null;
  value: number | null;
  text: string | null;
  itemId: string | null;
  itemSku: string | null;
  itemName: string | null;
  itemUnit: string | null;
  match: "None" | "Sku" | "Saved" | "Manual";
  decision: ImportDecision;
  manualValue: number | null;
  decisionComment: string | null;
  cardQuantity: number | null;
  cardUnit: string | null;
  outcome: string;
};

export type ImportChange = { kind: "added" | "removed" | "changed"; subject: string; before: string | null; after: string | null };

export type ImportRecipeView = {
  row: ImportRecipeRow;
  groupName: string | null;
  baseLabel: string | null;
  totalCell: string | null;
  totalRaw: string | null;
  totalFormula: string | null;
  lossCell: string | null;
  lossRaw: string | null;
  linkoProductId: number | null;
  linkoName: string | null;
  linkoSuggestedId: number | null;
  linkoSuggestedName: string | null;
  outputItemId: string | null;
  outputItemName: string | null;
  existingRecipeId: string | null;
  existingLabel: string | null;
  issues: ImportIssue[];
  lines: ImportLine[];
  cardChanges: ImportChange[];
  fileChanges: ImportChange[];
  fileChangesAgainst: string | null;
};

export type ImportMissing = { article: string; names: string[]; units: string[]; recipes: number; lines: number; semiFinished: boolean };

export type ImportApplyResult = {
  created: number;
  newVersions: number;
  failed: number;
  skipped: number;
  rows: { importRecipeId: string; name: string; outcome: "created" | "new_version" | "failed" | "skipped"; recipeId: string | null; error: string | null }[];
};

const base = "production/recipe-imports";
const json = (method: string, body?: unknown): RequestInit => ({ method, body: body === undefined ? undefined : JSON.stringify(body) });

export const importApi = {
  list: () => bff<ImportListRow[]>(base),
  get: (id: string) => bff<ImportView>(`${base}/${id}`),
  recipe: (id: string, recipeId: string) => bff<ImportRecipeView>(`${base}/${id}/recipes/${recipeId}`),
  updateRecipe: (id: string, recipeId: string, input: { code: string | null; linkoProductId: number | null; include: boolean }) =>
    bff<ImportRecipeView>(`${base}/${id}/recipes/${recipeId}`, json("PUT", input)),
  decide: (id: string, lineId: string, input: { itemId: string | null; decision: ImportDecision; manualValue: number | null; comment: string | null }) =>
    bff<ImportRecipeView>(`${base}/${id}/lines/${lineId}`, json("PUT", input)),
  map: (id: string, article: string, itemId: string) => bff<ImportView>(`${base}/${id}/map`, json("POST", { article, itemId })),
  missing: (id: string) => bff<ImportMissing[]>(`${base}/${id}/missing`),
  rematch: (id: string) => bff<ImportView>(`${base}/${id}/rematch`, json("POST")),
  apply: (id: string, recipeIds: string[] | null) => bff<ImportApplyResult>(`${base}/${id}/apply`, json("POST", { recipeIds })),
  /** multipart: Content-Type с границей ставит браузер. */
  upload: async (file: File) => {
    const form = new FormData();
    form.append("file", file);
    let response: Response;
    try {
      response = await fetch(`/bff/api/${base}`, { method: "POST", body: form });
    } catch {
      throw new Error(tNow("Нет связи с сервером OneBase — файл не загружен. Повторите."));
    }

    if (!response.ok) {
      const text = await response.text();
      let message = text;
      try {
        message = JSON.parse(text).error ?? text;
      } catch {
        // не JSON
      }
      throw new Error(response.status === 403 ? tNow("Недостаточно прав.") : response.status === 413 ? tNow("Файл слишком большой.") : message || `Ошибка ${response.status}`);
    }

    return (await response.json()) as { id: string; existing: boolean };
  },
};
