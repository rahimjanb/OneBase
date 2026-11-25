// Окно «Что нового» после обновления 1Base: содержимое релиза и правила показа. Тексты — по-русски, перевод — словарь i18n/uz/whatsNew.ts
// (интерфейс переводит их через t). Окно — components/whats-new/WhatsNew.tsx; отметку «просмотрено» хранит сервер (POST /api/auth/whats-new).
// Тест: node --test src/lib/whats-new.test.mjs.
import { canOpen } from "./navigation.ts";

export type WhatsNewTag = "new_section" | "new" | "improved";
export type WhatsNewColor = "violet" | "accent" | "ok" | "warn" | "cyan";
export type WhatsNewIcon = "battle" | "salesData" | "agentDay" | "assortment" | "forecast" | "screening" | "recipients";

/** Кому видна карточка: раздел меню (canOpen), любое из прав или «ИИ-агенты» (отделы, агентами которых управляет пользователь). */
export type WhatsNewNeed = { section?: string; anyOf?: string[]; agents?: boolean };

export type WhatsNewStep = {
  id: string;
  /** Короткое имя — список шагов слева. */
  title: string;
  tag: WhatsNewTag;
  /** Иконка шага и схема превью (без данных: только подписи и формы). */
  icon: WhatsNewIcon;
  color: WhatsNewColor;
  heading: string;
  description: string;
  /** Описание на телефоне — короче. */
  descriptionShort: string;
  bullets: string[];
  /** «Где найти»: последний чип — ссылка на route. */
  path: string[];
  route: string;
  /** Подпись превью для экранного диктора. */
  alt: string;
  need: WhatsNewNeed;
};

export type WhatsNewRelease = {
  /** Ключ «показано / не показано»: дата релиза, второе обновление за день — «2026-11-13.2» (сервер: WhatsNew.Normalize). */
  version: string;
  /** День релиза (Ташкент): пользователям, созданным с этого дня, окно не показывается — для них всё и так новое. */
  date: string;
  steps: WhatsNewStep[];
};

export const TAG_LABEL: Record<WhatsNewTag, string> = { new_section: "Новый раздел", new: "Новое", improved: "Улучшено" };

