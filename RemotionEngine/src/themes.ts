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
