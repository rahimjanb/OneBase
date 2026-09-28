import type { NextConfig } from "next";

const apiUrl = process.env.API_INTERNAL_URL ?? "http://localhost:5080";

const nextConfig: NextConfig = {
  output: "standalone",
  // В разработке проксируем /api на backend. В Docker /api перехватывает Nginx.
  async rewrites() {
    return [{ source: "/api/:path*", destination: `${apiUrl}/api/:path*` }];
  },
};

export default nextConfig;
