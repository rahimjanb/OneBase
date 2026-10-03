"use client";

import { Moon, Sun } from "lucide-react";
import { useEffect, useState } from "react";

const KEY = "onebase-theme";

/** Скрипт в <head>: применяет сохранённую тему до отрисовки, без мигания. */
export const themeInitScript = `try{var t=localStorage.getItem("${KEY}");if(t){document.documentElement.dataset.theme=t;document.addEventListener("DOMContentLoaded",function(){var c=t==="dark"?"#0b1220":"#16213a";document.querySelectorAll('meta[name="theme-color"]').forEach(function(m){m.setAttribute("content",c)})})}}catch(e){}`;

function currentTheme(): "light" | "dark" {
  const explicit = document.documentElement.dataset.theme;
  if (explicit === "light" || explicit === "dark") return explicit;
  return window.matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light";
}

/** Кнопка темы в шапке: цвет берёт от шапки (на телефоне она тёмная, на больших экранах — светлая). */
export function ThemeToggle() {
  const [theme, setTheme] = useState<"light" | "dark" | null>(null);

  useEffect(() => setTheme(currentTheme()), []);

  function toggle() {
    const next = currentTheme() === "dark" ? "light" : "dark";
    document.documentElement.dataset.theme = next;
    try {
      localStorage.setItem(KEY, next);
    } catch {
      // хранилище недоступно — тема действует до перезагрузки
    }
    // Цвет строки состояния установленного приложения — как у шапки выбранной темы.
    for (const meta of document.querySelectorAll('meta[name="theme-color"]')) meta.setAttribute("content", next === "dark" ? "#0b1220" : "#16213a");
    setTheme(next);
  }

  return (
    <button
      type="button"
      onClick={toggle}
      aria-label={theme === "dark" ? "Светлая тема" : "Тёмная тема"}
      className="grid size-10 place-items-center rounded-full text-current transition-colors hover:bg-white/10 hover:text-white lg:size-9 lg:hover:bg-muted lg:hover:text-ink"
    >
      {theme === "dark" ? <Sun className="size-4" strokeWidth={1.75} /> : <Moon className="size-4" strokeWidth={1.75} />}
    </button>
  );
}
