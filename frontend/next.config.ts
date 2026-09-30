import type { NextConfig } from "next";

const apiUrl = process.env.API_INTERNAL_URL ?? "http://localhost:5080";

const nextConfig: NextConfig = {
  output: "standalone",
  experimental: {
    // Уже открытые страницы 30 с отдаются из памяти браузера: «назад» и повторный клик по вкладке — мгновенно.
    // После синхронизации панель продаж сама вызывает router.refresh(), и кэш сбрасывается.
    staleTimes: { dynamic: 30 },
  },
  // Dashboard пока на демо-данных — главная ведёт в «Отделы» (оттуда — «Продажи»). Страница главной сохранена.
  async redirects() {
    return [{ source: "/", destination: "/departments", permanent: false }];
  },
  // В разработке проксируем /api на backend. В Docker /api перехватывает Nginx.
  async rewrites() {
    return [{ source: "/api/:path*", destination: `${apiUrl}/api/:path*` }];
  },
};

export default nextConfig;