/** Текущий релиз. Новый релиз — новая версия и свои шаги: окно появится у всех ещё раз. Прошлые релизы — в docs/whats-new.md. */
export const CURRENT_RELEASE: WhatsNewRelease = {
  version: "2026-11-25",
  date: "2026-11-25",
  steps: [
    {
      id: "battle",
      title: "Поле боя: цели на маршрут",
      tag: "new",
      icon: "battle",
      color: "warn",
      heading: "Могул и Twizos: цель на каждую точку",
      description:
        "Спящей точке ТП несёт цель Могул, клиенту — набор Twizos или стойку. Кешбек Могул и Twizos считается отдельно по каждому заказу Linko.",
      descriptionShort: "Цели Могул и Twizos на точки маршрута, два кешбека и стойки по регионам.",
      bullets: [
        "Маршрут и цели ТП на день с отметками выполнения",
        "Кешбек Могул и Twizos — отдельно, по каждому заказу",
        "Стойки Twizos: цель региона и остаток на складе",
        "Карточки Telegram — посмотреть и скачать PNG",
      ],
      path: ["Продажи", "Поле боя"],
      route: "/sales/battle",
      alt: "Маршрут ТП: точки с целями Могул, Twizos и стойка, два кешбека",
      need: { section: "/sales" },
    },
    {
      id: "sales-linko",
      title: "Продажи: цифры как в Linko",
      tag: "improved",
      icon: "salesData",
      color: "ok",
      heading: "План АКБ, объём и бронь — как в Linko",
      description:
        "План АКБ берётся из Linko вместо общей цели 125, а «Тотал» больше не считается второй раз вместе с Могулем и Twizos. Бронь склада — только подтверждённые перемещения. Cripzel теперь отдельная категория, а не часть Придзела.",
      descriptionShort: "План АКБ из Linko, объём без двойного счёта, бронь и Cripzel отдельно.",
      bullets: [
        "План АКБ из Linko у РМ, регионов и ТП",
        "«Из чего складывается объём» на странице региона",
        "Бронь — только подтверждённые перемещения, в автозаказе — бронь дилера",
        "Cripzel — отдельная категория в продажах, акциях и отчётах",
      ],
      path: ["Продажи", "Вторичка"],
      route: "/sales",
      alt: "Объём по категориям с планом АКБ из Linko, Cripzel — отдельной строкой",
      need: { section: "/sales" },
    },
    {
      id: "agent-day",
      title: "День ТП",
      tag: "new",
      icon: "agentDay",
      color: "cyan",
      heading: "Куда ходил ТП и что продал за день",
      description:
        "Кнопка «День ТП» на странице агента показывает, куда ТП ходил по порядку отметок и что продал. Время на точке Linko не передаёт — показаны минуты между отметками.",
      descriptionShort: "Визиты по порядку, продажи, пропущенные точки и номер визита после заказа.",
      bullets: [
        "Отметки визитов по порядку и минуты между ними",
        "Что продал и что точка обычно берёт, но не взяла",
        "Пропущенные точки маршрута и заказы без визита",
        "Какой это визит после прошлого заказа точки",
      ],
      path: ["Продажи", "Агенты"],
      route: "/sales/agents",
      alt: "День ТП: визиты по порядку с отметками «визит», «не посетил», «заказ без визита»",
      need: { section: "/sales" },
    },
    {
      id: "assortment",
      title: "Ассортимент с фото",
      tag: "improved",
      icon: "assortment",
      color: "accent",
      heading: "Артикулы категории — с фото",
      description:
        "Экран категории показывает артикулы таблицей или карточками с фото, на телефоне — сеткой или списком. Фото открывается на весь экран, на странице артикула — крупное фото.",
      descriptionShort: "Артикулы категории таблицей или карточками с фото.",
      bullets: ["Таблица или карточки с фото, на телефоне — сетка или список", "Фото на весь экран по нажатию", "Поиск по названию и артикулу"],
      path: ["Продажи", "Ассортимент"],
      route: "/sales/assortment",
      alt: "Артикулы категории карточками с фото",
      need: { section: "/sales" },
    },
    {
      id: "analytik",
      title: "Аналитик: прогнозы и исследования",
      tag: "improved",
      icon: "forecast",
      color: "violet",
      heading: "Прогноз с диапазоном и точностью",
      description:
        "«Прогнозы» показывают сценарии P10, P50 и P90 и как прогноз сбывался в прошлые месяцы. Сложный вопрос Аналитик разбирает исследованием и сам достаёт нужные цифры запросом к базе.",
      descriptionShort: "Прогнозы со сценариями и точностью, исследования сложных вопросов.",
      bullets: [
        "Диапазон 80 % и точность прошлых месяцев",
        "Исследования и решения — свои экраны",
        "Корреляции, регрессии и точки перелома",
        "Запросы к базе — только чтение, в пределах ваших прав",
      ],
      path: ["Аналитик", "Прогнозы"],
      route: "/analytik/forecasts",
      alt: "График прогноза с диапазоном и отметкой «Сегодня»",
      need: { section: "/analytik" },
    },
    {
      id: "screening",
      title: "AI-скрининг кандидатов",
      tag: "new",
      icon: "screening",
      color: "accent",
      heading: "AI оценивает резюме и приводит цитаты",
      description:
        "Скрининг оценивает резюме по шести критериям с весами и дословными цитатами из резюме. Баллы и категорию считает сервер: без подтверждённых фактов высокой оценки не бывает, а защищённые признаки модель не видит.",
      descriptionShort: "Оценка резюме по критериям с цитатами, баллы считает сервер.",
      bullets: ["Шесть критериев с весами и цитатами из резюме", "Статусы скрининга с отметками HR и сравнение кандидатов", "Резюме из HH и импорт файлов"],
      path: ["HR", "Подбор", "AI-скрининг"],
      route: "/hr/recruitment/screening",
      alt: "Оценка кандидата: критерии, цитата из резюме и категория",
      need: { anyOf: ["hr.recruitment.read", "hr.recruitment.manage"] },
    },
    {
      id: "telegram",
      title: "Автоотчёты: получатели и категории",
      tag: "improved",
      icon: "recipients",
      color: "cyan",
      heading: "Кто какие отчёты получает — на одном экране",
      description:
        "«Получатели» показывают отчёты и время отправки каждого получателя, а под именем — почему отчёт не уйдёт. Новый отчёт «Итоги дня по категориям» присылает продажи выбранных категорий и товаров за день.",
      descriptionShort: "«Получатели» с массовыми действиями и отчёт «Итоги дня по категориям».",
      bullets: [
        "Фильтры, массовые действия и отключение с причиной",
        "РМ — пользователь с ролью «РМ», регионы из профиля",
        "«Итоги дня по категориям»: свои категории и товары",
        "«Поле боя»: семь карточек ТП, СВР и РМ — файлом без сжатия",
      ],
      path: ["Telegram", "Автоотчёты", "Получатели"],
      route: "/settings/telegram/reports?tab=recipients",
      alt: "Получатели автоотчётов: отчёты, время и причина пропуска",
      // Автоотчёты строятся по данным «Продаж»: без них страница возвращает в настройки Telegram.
      need: { section: "/sales", anyOf: ["telegram.manage"] },
    },
  ],
};

