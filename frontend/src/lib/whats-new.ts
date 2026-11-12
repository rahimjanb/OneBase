// Окно «Что нового» после обновления 1Base: содержимое релиза и правила показа. Тексты — по-русски, перевод — словарь i18n/uz/whatsNew.ts
// (интерфейс переводит их через t). Окно — components/whats-new/WhatsNew.tsx; отметку «просмотрено» хранит сервер (POST /api/auth/whats-new).
// Тест: node --test src/lib/whats-new.test.mjs.
import { canOpen } from "./navigation.ts";

export type WhatsNewTag = "new_section" | "new" | "improved";
export type WhatsNewColor = "violet" | "accent" | "ok" | "warn" | "cyan";
export type WhatsNewIcon = "analytik" | "recruitment" | "production" | "twin" | "procurement" | "agents" | "incentives";

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

/** Текущий релиз. Новый релиз — новая версия и свои шаги: окно появится у всех ещё раз. */
export const CURRENT_RELEASE: WhatsNewRelease = {
  version: "2026-11-13",
  date: "2026-11-13",
  steps: [
    {
      id: "analytik",
      title: "Аналитик",
      tag: "new_section",
      icon: "analytik",
      color: "violet",
      heading: "Все данные компании в одном разделе",
      description:
        "Аналитик отвечает на вопросы о данных OneBase обычными словами, раскладывает продажи, план, оплаты и остатки по дням, регионам и товарным группам и считает сценарии «что если».",
      descriptionShort: "Вопросы о данных обычными словами, срезы по любым измерениям и сценарии «что если».",
      bullets: [
        "AI-аналитик: ответ с источником каждой цифры",
        "Срезы: день × регион × товарная группа, план и факт",
        "Симулятор: как изменятся продажи, сырьё, закупки и деньги",
      ],
      path: ["Аналитик", "Срезы"],
      route: "/analytik/explore",
      alt: "Вопрос Аналитику и цепочка «данные → срез → прогноз»",
      need: { section: "/analytik" },
    },
    {
      id: "recruitment",
      title: "Подбор с AI-рекрутером",
      tag: "new_section",
      icon: "recruitment",
      color: "accent",
      heading: "AI-рекрутер ведёт кандидатов в Telegram",
      description:
        "Вакансии, кандидаты и собеседования — в одном разделе. AI-рекрутер отвечает кандидатам в Telegram на их языке, подбирает вакансию по опыту и назначает собеседование на свободное время интервьюеров.",
      descriptionShort: "AI-рекрутер отвечает кандидатам в Telegram, HR подключается, когда нужно.",
      bullets: [
        "«Диалоги Telegram»: AI ведёт, HR ведёт или нужен HR",
        "Резюме, фото и сканы документов разбираются сами",
        "Собеседования — сразу в календаре интервьюеров",
      ],
      path: ["HR", "Подбор"],
      route: "/hr/recruitment",
      alt: "Диалоги Telegram с режимами «AI ведёт», «HR ведёт», «Нужен HR»",
      need: { anyOf: ["hr.recruitment.read", "hr.recruitment.manage"] },
    },
    {
      id: "production",
      title: "Производство и Технолог",
      tag: "new_section",
      icon: "production",
      color: "warn",
      heading: "Сырьё, техкарты и выпуск в одном разделе",
      description:
        "Машина с сырьём проходит путь «качество → вес → склад», партии хранятся со сроками годности. Технолог ведёт техкарты с этапами и фото, задания на выпуск расходуют сырьё по норме.",
      descriptionShort: "Приёмка сырья, партии со сроками, техкарты и задания на выпуск.",
      bullets: ["Партии сырья со сроками и контролем качества", "Техкарты с этапами и фото, импорт из Excel", "Задания на выпуск и сменные отчёты"],
      path: ["Отделы", "Производство"],
      route: "/production",
      alt: "Приезд сырья: качество, вес, склад; техкарта по этапам",
      need: { section: "/production" },
    },
    {
      id: "twin",
      title: "Цифровой двойник",
      tag: "new_section",
      icon: "twin",
      color: "cyan",
      heading: "Предприятие в 3D",
      description:
        "Территория завода — в 3D-модели. Здания привязаны к отделам и складам, а показатели и операционные карточки берутся из настоящих данных.",
      descriptionShort: "3D-модель территории с показателями отделов и складов.",
      bullets: ["3D-модель территории завода", "Здания связаны с отделами и складами", "Показатели и карточки по настоящим данным"],
      path: ["Цифровой двойник"],
      route: "/twin",
      alt: "Здания предприятия в 3D",
      need: { section: "/twin" },
    },
    {
      id: "procurement",
      title: "Снабжение и Склад",
      tag: "improved",
      icon: "procurement",
      color: "ok",
      heading: "Закупка — от заявки до склада",
      description:
        "Заявка идёт по маршруту согласования через руководителя и финансиста, закупка — в три этапа с отметкой «Куплено». Склад подсказывает минимумы остатков по расходу и принимает товар по штрихкоду.",
      descriptionShort: "Согласование заявок, «Куплено» с доставкой, минимумы и сканер штрихкода.",
      bullets: [
        "Согласование через руководителя и финансиста, тендеры",
        "Минимумы остатков по расходу, остатки в Excel",
        "Сканер штрихкода камерой, импорт движений из Excel",
      ],
      path: ["Отделы", "Снабжение"],
      route: "/procurement",
      alt: "Путь заявки: руководитель, финансист, «Куплено», склад",
      need: { section: "/procurement" },
    },
    {
      id: "agents",
      title: "ИИ-агенты в Telegram",
      tag: "improved",
      icon: "agents",
      color: "violet",
      heading: "У отделов — свои AI-сотрудники в Telegram",
      description:
        "ИИ-агенты собраны по отделам, у каждого свой номер Telegram. Агенты отвечают сразу, рекрутер знает все открытые вакансии, а проверка подскажет, почему агент может не отвечать.",
      descriptionShort: "Агенты по отделам со своими номерами Telegram и проверкой работы.",
      bullets: ["Свой номер Telegram у каждого агента", "Отвечают сразу, рекрутер знает открытые вакансии", "Проверка «почему агент может не отвечать»"],
      path: ["Настройки", "ИИ-агенты"],
      route: "/settings/agents",
      alt: "ИИ-агенты отделов, у каждого свой номер Telegram",
      need: { agents: true },
    },
    {
      id: "incentives",
      title: "Автоотчёты: акции ноября",
      tag: "new",
      icon: "incentives",
      color: "warn",
      heading: "Карточки акций Candy Gold в Telegram",
      description:
        "Автоотчёты ведут акции ноября: ТП получает цели маршрута, спящие точки и кэшбек, СВР — итог дня команды, РМ — итог недели зоны, руководство — сводку по стране.",
      descriptionShort: "Карточки ТП, итоги СВР и РМ и сводка руководства по акциям ноября.",
      bullets: [
        "Карточки ТП в 08:30, 13:00, 16:00 и 18:00 на русском и узбекском",
        "Итог дня команды СВР, итог недели зоны РМ, сводка руководства",
        "Вкладка «Акции»: параметры, спящие точки, кэшбек и выплаты",
      ],
      path: ["Telegram", "Автоотчёты", "Акции"],
      route: "/settings/telegram/reports?tab=incentives",
      alt: "Карточка ТП: время рассылки, цели маршрута, спящие точки, кэшбек",
      need: { anyOf: ["telegram.manage"] },
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
