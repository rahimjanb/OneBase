import type { MetadataRoute } from "next";

/**
 * Манифест веб-приложения: по нему Chrome, Edge, Firefox на Android и Safari на iOS предлагают
 * установить OneBase на рабочий стол и открывают его без адресной строки. Отдаётся как /manifest.webmanifest.
 */
export default function manifest(): MetadataRoute.Manifest {
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
