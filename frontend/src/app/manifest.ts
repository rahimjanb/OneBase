import type { MetadataRoute } from "next";
import { headers } from "next/headers";
import { isFieldHost, requestHost } from "@/lib/field/host";

/**
 * Манифест веб-приложения: по нему Chrome, Edge, Firefox на Android и Safari на iOS предлагают
 * установить приложение на рабочий стол и открывают его без адресной строки. Отдаётся как /manifest.webmanifest.
 * На домене Sales Base — своё приложение (имя, иконки, цвет); чтение заголовков делает манифест динамическим.
 */
export default async function manifest(): Promise<MetadataRoute.Manifest> {
  if (isFieldHost(requestHost(await headers()))) {
    return {
      id: "/",
      name: "Sales Base — полевые продажи",
      short_name: "Sales Base",
      description: "Маршрут, точки, визиты, задачи и KPI для агентов и супервайзеров",
      lang: "ru",
      start_url: "/",
      scope: "/",
      display: "standalone",
      orientation: "portrait",
      background_color: "#f6f8fb",
      theme_color: "#0d2e29", // как шапка Sales Base
      categories: ["business", "productivity"],
      icons: [
        { src: "/icons/field-192.png", sizes: "192x192", type: "image/png" },
        { src: "/icons/field-512.png", sizes: "512x512", type: "image/png" },
        { src: "/icons/field-maskable-512.png", sizes: "512x512", type: "image/png", purpose: "maskable" },
      ],
      shortcuts: [
        { name: "Маршрут", url: "/routes", icons: [{ src: "/icons/field-192.png", sizes: "192x192" }] },
        { name: "Задачи", url: "/tasks", icons: [{ src: "/icons/field-192.png", sizes: "192x192" }] },
      ],
    };
  }

  return {
    id: "/",
    name: "OneBase",
    short_name: "OneBase",
    description: "Корпоративная платформа с AI-сотрудниками",
    lang: "ru",
    start_url: "/",
    scope: "/",
    display: "standalone",
    orientation: "any",
    background_color: "#f6f8fb",
    theme_color: "#16213a",
    categories: ["business", "productivity"],
    icons: [
      { src: "/icons/icon-192.png", sizes: "192x192", type: "image/png" },
      { src: "/icons/icon-512.png", sizes: "512x512", type: "image/png" },
      { src: "/icons/icon-maskable-512.png", sizes: "512x512", type: "image/png", purpose: "maskable" },
    ],
  };
}
