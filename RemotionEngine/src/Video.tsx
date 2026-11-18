import React from "react";
import { AbsoluteFill, Audio, interpolate, staticFile, useCurrentFrame } from "remotion";
import { TransitionSeries, linearTiming, type TransitionPresentation } from "@remotion/transitions";
import { fade } from "@remotion/transitions/fade";
import { slide } from "@remotion/transitions/slide";
import { wipe } from "@remotion/transitions/wipe";
import { ExplainerScene, ExplainerVideoProps, SceneType } from "./types";
import { StyleProvider } from "./themeContext";
import { DEFAULT_STYLE_PACK, STYLE_PACKS } from "./themes";
import { BackgroundCanvas } from "./components/BackgroundCanvas";
import { TopHUD } from "./components/TopHUD";
import { ChapterProgressBar } from "./components/ChapterProgressBar";
import { DynamicSubtitles } from "./components/DynamicSubtitles";
import { HeroScene } from "./scenes/HeroScene";
import { FlowchartScene } from "./scenes/FlowchartScene";
import { CardComparisonScene } from "./scenes/CardComparisonScene";
import { MetricHighlightScene } from "./scenes/MetricHighlightScene";
import { CodeSnippetScene } from "./scenes/CodeSnippetScene";
import { SceneMedia } from "./components/SceneMedia";
import { getSafeScene } from "./sceneRegistry";

const TRANSITION_FRAMES = 12;
const KNOWN_SCENE_TYPES: SceneType[] = [
  "hero", "flowchart", "card-comparison", "metric-highlight", "code-snippet",
  "key-takeaway", "timeline", "bar-chart", "quote",
];

function normalizeSceneType(rawType?: string): SceneType {
  const lower = (rawType || "").toLowerCase().trim();
  if ((KNOWN_SCENE_TYPES as string[]).includes(lower)) return lower as SceneType;
  const t = lower.replace(/[-_\s]/g, "");
  // Specific types first, so "bar-chart" is not swallowed by the generic "chart" match.
  if (t.includes("barchart") || t.includes("bargraph") || t.includes("columnchart")) return "bar-chart";
  if (t.includes("timeline") || t.includes("chronology") || t.includes("milestone")) return "timeline";
  if (t.includes("quote") || t.includes("testimonial")) return "quote";
  if (t.includes("takeaway") || t.includes("summary") || t.includes("conclusion")) return "key-takeaway";
  if (t.includes("flow") || t.includes("chart") || t.includes("arch") || t.includes("diagram") || t.includes("process") || t.includes("pipeline") || t.includes("workflow")) {
    return "flowchart";
  }
  if (t.includes("compar") || t.includes("card") || t.includes("versus") || t === "vs" || t.includes("table")) {
    return "card-comparison";
  }
  if (t.includes("metric") || t.includes("stat") || t.includes("number") || t.includes("highlight") || t.includes("benchmark")) {
    return "metric-highlight";
  }
  if (t.includes("code") || t.includes("snippet") || t.includes("terminal") || t.includes("syntax") || t.includes("script")) {
    return "code-snippet";
  }
  // Default fallback - never leave the screen blank!
  return "hero";
}

/** Rotate through a few presentations so consecutive cuts don't feel repetitive. */
function getTransitionPresentation(index: number, preferred: "fade" | "slide" | "wipe"): TransitionPresentation<Record<string, unknown>> {
  const variants: Array<() => TransitionPresentation<Record<string, unknown>>> = [
    () => (preferred === "fade" ? fade() : preferred === "slide" ? slide({ direction: "from-right" }) : wipe({ direction: "from-left" })) as TransitionPresentation<Record<string, unknown>>,
    () => fade() as TransitionPresentation<Record<string, unknown>>,
    () => slide({ direction: index % 2 === 0 ? "from-bottom" : "from-left" }) as TransitionPresentation<Record<string, unknown>>,
    () => wipe({ direction: "from-top-left" }) as TransitionPresentation<Record<string, unknown>>,
  ];
  return variants[index % variants.length]();
}

/** Gentle camera drift + short exit so scene content doesn't sit statically. */
const SceneMotion: React.FC<{ durationInFrames: number; children: React.ReactNode }> = ({ durationInFrames, children }) => {
  const frame = useCurrentFrame();
  const scale = interpolate(frame, [0, durationInFrames], [1, 1.035], { extrapolateRight: "clamp" });
  const exitStart = Math.max(0, durationInFrames - 10);
  const exit = interpolate(frame, [exitStart, durationInFrames], [0, 1], { extrapolateLeft: "clamp", extrapolateRight: "clamp" });
  return (
    <div style={{ width: "100%", height: "100%", transform: `scale(${scale * (1 - exit * 0.03)})`, opacity: 1 - exit * 0.35 }}>
      {children}
    </div>
  );
};

