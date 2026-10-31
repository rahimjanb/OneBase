import type { Dict } from "./core";
import { toCyrillic } from "./cyrillic";
import { uz } from "./uz";

/**
 * Исправления узбекской кириллицы, где правило транслитерации даёт не ту форму (ключ — русский текст, значение — готовый перевод
 * кириллицей). Обычно не нужны: новые переводы пишутся латиницей в uz/*, кириллица появляется сама.
 */
const overrides: Dict = {};

/** Узбекский (кириллица): латинский словарь, переведённый в кириллицу по правилам орфографии, с исправлениями поверх. */
export const uzCyrl: Dict = { ...Object.fromEntries(Object.entries(uz).map(([key, value]) => [key, toCyrillic(value)])), ...overrides };
