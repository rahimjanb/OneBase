"use client";

import { useEffect, useRef } from "react";
import * as THREE from "three";
import { useT } from "@/i18n/client";
import type { Tone } from "@/lib/twin";
import { blocks, gates, roads, siteCenter, territory, type SiteBlock } from "@/lib/twin-site";

/**
 * 3D-модель территории завода (перенос factory-3d.html на npm-пакет three): корпуса, проезд, ворота и охрана, граница территории.
 * Цвет корпуса — статус слоя (токены темы OneBase: ok / warn / bad / акцент), «вне слоя» — полупрозрачный, выбранный — с контуром и подсветкой.
 * Управление: вращение — левая кнопка или палец, сдвиг — правая кнопка, Shift или два пальца, масштаб — колесо или щипок; «План сверху» —
 * вид сверху, север вверху. Переходы камеры плавные (без анимации — при «уменьшить движение»). Кадр рисуется только при изменениях.
 * Подписи — HTML поверх холста: React рисует их, сцена каждый кадр ставит на место.
 */

export type SceneBuilding = { key: string; name: string; tone: Tone; value: string | null };
export type SceneInsets = { top: number; right: number; bottom: number };

type Props = {
  buildings: SceneBuilding[];
  selected: string | null;
  onSelect: (key: string | null) => void;
  mode: "3d" | "plan";
  /** Меняется — вид сбрасывается к исходному. */
  resetKey: number;
  /** Сколько места сверху, справа и снизу занимают панели поверх модели: центр вида — в свободной части. */
  insets: SceneInsets;
};

type Engine = {
  setBuildings: (list: SceneBuilding[]) => void;
  setSelected: (key: string | null) => void;
  setMode: (mode: "3d" | "plan") => void;
  setInsets: (insets: SceneInsets) => void;
  reset: () => void;
  dispose: () => void;
};

export default function TwinScene({ buildings, selected, onSelect, mode, resetKey, insets }: Props) {
  const t = useT();
  const hostRef = useRef<HTMLDivElement>(null);
  const labelsRef = useRef<HTMLDivElement>(null);
  const engineRef = useRef<Engine | null>(null);
  const onSelectRef = useRef(onSelect);
  onSelectRef.current = onSelect;

  useEffect(() => {
    const host = hostRef.current;
    const labels = labelsRef.current;
    if (!host || !labels) return;
    const engine = createEngine(host, labels, (key) => onSelectRef.current(key));
    engineRef.current = engine;
    return () => {
      engine.dispose();
      engineRef.current = null;
    };
  }, []);

  useEffect(() => engineRef.current?.setBuildings(buildings), [buildings]);
  useEffect(() => engineRef.current?.setSelected(selected), [selected]);
  useEffect(() => engineRef.current?.setMode(mode), [mode]);
  useEffect(() => engineRef.current?.setInsets(insets), [insets]);
  const firstReset = useRef(resetKey);
  useEffect(() => {
    if (resetKey !== firstReset.current) engineRef.current?.reset();
  }, [resetKey]);

  return (
    <div className="absolute inset-0 isolate">
      <div ref={hostRef} className="absolute inset-0 touch-none select-none" aria-label={t("3D-модель территории завода")} role="img" />
      <div ref={labelsRef} className="pointer-events-none absolute inset-0 overflow-hidden" aria-hidden={false}>
        {buildings.map((b) => (
          <button
            key={b.key}
            type="button"
            data-label={b.key}
            onClick={() => onSelect(b.key === selected ? null : b.key)}
            className={`twin-label pointer-events-auto absolute left-0 top-0 flex max-w-[280px] items-center gap-1.5 whitespace-nowrap rounded-full border py-1 pl-2 pr-2.5 text-[12px] font-medium leading-none shadow-[0_6px_18px_-8px_rgb(0_0_0/0.45)] backdrop-blur-md transition-[opacity,box-shadow,border-color] duration-200 will-change-transform focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-accent ${
              b.key === selected ? "border-accent-bright bg-surface-solid text-ink ring-2 ring-accent-bright/40" : "border-line-strong bg-surface-solid/80 text-ink-soft hover:border-accent-bright/70"
            } ${b.tone === "off" && b.key !== selected ? "opacity-60" : ""}`}
            style={{ visibility: "hidden" }}
          >
            <span aria-hidden className={`size-2 shrink-0 rounded-full ${dotClass[b.tone]}`} />
            <span className="max-w-[170px] truncate">{b.name}</span>
            {b.value && (
              <span className={`shrink-0 border-l border-line pl-1.5 font-semibold tabular-nums [.twin-label-compact_&]:hidden ${valueClass[b.tone]}`}>{b.value}</span>
            )}
            <span aria-hidden className="absolute left-1/2 top-full h-3.5 w-px -translate-x-1/2 bg-gradient-to-b from-line-strong to-transparent" />
          </button>
        ))}
        {gates.map((g) => (
          <span
            key={g.name}
            data-gate={g.name}
            className="absolute left-0 top-0 whitespace-nowrap rounded-full border border-line bg-surface-solid/70 px-2 py-0.5 text-[10.5px] font-medium text-ink-3 backdrop-blur-md"
            style={{ visibility: "hidden" }}
          >
            {t(g.name)}
          </span>
        ))}
      </div>
    </div>
  );
}

