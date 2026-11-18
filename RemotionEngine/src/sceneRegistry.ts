import React from "react";
import { z } from "zod";
import { ExplainerScene, SceneType } from "./types";
import { HeroScene } from "./scenes/HeroScene";
import { FlowchartScene } from "./scenes/FlowchartScene";
import { CardComparisonScene } from "./scenes/CardComparisonScene";
import { MetricHighlightScene } from "./scenes/MetricHighlightScene";
import { CodeSnippetScene } from "./scenes/CodeSnippetScene";
import { KeyTakeawayScene } from "./scenes/KeyTakeawayScene";
import { TimelineScene } from "./scenes/TimelineScene";
import { BarChartScene } from "./scenes/BarChartScene";
import { QuoteScene } from "./scenes/QuoteScene";

type SceneComponent = React.FC<{ scene: ExplainerScene; isPortrait?: boolean }>;

const baseSchema = z.object({
  id: z.string(),
  type: z.string(),
  title: z.string(),
  subtitle: z.string().optional(),
  durationInFrames: z.number().int().positive(),
  category: z.enum(["tech", "finance", "general"]).optional(),
  imageUrl: z.string().optional(),
  videoUrl: z.string().optional(),
  iconSvg: z.string().optional(),
  iconifyName: z.string().optional(),
  layout: z.enum(["fullbleed-image", "split-image"]).optional(),
}).passthrough();

export interface SceneRegistryEntry {
  component: SceneComponent;
  schema: z.ZodType;
  minFrames: number;
  maxFrames: number;
}

export const SCENE_REGISTRY: Record<SceneType, SceneRegistryEntry> = {
  hero: { component: HeroScene, schema: baseSchema, minFrames: 60, maxFrames: 900 },
  flowchart: { component: FlowchartScene, schema: baseSchema, minFrames: 90, maxFrames: 900 },
  "card-comparison": { component: CardComparisonScene, schema: baseSchema, minFrames: 90, maxFrames: 900 },
  "metric-highlight": { component: MetricHighlightScene, schema: baseSchema, minFrames: 90, maxFrames: 900 },
  "code-snippet": { component: CodeSnippetScene, schema: baseSchema, minFrames: 120, maxFrames: 1200 },
  "key-takeaway": { component: KeyTakeawayScene, schema: baseSchema, minFrames: 90, maxFrames: 900 },
  timeline: { component: TimelineScene, schema: baseSchema, minFrames: 120, maxFrames: 1200 },
  "bar-chart": { component: BarChartScene, schema: baseSchema, minFrames: 120, maxFrames: 1200 },
  quote: { component: QuoteScene, schema: baseSchema, minFrames: 60, maxFrames: 900 },
};

export function getSafeScene(rawScene: ExplainerScene): {
  scene: ExplainerScene;
  entry: SceneRegistryEntry;
  warned: boolean;
} {
  const rawType = String(rawScene?.type ?? "").trim().toLowerCase();
  const normalizedType = SCENE_REGISTRY[rawType as SceneType] ? rawType as SceneType : "hero";
  const entry = SCENE_REGISTRY[normalizedType];
  const result = entry.schema.safeParse(rawScene);

  if (!result.success) {
    console.warn("[ExplainerVideo] Invalid scene data; using safe hero fallback.", rawScene?.id, result.error.issues);
    return {
      scene: {
        id: String(rawScene?.id || "fallback-hero"),
        type: "hero",
        title: String(rawScene?.title || "Nội dung đang được cập nhật"),
        subtitle: "Vui lòng kiểm tra dữ liệu scene.",
        durationInFrames: Math.max(entry.minFrames, 150),
      },
      entry: SCENE_REGISTRY.hero,
      warned: true,
    };
  }

  const duration = Math.min(entry.maxFrames, Math.max(entry.minFrames, rawScene.durationInFrames ?? entry.minFrames));
  return { scene: { ...rawScene, type: normalizedType, durationInFrames: duration }, entry, warned: false };
}