export const ExplainerVideo: React.FC<ExplainerVideoProps> = ({
  topic,
  aspectRatio,
  audioPath,
  subtitles = [],
  chapters = [],
  scenes = [],
  theme,
  backgroundStyle,
  stylePack,
  stylePackId,
}) => {
  const isPortrait = aspectRatio === "9:16";
  const resolvedStyle = stylePack ?? (stylePackId ? STYLE_PACKS[stylePackId] : undefined) ?? DEFAULT_STYLE_PACK;
  const sceneTimeline = scenes.reduce<{ offset: number; items: Array<{ scene: ExplainerScene; from: number; duration: number }> }>(
    (timeline, scene) => {
      const duration = scene.durationInFrames ?? 150;
      timeline.items.push({ scene, from: timeline.offset, duration });
      timeline.offset += duration;
      return timeline;
    },
    { offset: 0, items: [] }
  ).items;
  const timelineChildren = sceneTimeline.reduce<React.ReactNode[]>(
    (children, { scene, duration }, index) => {
      if (index > 0) {
        children.push(
          <TransitionSeries.Transition
            key={`transition-${scene.id}`}
            timing={linearTiming({ durationInFrames: TRANSITION_FRAMES })}
            presentation={getTransitionPresentation(index, resolvedStyle.transition)}
          />
        );
      }

      const normalizedScene = normalizeSceneType(scene.type);
      const safeResult = getSafeScene({ ...scene, type: normalizedScene });
      const SceneComponent = safeResult.entry.component;
      // Keep the backend-provided duration (aligned to narration); the registry's
      // min/max clamp would otherwise shift every following scene away from the audio.
      const safeScene = { ...safeResult.scene, durationInFrames: duration };
      const splitMedia = safeScene.layout === "split-image";
      // Each transition overlaps the next scene by TRANSITION_FRAMES, so extend every
      // scene but the last by that amount: scene N then starts exactly at sum(durations).
      const isLast = index === sceneTimeline.length - 1;
      const sequenceDuration = duration + (isLast ? 0 : TRANSITION_FRAMES);

      children.push(
        <TransitionSeries.Sequence key={safeScene.id} durationInFrames={sequenceDuration}>
          <AbsoluteFill>
            <SceneMedia scene={safeScene} isPortrait={isPortrait} durationInFrames={sequenceDuration} />
            <div
              style={{
                position: "absolute",
                inset: splitMedia ? "0 0 0 45%" : 0,
                zIndex: 2,
              }}
            >
              <SceneMotion durationInFrames={sequenceDuration}>
                <SceneComponent scene={safeScene} isPortrait={isPortrait} />
              </SceneMotion>
            </div>
          </AbsoluteFill>
        </TransitionSeries.Sequence>
      );
      return children;
    },
    []
  );

  return (
    <StyleProvider pack={resolvedStyle}>
    <div
      style={{
        position: "relative",
        width: "100%",
        height: "100%",
        overflow: "hidden",
        backgroundColor: resolvedStyle.palette.bg,
      }}
    >
      {/* 1. Global Ambient Background */}
      <BackgroundCanvas
        primaryColor={theme?.primaryColor || resolvedStyle.palette.primary}
        accentColor={theme?.accentColor || resolvedStyle.palette.accent}
        style={backgroundStyle ?? resolvedStyle.background}
      />

      {/* 2. Top HUD Header */}
      <TopHUD
        topic={topic}
        chapters={chapters}
        isPortrait={isPortrait}
        primaryColor={theme?.primaryColor || resolvedStyle.palette.primary}
      />

      {/* 3. Audio Narration Track */}
      {audioPath && <Audio src={staticFile(audioPath)} />}

      {/* 4. Sequenced Scene Timeline */}
      <TransitionSeries>{timelineChildren}</TransitionSeries>

      {/* 5. Dynamic Synchronized Subtitles */}
      <DynamicSubtitles subtitles={subtitles} isPortrait={isPortrait} />

      {/* 6. Bottom Chapter Progress Bar */}
      <ChapterProgressBar
        chapters={chapters}
        primaryColor={theme?.primaryColor || resolvedStyle.palette.primary}
      />
    </div>
    </StyleProvider>
  );
};
