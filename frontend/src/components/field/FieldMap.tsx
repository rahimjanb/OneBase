"use client";

import "leaflet/dist/leaflet.css";
import { useEffect, useRef, useState } from "react";
import type * as Leaflet from "leaflet";
import { LocateFixed } from "lucide-react";
import { mapStateStyle } from "@/lib/field/labels";
import type { FieldMapView } from "@/lib/field/types";
import { fieldApi } from "./hooks";

const routeColors = ["#0d9488", "#3b76f6", "#e0922f", "#b5179e", "#1f9d6b", "#dc4c4c", "#6d28d9", "#0891b2"];
const esc = (s: string) => s.replace(/[&<>"']/g, (c) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" })[c]!);
const money = (v: number) => (v >= 1e6 ? `${(v / 1e6).toLocaleString("ru-RU", { maximumFractionDigits: 1 })} млн` : v >= 1e3 ? `${Math.round(v / 1e3)} тыс` : String(Math.round(v)));

/**
 * Карта Sales Base (Leaflet + OpenStreetMap): точки по состоянию, маршруты с номерами, позиции агентов, «я здесь».
 * При сдвиге карты точки подгружаются для видимой области (до 1500, крупные первыми).
 */
export function FieldMap({ initial, query, followMe }: { initial: FieldMapView; query: string; followMe: boolean }) {
  const box = useRef<HTMLDivElement>(null);
  const mapRef = useRef<Leaflet.Map | null>(null);
  const layersRef = useRef<{ points: Leaflet.LayerGroup; routes: Leaflet.LayerGroup; agents: Leaflet.LayerGroup; me: Leaflet.LayerGroup } | null>(null);
  const LRef = useRef<typeof Leaflet | null>(null);
  const [view, setView] = useState(initial);
  const [status, setStatus] = useState<string | null>(null);
  const [locating, setLocating] = useState(false);
  // Фильтры меняются без пересоздания карты (страница не перемонтируется при смене query) — обработчик сдвига читает актуальные.
  const queryRef = useRef(query);
  useEffect(() => {
    queryRef.current = query;
  }, [query]);

  useEffect(() => setView(initial), [initial]);

  // Инициализация карты один раз.
  useEffect(() => {
    let cancelled = false;
    let timer: ReturnType<typeof setTimeout> | undefined;
    (async () => {
      const L = (await import("leaflet")).default;
      if (cancelled || !box.current || mapRef.current) return;
      LRef.current = L;
      const map = L.map(box.current, { preferCanvas: true, zoomControl: true, attributionControl: true }).setView([41.31, 69.28], 11);
      L.tileLayer("https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png", { maxZoom: 19, attribution: "© OpenStreetMap" }).addTo(map);
      layersRef.current = { routes: L.layerGroup().addTo(map), points: L.layerGroup().addTo(map), agents: L.layerGroup().addTo(map), me: L.layerGroup().addTo(map) };
      mapRef.current = map;

      // Начальный вид: маршруты, иначе все точки.
      const routeIds = new Set(initial.routes.flatMap((r) => r.marketIds));
      const focus = initial.points.filter((p) => routeIds.has(p.marketId));
      const pts = (focus.length ? focus : initial.points).map((p) => [p.lat, p.lon] as [number, number]);
      if (pts.length) map.fitBounds(L.latLngBounds(pts), { padding: [30, 30], maxZoom: 15 });
      draw(initial);

      map.on("moveend", () => {
        clearTimeout(timer);
        timer = setTimeout(async () => {
          const b = map.getBounds();
          try {
            setStatus("Загружаем точки…");
            const q = queryRef.current;
            const next = await fieldApi.get<FieldMapView>(`map?${q}${q ? "&" : ""}south=${b.getSouth()}&west=${b.getWest()}&north=${b.getNorth()}&east=${b.getEast()}`);
            setView(next);
            setStatus(next.truncated ? `Показаны ${next.points.length} крупнейших из ${next.total} — приблизьте карту` : null);
          } catch (e) {
            setStatus(e instanceof Error ? e.message : String(e));
          }
        }, 450);
      });
    })();
    return () => {
      cancelled = true;
      clearTimeout(timer);
      mapRef.current?.remove();
      mapRef.current = null;
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  useEffect(() => {
    if (mapRef.current) draw(view);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [view]);

  function draw(v: FieldMapView) {
    const L = LRef.current;
    const layers = layersRef.current;
    if (!L || !layers) return;
    layers.points.clearLayers();
    layers.routes.clearLayers();
    layers.agents.clearLayers();
    const byId = new Map(v.points.map((p) => [p.marketId, p]));

    v.routes.forEach((r, i) => {
      const color = routeColors[i % routeColors.length];
      const line = r.marketIds.map((id) => byId.get(id)).filter(Boolean).map((p) => [p!.lat, p!.lon] as [number, number]);
      if (line.length > 1) L.polyline(line, { color, weight: 3, opacity: 0.7, dashArray: "6 6" }).addTo(layers.routes).bindTooltip(r.agentName);
    });

    for (const p of v.points) {
      const style = mapStateStyle[p.state] ?? mapStateStyle.normal;
      const popup = `<div style="min-width:180px"><b>${esc(p.name)}</b><br/><span style="color:${style.color}">${style.label}</span>${p.sales30 ? `<br/>Продажи 30 дн: ${money(p.sales30)} сум` : ""}<br/><a href="/field/customers/${p.marketId}">Открыть карточку →</a></div>`;
      if (p.sequence != null) {
        const icon = L.divIcon({
          className: "",
          html: `<div style="background:${style.color};color:#fff;width:26px;height:26px;border-radius:13px;display:grid;place-items:center;font:600 12px Inter,sans-serif;border:2px solid #fff;box-shadow:0 1px 4px rgba(0,0,0,.3)">${p.sequence}</div>`,
          iconSize: [26, 26],
          iconAnchor: [13, 13],
        });
        L.marker([p.lat, p.lon], { icon, zIndexOffset: 500 }).bindPopup(popup).addTo(layers.points);
      } else {
        L.circleMarker([p.lat, p.lon], { radius: p.state === "normal" ? 4 : 6, color: "#fff", weight: 1, fillColor: style.color, fillOpacity: 0.9 }).bindPopup(popup).addTo(layers.points);
      }
    }

    for (const a of v.agents) {
      const initials = a.name.split(" ").filter(Boolean).slice(-2).map((s) => s[0]).join("").toUpperCase();
      const icon = L.divIcon({
        className: "",
        html: `<div style="background:#0d2e29;color:#fff;width:32px;height:32px;border-radius:16px;display:grid;place-items:center;font:700 11px Inter,sans-serif;border:2px solid #5eead4">${esc(initials)}</div>`,
        iconSize: [32, 32],
        iconAnchor: [16, 16],
      });
      L.marker([a.lat, a.lon], { icon, zIndexOffset: 1000 })
        .bindPopup(`<b>${esc(a.name)}</b><br/>визит в ${new Date(a.at).toLocaleTimeString("ru-RU", { hour: "2-digit", minute: "2-digit" })}`)
        .addTo(layers.agents);
    }
  }

  // «Я здесь» — для агента следим за позицией.
  useEffect(() => {
    if (!followMe || typeof navigator === "undefined" || !navigator.geolocation) return;
    const id = navigator.geolocation.watchPosition(
      (pos) => {
        const L = LRef.current;
        const layers = layersRef.current;
        if (!L || !layers) return;
        layers.me.clearLayers();
        const ll: [number, number] = [pos.coords.latitude, pos.coords.longitude];
        L.circle(ll, { radius: Math.min(pos.coords.accuracy, 500), color: "#3b76f6", weight: 1, fillOpacity: 0.1 }).addTo(layers.me);
        L.circleMarker(ll, { radius: 7, color: "#fff", weight: 2, fillColor: "#3b76f6", fillOpacity: 1 }).bindTooltip("Вы здесь").addTo(layers.me);
      },
      () => undefined,
      { enableHighAccuracy: true, maximumAge: 15000, timeout: 20000 },
    );
    return () => navigator.geolocation.clearWatch(id);
  }, [followMe]);

  const locate = () => {
    if (!navigator.geolocation) return;
    setLocating(true);
    navigator.geolocation.getCurrentPosition(
      (pos) => {
        setLocating(false);
        mapRef.current?.setView([pos.coords.latitude, pos.coords.longitude], 15);
      },
      () => {
        setLocating(false);
        setStatus("Не удалось определить местоположение — проверьте разрешение и GPS");
      },
      { enableHighAccuracy: true, timeout: 12000 },
    );
  };

  return (
    <div className="field-map relative isolate overflow-hidden rounded-xl border border-line">
      <div ref={box} className="h-[calc(100dvh-15rem)] min-h-[420px] w-full bg-muted lg:h-[calc(100dvh-13rem)]" />
      <button type="button" onClick={locate} aria-label="Моё местоположение" className="absolute bottom-24 right-3 z-[500] grid size-11 place-items-center rounded-full bg-surface text-ink shadow-md lg:bottom-6">
        <LocateFixed className={`size-5 ${locating ? "animate-pulse text-accent" : ""}`} />
      </button>
      {status && <div className="absolute left-1/2 top-3 z-[500] -translate-x-1/2 rounded-full bg-surface/95 px-3 py-1.5 text-xs text-ink shadow">{status}</div>}
      <div className="absolute bottom-3 left-3 z-[500] flex max-w-[calc(100%-5rem)] flex-wrap gap-x-3 gap-y-1 rounded-lg bg-surface/95 px-3 py-2 text-[11px] text-ink-2 shadow">
        {Object.entries(mapStateStyle).map(([key, s]) => (
          <span key={key} className="inline-flex items-center gap-1">
            <span className="size-2.5 rounded-full" style={{ background: s.color }} />
            {s.label}
          </span>
        ))}
      </div>
    </div>
  );
}
