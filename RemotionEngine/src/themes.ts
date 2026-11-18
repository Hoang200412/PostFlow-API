import { loadFont as loadBeVietnamPro, fontFamily as beVietnamPro } from "@remotion/google-fonts/BeVietnamPro";
import { loadFont as loadSpaceGrotesk, fontFamily as spaceGrotesk } from "@remotion/google-fonts/SpaceGrotesk";
import { loadFont as loadManrope, fontFamily as manrope } from "@remotion/google-fonts/Manrope";
import { loadFont as loadDMSerifDisplay, fontFamily as dmSerifDisplay } from "@remotion/google-fonts/DMSerifDisplay";

loadBeVietnamPro("normal", { weights: ["400", "600", "700", "800", "900"], subsets: ["latin", "vietnamese"] });
loadSpaceGrotesk("normal", { weights: ["600", "700"], subsets: ["latin", "latin-ext"] });
loadManrope("normal", { weights: ["400", "600", "700", "800"], subsets: ["latin", "latin-ext"] });
loadDMSerifDisplay("normal", { weights: ["400"], subsets: ["latin", "latin-ext"] });

export type CardStyle = "glass" | "flat" | "outline" | "neo";
export type MotionStyle = "springy" | "smooth" | "snappy";

export interface StylePack {
  palette: {
    bg: string;
    surface: string;
    primary: string;
    accent: string;
    text: string;
    muted: string;
  };
  fonts: {
    heading: string;
    body: string;
  };
  background: "aurora" | "dotgrid" | "paper" | "mesh" | "none";
  cardStyle: CardStyle;
  motion: MotionStyle;
  transition: "fade" | "slide" | "wipe";
}

export const STYLE_PACKS: Record<string, StylePack> = {
  neon: {
    palette: { bg: "#07090e", surface: "#111827", primary: "#38bdf8", accent: "#a78bfa", text: "#f8fafc", muted: "#94a3b8" },
    fonts: { heading: spaceGrotesk, body: beVietnamPro },
    background: "mesh",
    cardStyle: "glass",
    motion: "springy",
    transition: "slide",
  },
  paper: {
    palette: { bg: "#f7f3e8", surface: "#fffdf7", primary: "#1f2937", accent: "#b45309", text: "#1f2937", muted: "#64748b" },
    fonts: { heading: dmSerifDisplay, body: beVietnamPro },
    background: "paper",
    cardStyle: "flat",
    motion: "smooth",
    transition: "fade",
  },
  finance: {
    palette: { bg: "#090a0c", surface: "#17140d", primary: "#eab308", accent: "#10b981", text: "#fff7ed", muted: "#a3a3a3" },
    fonts: { heading: spaceGrotesk, body: beVietnamPro },
    background: "aurora",
    cardStyle: "neo",
    motion: "snappy",
    transition: "wipe",
  },
  education: {
    palette: { bg: "#fff7fb", surface: "#ffffff", primary: "#ec4899", accent: "#8b5cf6", text: "#3b2b4f", muted: "#7c6a93" },
    fonts: { heading: manrope, body: beVietnamPro },
    background: "dotgrid",
    cardStyle: "outline",
    motion: "springy",
    transition: "fade",
  },
  newsroom: {
    palette: { bg: "#050505", surface: "#111111", primary: "#ef4444", accent: "#facc15", text: "#ffffff", muted: "#b3b3b3" },
    fonts: { heading: manrope, body: beVietnamPro },
    background: "none",
    cardStyle: "outline",
    motion: "snappy",
    transition: "wipe",
  },
  lifestyle: {
    palette: { bg: "#fff5ed", surface: "#fffaf6", primary: "#f97316", accent: "#db2777", text: "#4a2718", muted: "#8b6f62" },
    fonts: { heading: dmSerifDisplay, body: beVietnamPro },
    background: "aurora",
    cardStyle: "glass",
    motion: "smooth",
    transition: "slide",
  },
};

export const DEFAULT_STYLE_PACK = STYLE_PACKS.neon;

function hexLuminance(hex: string): number {
  const h = hex.replace("#", "");
  const full = h.length === 3 ? h.split("").map((c) => c + c).join("") : h;
  const r = parseInt(full.slice(0, 2), 16) / 255;
  const g = parseInt(full.slice(2, 4), 16) / 255;
  const b = parseInt(full.slice(4, 6), 16) / 255;
  return 0.2126 * r + 0.7152 * g + 0.0722 * b;
}

export function isLightPack(pack: StylePack): boolean {
  return hexLuminance(pack.palette.bg) > 0.55;
}

/** Theme-aware neutral colors so scenes stay readable on both dark and light packs. */
export function paletteTone(pack: StylePack) {
  const light = isLightPack(pack);
  return {
    light,
    /** Hairline borders / dividers */
    line: light ? "rgba(15, 23, 42, 0.14)" : "rgba(255, 255, 255, 0.15)",
    /** Faint filled chips / icon wells */
    wash: light ? "rgba(15, 23, 42, 0.06)" : "rgba(255, 255, 255, 0.06)",
    /** Strong text on surface */
    strong: pack.palette.text,
    /** Body text on surface */
    soft: light ? "#334155" : "#e2e8f0",
    /** Secondary text */
    muted: pack.palette.muted,
    /** Text shadow / drop shadow base */
    shadow: light ? "rgba(15, 23, 42, 0.18)" : "rgba(0, 0, 0, 0.55)",
    /** Text on top of a filled primary/accent chip */
    onChip: "#ffffff",
    /** Floating chip background */
    chipBg: light ? "rgba(255, 255, 255, 0.85)" : "rgba(15, 23, 42, 0.75)",
  };
}
