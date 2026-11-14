import type { Dict } from "../core";
import agents from "./agents";
import ai from "./ai";
import analytik from "./analytik";
import simulator from "./simulator";
import explore from "./explore";
import arrivals from "./arrivals";
import campaigns from "./campaigns";
import common from "./common";
import dashboard from "./dashboard";
import dataManager from "./dataManager";
import field from "./field";
import fieldControl from "./fieldControl";
import files from "./files";
import home from "./home";
import hr from "./hr";
import lib from "./lib";
import myDay from "./myDay";
import production from "./production";
import recipeCards from "./recipeCards";
import recruitment from "./recruitment";
import screening from "./screening";
import requests from "./requests";
import rights from "./rights";
import sales from "./sales";
import salesPages from "./salesPages";
import serverAi from "./serverAi";
import serverField from "./serverField";
import serverSales from "./serverSales";
import serverSettings from "./serverSettings";
import settings from "./settings";
import tasks from "./tasks";
import technologist from "./technologist";
import telegramReports from "./telegramReports";
import twin from "./twin";
import whatsNew from "./whatsNew";
import forecasts from "./forecasts";
import telegramRecipients from "./telegramRecipients";

/**
 * Узбекский (латиница): ключ — русский текст интерфейса. Словари по разделам, чтобы их можно было вести независимо;
 * термины «Полевого контроля» — база, разделы перекрывают её. Одинаковый русский текст в разных разделах переводится одинаково.
 */
export const uz: Dict = {
  // «Мой день» и календарь — первыми: тот же текст в словарях разделов перекрывает перевод отсюда.
  ...myDay,
  ...requests,
  // «Аналитик»: свои термины; общие слова (период, факт, план) ниже перекрывают разделы, где они уже переведены.
  ...analytik,
  // Симулятор сценариев «Аналитика»: параметры, показатели, ограничения — после Аналитика (свои значения общих слов «База», «Мощность»).
  ...simulator,
  ...explore,
  // Цифровой двойник: общие слова переведены так же, как в разделах; разделы ниже перекрывают его.
  ...twin,
  ...whatsNew,
  ...forecasts,
  ...telegramRecipients,
  ...production,
  // «Технолог» — после производства: его термины (замес, этап, брак) перекрывают общие.
  ...technologist,
  // Список техкарт с фото продукта.
  ...recipeCards,
  // Поставки (приезд машины, качество до склада, приёмка) и главная по ролям.
  ...arrivals,
  // Права пользователя в разделах отделов и настройках.
  ...rights,
  ...fieldControl,
  // Тексты сервера (ошибки, пояснения, подписи) — переводятся по ключу или шаблону; словари интерфейса ниже перекрывают их.
  ...serverSales,
  ...serverField,
  ...serverAi,
  ...serverSettings,
  ...hr,
  ...recruitment,
  ...screening,
  ...agents,
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