const dotClass: Record<Tone, string> = {
  ok: "bg-ok",
  warn: "bg-warn",
  bad: "bg-bad",
  info: "bg-accent-bright",
  nodata: "bg-ink-4",
  off: "bg-ink-4/50",
};

const valueClass: Record<Tone, string> = {
  ok: "text-ok",
  warn: "text-warn",
  bad: "text-bad",
  info: "text-accent-bright",
  nodata: "text-ink-3",
  off: "text-ink-4",
};

// ---------------------------------------------------------------------------------------------------------------------------------------
// Сцена

type Palette = {
  dark: boolean;
  tone: Record<Exclude<Tone, "off">, THREE.Color>;
  accent: THREE.Color;
  wall: THREE.Color;
  roof: THREE.Color;
  rib: THREE.Color;
  glass: THREE.Color;
  frame: THREE.Color;
  site: THREE.Color;
  road: THREE.Color;
  grid: THREE.Color;
  border: THREE.Color;
  /** Куда «уходит» корпус вне слоя: непрозрачный приглушённый цвет вместо прозрачности (без наложений). */
  recede: THREE.Color;
};

/** Цвет токена темы (hex); не hex или нет — запасной. */
function token(css: CSSStyleDeclaration, name: string, fallback: string): THREE.Color {
  const v = css.getPropertyValue(name).trim();
  return new THREE.Color(/^#[0-9a-f]{3,8}$/i.test(v) ? v.slice(0, 7) : fallback);
}

function readPalette(): Palette {
  const root = document.documentElement;
  const css = getComputedStyle(root);
  const dark = css.colorScheme.includes("dark");
  return {
    dark,
    tone: {
      ok: token(css, "--color-ok", "#1f9d6b"),
      warn: token(css, "--color-warn", "#e0922f"),
      bad: token(css, "--color-bad", "#dc4c4c"),
      info: token(css, "--color-accent-bright", "#2563eb"),
      nodata: new THREE.Color(dark ? "#6f7c96" : "#a9b2c2"),
    },
    accent: token(css, "--color-accent-bright", "#2563eb"),
    wall: new THREE.Color(dark ? "#cfd8ea" : "#ffffff"),
    roof: new THREE.Color(dark ? "#9aa8c2" : "#dde2ea"),
    rib: new THREE.Color(dark ? "#7d8ba6" : "#c3cad6"),
    glass: new THREE.Color(dark ? "#7fa6e0" : "#8fb4cf"),
    frame: new THREE.Color(dark ? "#3b4b6e" : "#4a5263"),
    site: new THREE.Color(dark ? "#172644" : "#eef1f5"),
    road: new THREE.Color(dark ? "#24365b" : "#c9d0da"),
    grid: new THREE.Color(dark ? "#2a3c63" : "#cfd6e0"),
    border: token(css, "--color-accent-bright", "#2563eb"),
    recede: new THREE.Color(dark ? "#26324d" : "#e4e8ee"),
  };
}

type BuildingMats = { body: THREE.MeshStandardMaterial; roof: THREE.MeshStandardMaterial; rib: THREE.MeshStandardMaterial; glass: THREE.MeshStandardMaterial; frame: THREE.MeshStandardMaterial };

type Built = { block: SiteBlock; group: THREE.Group; mats: BuildingMats; outline: THREE.LineSegments; anchor: THREE.Vector3; pick: THREE.Object3D[] };

type View = { x: number; z: number; theta: number; phi: number; dist: number };

const BASE_Y = 0.6;
const HOME: View = { x: siteCenter.x, z: siteCenter.z, theta: -0.75, phi: 0.95, dist: 300 };
/** Размер территории для «вписать в экран»: план — по длинной стороне (с юга на север), 3D — по диагонали с запасом. */
const SITE_SPAN = { plan: { w: 135, h: 235 }, view: { w: 250, h: 210 } };
const PLAN_PHI = 0.012;
const MIN_DIST = 40;
const MAX_DIST = 700;

function createEngine(host: HTMLElement, labels: HTMLElement, onPick: (key: string | null) => void): Engine {
  const reduced = matchMedia("(prefers-reduced-motion: reduce)").matches;
  const renderer = new THREE.WebGLRenderer({ antialias: true, alpha: true, powerPreference: "high-performance" });
  renderer.setPixelRatio(Math.min(devicePixelRatio, 2));
  renderer.setClearColor(0x000000, 0);
  renderer.shadowMap.enabled = true;
  renderer.shadowMap.type = THREE.PCFShadowMap;
  renderer.domElement.style.display = "block";
  renderer.domElement.style.touchAction = "none";
  host.appendChild(renderer.domElement);

  const scene = new THREE.Scene();
  const camera = new THREE.PerspectiveCamera(42, 1, 1, 3000);
  const disposables: { dispose: () => void }[] = [];
  const keep = <D extends { dispose: () => void }>(d: D) => (disposables.push(d), d);

  // Свет: небо и земля + солнце с мягкими тенями (единицы three r155+ — физические, поэтому интенсивности больше, чем в r128).
  const hemi = new THREE.HemisphereLight(0xffffff, 0x8a94a8, 2.1);
  scene.add(hemi);
  const sun = new THREE.DirectionalLight(0xffffff, 2.4);
  sun.position.set(-120, 180, -90);
  sun.target.position.set(siteCenter.x, 0, siteCenter.z);
  sun.castShadow = true;
  sun.shadow.mapSize.set(2048, 2048);
  Object.assign(sun.shadow.camera, { left: -200, right: 200, top: 200, bottom: -200, near: 10, far: 700 });
  sun.shadow.bias = -0.0004;
  scene.add(sun, sun.target);

  let palette = readPalette();

  // Земля: только тени (фон страницы просвечивает), сетка и площадка территории.
  const ground = new THREE.Mesh(keep(new THREE.PlaneGeometry(1400, 1400)), keep(new THREE.ShadowMaterial({ opacity: 0.22 })));
  ground.rotation.x = -Math.PI / 2;
  ground.position.set(siteCenter.x, 0, siteCenter.z);
  ground.receiveShadow = true;
  scene.add(ground);

  const grid = new THREE.GridHelper(1000, 100, palette.grid, palette.grid);
  grid.position.set(siteCenter.x, 0.02, siteCenter.z);
  const gridMat = grid.material as THREE.LineBasicMaterial;
  gridMat.transparent = true;
  gridMat.opacity = 0.35;
  keep(grid.geometry);
  keep(gridMat);
  scene.add(grid);

  const siteMat = keep(new THREE.MeshStandardMaterial({ color: palette.site, roughness: 1 }));
  const shape = new THREE.Shape(territory.map(([x, z]) => new THREE.Vector2(x, -z)));
  const site = new THREE.Mesh(keep(new THREE.ExtrudeGeometry(shape, { depth: BASE_Y, bevelEnabled: false })), siteMat);
  site.rotation.x = -Math.PI / 2;
  site.receiveShadow = true;
  scene.add(site);
  const borderMat = keep(new THREE.LineBasicMaterial({ color: palette.border, transparent: true, opacity: 0.75 }));
  const border = new THREE.Line(keep(new THREE.BufferGeometry().setFromPoints([...territory, territory[0]].map(([x, z]) => new THREE.Vector3(x, BASE_Y + 0.15, z)))), borderMat);
  scene.add(border);

  // Проезд и разметка.
  const roadMat = keep(new THREE.MeshStandardMaterial({ color: palette.road, roughness: 1 }));
  const markMat = keep(new THREE.MeshBasicMaterial({ color: 0xf2f2ee, transparent: true, opacity: 0.7 }));
  const dashGeo = keep(new THREE.BoxGeometry(0.3, 0.05, 3));
  for (const r of roads) {
    const m = new THREE.Mesh(keep(new THREE.BoxGeometry(r.w, 0.25, r.d)), roadMat);
    m.position.set(r.x + r.w / 2, BASE_Y + 0.12, r.z + r.d / 2);
    m.receiveShadow = true;
    scene.add(m);
    for (let i = 0; i < Math.floor(r.d / 6); i++) {
      const dash = new THREE.Mesh(dashGeo, markMat);
      dash.position.set(r.x + r.w / 2, BASE_Y + 0.26, r.z + i * 6 + 3);
      scene.add(dash);
    }
  }

  // Ворота для фур и охрана.
  const gateMats = {
    steel: keep(new THREE.MeshStandardMaterial({ color: 0x2b2f33, roughness: 0.6, metalness: 0.4 })),
    groove: keep(new THREE.MeshStandardMaterial({ color: 0x4b4f55, roughness: 0.9 })),
    rib: keep(new THREE.MeshStandardMaterial({ color: palette.rib, roughness: 0.95 })),
    wall: keep(new THREE.MeshStandardMaterial({ color: palette.wall, roughness: 0.9 })),
    glass: keep(new THREE.MeshStandardMaterial({ color: palette.glass, roughness: 0.3, metalness: 0.2 })),
  };
  const box = (w: number, h: number, d: number) => keep(new THREE.BoxGeometry(w, h, d));
  for (const g of gates) {
    const cx = g.x + g.w / 2;
    if (g.type === "guard") {
      const body = new THREE.Mesh(box(g.w, g.h, 3), gateMats.wall);
      body.position.set(cx, BASE_Y + g.h / 2, g.z + 1.5);
      body.castShadow = true;
      const win = new THREE.Mesh(box(g.w + 0.1, g.h * 0.4, 3.1), gateMats.glass);
      win.position.set(cx, BASE_Y + g.h * 0.6, g.z + 1.5);
      const roof = new THREE.Mesh(box(g.w + 0.6, 0.3, 3.6), gateMats.rib);
      roof.position.set(cx, BASE_Y + g.h + 0.15, g.z + 1.5);
      scene.add(body, win, roof);
    } else {
      const leaf = new THREE.Mesh(box(g.w, g.h * 0.78, 0.6), gateMats.steel);
      leaf.position.set(cx, BASE_Y + g.h * 0.39, g.z + 0.1);
      scene.add(leaf);
      for (let i = 1; i <= 4; i++) {
        const groove = new THREE.Mesh(box(g.w * 0.55 - i * 0.5, 0.12, 0.2), gateMats.groove);
        groove.position.set(cx, BASE_Y + g.h * 0.1 + i * 0.35, g.z + 0.45);
        scene.add(groove);
      }
      const grille = new THREE.Mesh(box(g.w, g.h * 0.22, 0.15), gateMats.rib);
      grille.position.set(cx, BASE_Y + g.h * 0.89, g.z + 0.1);
      scene.add(grille);
      const barGeo = box(0.06, g.h * 0.22, 0.2);
      for (let x = -g.w / 2 + 0.5; x < g.w / 2; x += 0.5) {
        const bar = new THREE.Mesh(barGeo, gateMats.steel);
        bar.position.set(cx + x, BASE_Y + g.h * 0.89, g.z + 0.2);
        scene.add(bar);
      }
      const frame = new THREE.Mesh(box(g.w + 0.6, 0.3, 0.8), gateMats.steel);
      frame.position.set(cx, BASE_Y + g.h, g.z + 0.2);
      const visor = new THREE.Mesh(box(g.w + 2, 0.12, 1.4), gateMats.rib);
      visor.position.set(cx, BASE_Y + g.h + 0.4, g.z + 0.7);
      visor.castShadow = true;
      const apron = new THREE.Mesh(box(g.w + 2, 0.25, 12), roadMat);
      apron.position.set(cx, BASE_Y + 0.12, g.z + 6);
      apron.receiveShadow = true;
      scene.add(frame, visor, apron);
    }
  }

  // Корпуса.
  const built = new Map<string, Built>();
  const pickables: THREE.Object3D[] = [];
  for (const b of blocks) {
    const item = buildBlock(b, palette, keep);
    built.set(b.key, item);
    scene.add(item.group);
    for (const p of item.pick) {
      p.userData.key = b.key;
      pickables.push(p);
    }
  }

  // ---------- Состояние ----------
  let tones = new Map<string, Tone>();
  let selected: string | null = null;
  let hovered: string | null = null;
  let mode: "3d" | "plan" = "3d";
  let insets: SceneInsets = { top: 0, right: 0, bottom: 0 };
  const view: View = { ...HOME };
  const goal: View = { ...HOME };
  let width = 1;
  let height = 1;
  let dirty = true;
  let frame = 0;
  /** Пока пользователь не менял масштаб, вид вписывается в свободную часть экрана при каждом изменении размеров и панелей. */
  let fitted = true;

  /** Расстояние камеры, при котором территория помещается в свободной от панелей части экрана. */
  function fitDistance(plan: boolean) {
    const span = plan ? SITE_SPAN.plan : SITE_SPAN.view;
    const freeW = Math.max(200, width - Math.min(insets.right, width * 0.6));
    const freeH = Math.max(200, height - Math.min(insets.top, height * 0.5) - Math.min(insets.bottom, height * 0.6));
    const tanV = Math.tan(((camera.fov / 2) * Math.PI) / 180);
    const byH = span.h / 2 / tanV / (freeH / height);
    const byW = span.w / 2 / (tanV * (width / height)) / (freeW / width);
    return clamp(Math.max(byH, byW) * 1.04, 160, MAX_DIST);
  }

  function paint() {
    for (const [key, item] of built) {
      const tone = tones.get(key) ?? "off";
      const isSel = key === selected;
      const isHover = key === hovered;
      const off = tone === "off" && !isSel;
      const tint = tone === "off" ? null : palette.tone[tone];
      const { body, roof, rib, glass, frame: frameMat } = item.mats;
      const glassBlock = !!item.block.glass;
      body.color.copy(glassBlock ? palette.glass : palette.wall);
      roof.color.copy(palette.roof);
      rib.color.copy(palette.rib);
      glass.color.copy(palette.glass);
      frameMat.color.copy(palette.frame);
      if (tint) {
        // Статус — цветом стен и крыши: стены сильнее, крыша мягче, чтобы форма корпуса читалась.
        body.color.lerp(tint, tone === "nodata" ? 0.45 : glassBlock ? 0.5 : 0.62);
        roof.color.lerp(tint, tone === "nodata" ? 0.3 : 0.4);
        rib.color.lerp(tint, 0.25);
        // Стеклянный офис почти весь из рам и стекла — их тоже подкрашиваем, иначе статус не виден.
        if (glassBlock) frameMat.color.lerp(tint, tone === "nodata" ? 0.25 : 0.45);
      } else if (off) {
        // Вне слоя — приглушённо, но непрозрачно: прозрачные корпуса накладываются друг на друга.
        for (const m of [body, roof, rib, glass, frameMat]) m.color.lerp(palette.recede, 0.62);
      }
      const glow = isSel ? 0.1 : isHover ? 0.08 : palette.dark && tint && tone !== "nodata" ? 0.06 : 0;
      for (const m of [body, roof]) {
        m.emissive.copy(isSel || isHover ? palette.accent : (tint ?? palette.wall));
        m.emissiveIntensity = glow;
      }
      item.outline.visible = isSel || isHover;
      const outlineMat = item.outline.material as THREE.LineBasicMaterial;
      outlineMat.color.copy(palette.accent);
      outlineMat.opacity = isSel ? 1 : 0.55;
    }
    dirty = true;
  }

  function applyCamera() {
    const phi = mode === "plan" ? Math.max(PLAN_PHI, view.phi) : view.phi;
    camera.position.set(
      view.x + view.dist * Math.sin(phi) * Math.sin(view.theta),
      view.dist * Math.cos(phi),
      view.z + view.dist * Math.sin(phi) * Math.cos(view.theta),
    );
    camera.up.set(0, 1, 0);
    camera.lookAt(view.x, 0, view.z);
  }

  function resize() {
    width = Math.max(1, host.clientWidth);
    height = Math.max(1, host.clientHeight);
    renderer.setSize(width, height, false);
    renderer.domElement.style.width = `${width}px`;
    renderer.domElement.style.height = `${height}px`;
    camera.aspect = width / height;
    // Центр вида — в свободной от панелей части: снимаем окно «большого» кадра (см. setViewOffset).
    const r = Math.min(insets.right, width * 0.6);
    const top = Math.min(insets.top, height * 0.5);
    const bottom = Math.min(insets.bottom, height * 0.6);
    if (r > 0 || top > 0 || bottom > 0) camera.setViewOffset(width + r, height + top + bottom, r, bottom, width, height);
    else camera.clearViewOffset();
    camera.updateProjectionMatrix();
    if (fitted && !selected) goal.dist = fitDistance(mode === "plan");
    dirty = true;
  }

  const tmp = new THREE.Vector3();
  function placeLabels() {
    const compact = view.dist > 430;
    labels.querySelectorAll<HTMLElement>("[data-label]").forEach((el) => {
      const item = built.get(el.dataset.label!);
      if (!item) return;
      tmp.copy(item.anchor).project(camera);
      const visible = tmp.z < 1 && Math.abs(tmp.x) < 1.2 && Math.abs(tmp.y) < 1.2;
      el.style.visibility = visible ? "visible" : "hidden";
      if (!visible) return;
      const x = ((tmp.x + 1) / 2) * width;
      const y = ((1 - tmp.y) / 2) * height;
      el.style.transform = `translate3d(${x}px, ${y}px, 0) translate(-50%, calc(-100% - 14px))`;
      el.style.zIndex = String(1000 - Math.round(tmp.z * 1000) + (el.dataset.label === selected ? 2000 : 0));
      el.classList.toggle("twin-label-compact", compact && el.dataset.label !== selected);
    });
    labels.querySelectorAll<HTMLElement>("[data-gate]").forEach((el) => {
      const g = gates.find((x) => x.name === el.dataset.gate);
      if (!g) return;
      tmp.set(g.x + g.w / 2, BASE_Y + g.h + 1, g.z + 1).project(camera);
      const visible = tmp.z < 1 && view.dist < 360;
      el.style.visibility = visible ? "visible" : "hidden";
      if (visible) el.style.transform = `translate3d(${((tmp.x + 1) / 2) * width}px, ${((1 - tmp.y) / 2) * height}px, 0) translate(-50%, -130%)`;
    });
  }

  function step() {
    frame = requestAnimationFrame(step);
    let moving = false;
    const k = reduced ? 1 : 0.14;
    for (const key of ["x", "z", "theta", "phi", "dist"] as const) {
      const d = goal[key] - view[key];
      if (Math.abs(d) > (key === "theta" || key === "phi" ? 0.0005 : 0.02)) {
        view[key] += d * k;
        moving = true;
      } else if (d !== 0) {
        view[key] = goal[key];
        moving = true;
      }
    }
    if (!moving && !dirty) return;
    dirty = false;
    applyCamera();
    renderer.render(scene, camera);
    placeLabels();
  }

  // ---------- Управление ----------
  const el = renderer.domElement;
  let drag: { id: number; x: number; y: number; sx: number; sy: number; btn: number; moved: boolean } | null = null;
  let pinch: { d: number; cx: number; cy: number } | null = null;
  const ray = new THREE.Raycaster();
  const pointer = new THREE.Vector2();

  const keyAt = (clientX: number, clientY: number): string | null => {
    const rect = el.getBoundingClientRect();
    pointer.set(((clientX - rect.left) / rect.width) * 2 - 1, -((clientY - rect.top) / rect.height) * 2 + 1);
    ray.setFromCamera(pointer, camera);
    const hit = ray.intersectObjects(pickables, false)[0];
    return (hit?.object.userData.key as string | undefined) ?? null;
  };

  function pan(dx: number, dy: number) {
    const k = view.dist / 900;
    const theta = mode === "plan" ? 0 : view.theta;
    const rightX = Math.cos(theta);
    const rightZ = -Math.sin(theta);
    const fwdX = -Math.sin(theta);
    const fwdZ = -Math.cos(theta);
    // «Взять карту»: модель идёт за пальцем — вправо и вниз тянут вправо и вниз.
    const mx = -dx * k * rightX + dy * k * fwdX;
    const mz = -dx * k * rightZ + dy * k * fwdZ;
    for (const v of [view, goal]) {
      v.x = clamp(v.x + mx, -150, 300);
      v.z = clamp(v.z + mz, -120, 360);
    }
  }

  const onDown = (e: PointerEvent) => {
    if (pinch) return;
    el.setPointerCapture(e.pointerId);
    drag = { id: e.pointerId, x: e.clientX, y: e.clientY, sx: e.clientX, sy: e.clientY, btn: e.button, moved: false };
  };
  const onMove = (e: PointerEvent) => {
    if (!drag || drag.id !== e.pointerId) {
      if (e.pointerType === "mouse" && !drag) {
        const key = keyAt(e.clientX, e.clientY);
        if (key !== hovered) {
          hovered = key;
          el.style.cursor = key ? "pointer" : "grab";
          paint();
        }
      }
      return;
    }
    const dx = e.clientX - drag.x;
    const dy = e.clientY - drag.y;
    if (Math.abs(e.clientX - drag.sx) + Math.abs(e.clientY - drag.sy) > 4) drag.moved = true;
    if (drag.btn === 2 || e.shiftKey || mode === "plan") pan(dx, dy);
    else {
      view.theta -= dx * 0.006;
      view.phi = clamp(view.phi - dy * 0.006, 0.15, 1.45);
      goal.theta = view.theta;
      goal.phi = view.phi;
    }
    drag.x = e.clientX;
    drag.y = e.clientY;
    el.style.cursor = "grabbing";
    dirty = true;
  };
  const onUp = (e: PointerEvent) => {
    if (drag && drag.id === e.pointerId && !drag.moved && drag.btn === 0) onPick(keyAt(e.clientX, e.clientY));
    drag = null;
    el.style.cursor = hovered ? "pointer" : "grab";
  };
  const onLeave = () => {
    if (hovered) {
      hovered = null;
      paint();
    }
  };
  const onWheel = (e: WheelEvent) => {
    e.preventDefault();
    fitted = false;
    goal.dist = clamp(goal.dist * (1 + Math.sign(e.deltaY) * 0.12), MIN_DIST, MAX_DIST);
  };
  const touchInfo = (e: TouchEvent) => {
    const [a, b] = [e.touches[0], e.touches[1]];
    return { d: Math.hypot(a.clientX - b.clientX, a.clientY - b.clientY), cx: (a.clientX + b.clientX) / 2, cy: (a.clientY + b.clientY) / 2 };
  };
  const onTouchStart = (e: TouchEvent) => {
    if (e.touches.length === 2) {
      pinch = touchInfo(e);
      drag = null;
    }
  };
  const onTouchMove = (e: TouchEvent) => {
    if (e.touches.length !== 2 || !pinch) return;
    const next = touchInfo(e);
    fitted = false;
    view.dist = goal.dist = clamp(view.dist * (pinch.d / next.d), MIN_DIST, MAX_DIST);
    pan(next.cx - pinch.cx, next.cy - pinch.cy);
    pinch = next;
    dirty = true;
  };
  const onTouchEnd = (e: TouchEvent) => {
    if (e.touches.length < 2) pinch = null;
  };
  const onContext = (e: Event) => e.preventDefault();

  el.style.cursor = "grab";
  el.addEventListener("pointerdown", onDown);
  el.addEventListener("pointermove", onMove);
  el.addEventListener("pointerup", onUp);
  el.addEventListener("pointercancel", onUp);
  el.addEventListener("pointerleave", onLeave);
  el.addEventListener("wheel", onWheel, { passive: false });
  el.addEventListener("touchstart", onTouchStart, { passive: true });
  el.addEventListener("touchmove", onTouchMove, { passive: true });
  el.addEventListener("touchend", onTouchEnd);
  el.addEventListener("contextmenu", onContext);

  const observer = new ResizeObserver(resize);
  observer.observe(host);

  // Тема: data-theme на <html> или системная — цвета сцены из токенов.
  const retheme = () => {
    palette = readPalette();
    siteMat.color.copy(palette.site);
    roadMat.color.copy(palette.road);
    gridMat.color.copy(palette.grid);
    borderMat.color.copy(palette.border);
    gateMats.rib.color.copy(palette.rib);
    gateMats.wall.color.copy(palette.wall);
    gateMats.glass.color.copy(palette.glass);
    hemi.groundColor.set(palette.dark ? 0x3a4560 : 0x9aa3b2);
    (ground.material as THREE.ShadowMaterial).opacity = palette.dark ? 0.35 : 0.18;
    paint();
  };
  const themeObserver = new MutationObserver(retheme);
  themeObserver.observe(document.documentElement, { attributes: true, attributeFilter: ["data-theme", "class", "data-brand"] });
  const scheme = matchMedia("(prefers-color-scheme: dark)");
  scheme.addEventListener("change", retheme);

  resize();
  view.dist = goal.dist;
  retheme();
  frame = requestAnimationFrame(step);

  return {
    setBuildings(list) {
      tones = new Map(list.map((b) => [b.key, b.tone]));
      paint();
    },
    setSelected(key) {
      selected = key;
      const item = key ? built.get(key) : null;
      if (item) {
        const b = item.block;
        goal.x = b.x + b.w / 2;
        goal.z = b.z + b.d / 2;
        goal.dist = clamp(Math.max(b.w, b.d) * 2.6 + 70, 110, 240);
      }
      paint();
    },
    setMode(next) {
      if (next === mode) return;
      mode = next;
      if (mode === "plan") {
        goal.phi = PLAN_PHI;
        goal.theta = 0;
      } else {
        goal.phi = HOME.phi;
        goal.theta = HOME.theta;
      }
      if (fitted || !selected) {
        fitted = true;
        goal.x = HOME.x;
        goal.z = HOME.z;
        goal.dist = fitDistance(mode === "plan");
      }
      dirty = true;
    },
    setInsets(next) {
      insets = next;
      resize();
    },
    reset() {
      Object.assign(goal, HOME);
      fitted = true;
      goal.dist = fitDistance(mode === "plan");
      if (mode === "plan") {
        goal.phi = PLAN_PHI;
        goal.theta = 0;
      }
    },
    dispose() {
      cancelAnimationFrame(frame);
      observer.disconnect();
      themeObserver.disconnect();
      scheme.removeEventListener("change", retheme);
      el.removeEventListener("pointerdown", onDown);
      el.removeEventListener("pointermove", onMove);
      el.removeEventListener("pointerup", onUp);
      el.removeEventListener("pointercancel", onUp);
      el.removeEventListener("pointerleave", onLeave);
      el.removeEventListener("wheel", onWheel);
      el.removeEventListener("touchstart", onTouchStart);
      el.removeEventListener("touchmove", onTouchMove);
      el.removeEventListener("touchend", onTouchEnd);
      el.removeEventListener("contextmenu", onContext);
      for (const d of disposables) d.dispose();
      renderer.dispose();
      el.remove();
    },
  };
}

const clamp = (v: number, min: number, max: number) => Math.min(max, Math.max(min, v));

/** Корпус — как buildBlock в factory-3d.html: стены, межэтажные пояса и окна, крыша (плоская, двускатная, с рёбрами, с фонарём), офис со стеклом. */
function buildBlock(b: SiteBlock, palette: Palette, keep: <D extends { dispose: () => void }>(d: D) => D): Built {
  const mats: BuildingMats = {
    body: keep(new THREE.MeshStandardMaterial({ color: b.glass ? palette.glass : palette.wall, roughness: b.glass ? 0.35 : 0.85, metalness: 0 })),
    roof: keep(new THREE.MeshStandardMaterial({ color: palette.roof, roughness: 0.95 })),
    rib: keep(new THREE.MeshStandardMaterial({ color: palette.rib, roughness: 0.95 })),
    glass: keep(new THREE.MeshStandardMaterial({ color: palette.glass, roughness: 0.3, metalness: 0.2 })),
    frame: keep(new THREE.MeshStandardMaterial({ color: palette.frame, roughness: 0.6, metalness: 0.4 })),
  };
  const box = (w: number, h: number, d: number) => keep(new THREE.BoxGeometry(w, h, d));
  const group = new THREE.Group();
  group.position.set(b.x + b.w / 2, BASE_Y, b.z + b.d / 2);
  const pick: THREE.Object3D[] = [];

  const bodyGeo = box(b.w, b.h, b.d);
  const body = new THREE.Mesh(bodyGeo, mats.body);
  body.position.y = b.h / 2;
  body.castShadow = body.receiveShadow = true;
  group.add(body);
  pick.push(body);

  const floors = b.floors || 1;
  if (b.glass) {
    const fh = b.h / floors;
    for (let f = 1; f <= floors; f++) {
      const slab = new THREE.Mesh(box(b.w + 0.3, 0.45, b.d + 0.3), mats.frame);
      slab.position.y = Math.min(b.h, f * fh);
      group.add(slab);
    }
    const step = 3;
    const mx = box(0.18, b.h, 0.25);
    const mz = box(0.25, b.h, 0.18);
    for (let x = -b.w / 2 + step; x < b.w / 2; x += step) {
      for (const z of [b.d / 2 + 0.1, -b.d / 2 - 0.1]) {
        const m = new THREE.Mesh(mx, mats.frame);
        m.position.set(x, b.h / 2, z);
        group.add(m);
      }
    }
    for (let z = -b.d / 2 + step; z < b.d / 2; z += step) {
      for (const x of [b.w / 2 + 0.1, -b.w / 2 - 0.1]) {
        const m = new THREE.Mesh(mz, mats.frame);
        m.position.set(x, b.h / 2, z);
        group.add(m);
      }
    }
    // Парапет — материалом крыши: сверху видно статус корпуса, а не тёмную раму.
    const parapet = new THREE.Mesh(box(b.w + 0.4, 0.6, b.d + 0.4), mats.roof);
    parapet.position.y = b.h + 0.2;
    group.add(parapet);
  } else if (floors > 1) {
    const fh = b.h / floors;
    for (let f = 1; f < floors; f++) {
      const band = new THREE.Mesh(box(b.w + 0.1, 0.35, b.d + 0.1), mats.rib);
      band.position.y = f * fh;
      group.add(band);
    }
    for (let f = 0; f < floors; f++) {
      const win = new THREE.Mesh(box(b.w + 0.12, fh * 0.35, b.d + 0.12), mats.glass);
      win.position.y = f * fh + fh * 0.55;
      group.add(win);
    }
  }

  if (b.roof === "gable") {
    const rise = Math.min(b.w, b.d) * 0.22;
    const s = new THREE.Shape([new THREE.Vector2(-b.w / 2, 0), new THREE.Vector2(0, rise), new THREE.Vector2(b.w / 2, 0)]);
    const g = keep(new THREE.ExtrudeGeometry(s, { depth: b.d, bevelEnabled: false }));
    g.translate(0, b.h, -b.d / 2);
    const roof = new THREE.Mesh(g, mats.roof);
    roof.castShadow = true;
    group.add(roof);
    pick.push(roof);
  } else {
    const slab = new THREE.Mesh(box(b.w, 0.4, b.d), mats.roof);
    slab.position.y = b.h + 0.2;
    slab.receiveShadow = true;
    group.add(slab);
    pick.push(slab);
    if (b.roof === "ribbed") {
      const n = Math.max(2, Math.floor(b.d / 9));
      const ribGeo = box(b.w * 0.9, 0.5, 1.2);
      for (let i = 0; i < n; i++) {
        const rib = new THREE.Mesh(ribGeo, mats.rib);
        rib.position.set(0, b.h + 0.6, -b.d / 2 + (i + 0.5) * (b.d / n));
        group.add(rib);
      }
      const nv = Math.max(1, Math.floor(b.d / 18));
      const ventGeo = keep(new THREE.CylinderGeometry(0.9, 0.9, 1.6, 12));
      for (let i = 0; i < nv; i++) {
        const v = new THREE.Mesh(ventGeo, mats.rib);
        v.position.set(b.w * 0.3, b.h + 1.2, -b.d / 2 + (i + 0.5) * (b.d / nv));
        v.castShadow = true;
        group.add(v);
      }
    }
    if (b.roof === "skylight") {
      const along = b.d > b.w;
      const sk = new THREE.Mesh(box(along ? b.w * 0.3 : b.w * 0.85, 1.4, along ? b.d * 0.85 : b.d * 0.3), mats.glass);
      sk.position.y = b.h + 1.1;
      sk.castShadow = true;
      group.add(sk);
    }
  }

  const outline = new THREE.LineSegments(
    keep(new THREE.EdgesGeometry(keep(new THREE.BoxGeometry(b.w + 0.6, b.h + 0.8, b.d + 0.6)))),
    keep(new THREE.LineBasicMaterial({ color: palette.accent, transparent: true, opacity: 1 })),
  );
  outline.position.y = (b.h + 0.8) / 2;
  outline.visible = false;
  group.add(outline);

  const top = b.roof === "gable" ? b.h + Math.min(b.w, b.d) * 0.22 : b.h + 1.6;
  return { block: b, group, mats, outline, anchor: new THREE.Vector3(b.x + b.w / 2, BASE_Y + top + 1, b.z + b.d / 2), pick };
}
