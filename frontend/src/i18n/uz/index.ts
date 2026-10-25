import type { Dict } from "../core";
import ai from "./ai";
import campaigns from "./campaigns";
import common from "./common";
import dashboard from "./dashboard";
import dataManager from "./dataManager";
import field from "./field";
import fieldControl from "./fieldControl";
import files from "./files";
import home from "./home";
import lib from "./lib";
import sales from "./sales";
import salesPages from "./salesPages";
import serverAi from "./serverAi";
import serverField from "./serverField";
import serverSales from "./serverSales";
import serverSettings from "./serverSettings";
import settings from "./settings";
import tasks from "./tasks";
import telegramReports from "./telegramReports";

/**
 * Узбекский (латиница): ключ — русский текст интерфейса. Словари по разделам, чтобы их можно было вести независимо;
 * термины «Полевого контроля» — база, разделы перекрывают её. Одинаковый русский текст в разных разделах переводится одинаково.
 */
export const uz: Dict = {
  ...fieldControl,
  // Тексты сервера (ошибки, пояснения, подписи) — переводятся по ключу или шаблону; словари интерфейса ниже перекрывают их.
  ...serverSales,
  ...serverField,
  ...serverAi,
  ...serverSettings,
  ...lib,
  ...common,
  ...home,
  ...dashboard,
  ...tasks,
  ...files,
  ...ai,
  ...dataManager,
  ...campaigns,
  ...telegramReports,
  ...settings,
  ...sales,
  ...salesPages,
  ...field,
};