/** Права экрана (screenPermissions — с признаками из /api/auth/me) и отделы ИИ-агентов пользователя. */
export type WhatsNewAccess = { permissions: readonly string[]; agentDepartments?: readonly string[] | null };

/** Карточка открыта пользователю: нет доступа к разделу — её нет в окне, нумерация шагов пересчитывается. */
export function stepOpen(step: WhatsNewStep, access: WhatsNewAccess): boolean {
  const { section, anyOf, agents } = step.need;
  if (section && !canOpen(section, access.permissions)) return false;
  if (anyOf && !anyOf.some((p) => access.permissions.includes(p))) return false;
  if (agents && !(access.agentDepartments?.length ?? 0)) return false;
  return true;
}

export const visibleSteps = (release: WhatsNewRelease, access: WhatsNewAccess): WhatsNewStep[] => release.steps.filter((s) => stepOpen(s, access));

/** Как на сервере (WhatsNew.Compare): по дню, затем по номеру за день («2026-11-13.2» после «2026-11-13»). */
export function compareVersions(a: string, b: string): number {
  const [dayA, partA = "0"] = a.split(".");
  const [dayB, partB = "0"] = b.split(".");
  return dayA !== dayB ? (dayA < dayB ? -1 : 1) : Number(partA) - Number(partB);
}

/** Окно этой версии уже закрыто (на сервере или в этом браузере; закрыто более новое — тоже). */
export const seenRelease = (seen: string | null | undefined, version: string): boolean => !!seen && compareVersions(seen, version) >= 0;

/** Ключ браузера: свой у каждого пользователя (в одном браузере могут работать двое). */
export const seenKey = (userId: string) => `onebase.whatsNew.seen.${userId}`;

export type WhatsNewUser = { whatsNewSeen?: string | null; createdAt?: string | null };

/**
 * Показать окно самому (не по «Профиль → Что нового»): есть хоть одна открытая пользователю карточка, версия не закрыта ни на сервере, ни в
 * этом браузере (запрос мог не дойти — повторно окно не показывается), и пользователь пришёл до дня релиза (по Ташкенту).
 */
export function shouldShow(release: WhatsNewRelease, user: WhatsNewUser, localSeen: string | null, steps: number): boolean {
  if (steps === 0 || seenRelease(user.whatsNewSeen, release.version) || seenRelease(localSeen, release.version)) return false;
  return !user.createdAt || new Date(user.createdAt).getTime() < new Date(`${release.date}T00:00:00+05:00`).getTime();
}

/** Русское склонение: pluralRu(7, ["нововведение", "нововведения", "нововведений"]) → «нововведений»; по-узбекски форма одна (словарь). */
export function pluralRu(n: number, forms: [string, string, string]): string {
  const mod10 = n % 10;
  const mod100 = n % 100;
  if (mod10 === 1 && mod100 !== 11) return forms[0];
  if (mod10 >= 2 && mod10 <= 4 && (mod100 < 12 || mod100 > 14)) return forms[1];
  return forms[2];
}
