import React from "react";
import { AbsoluteFill, Audio, staticFile } from "remotion";
import { TransitionSeries, linearTiming } from "@remotion/transitions";
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

function normalizeSceneType(rawType?: string): SceneType {
  const t = (rawType || "").toLowerCase().trim().replace(/[-_]/g, "");
  if (t.includes("flow") || t.includes("chart") || t.includes("arch") || t.includes("diagram") || t.includes("process") || t.includes("pipeline") || t.includes("workflow")) {
    return "flowchart";
  }
  if (t.includes("compar") || t.includes("card") || t.includes("versus") || t.includes("vs") || t.includes("table")) {
    return "card-comparison";
  }
  if (t.includes("metric") || t.includes("stat") || t.includes("number") || t.includes("highlight") || t.includes("benchmark")) {
    return "metric-highlight";
  }
  if (t.includes("code") || t.includes("snippet") || t.includes("terminal") || t.includes("syntax") || t.includes("script")) {
    return "code-snippet";
  }
  if (t.includes("takeaway") || t.includes("summary") || t.includes("conclusion")) {
    return "key-takeaway";
  }
  if (t.includes("timeline") || t.includes("chronology") || t.includes("milestone")) return "timeline";
  if (t.includes("barchart") || t.includes("bargraph") || t.includes("columnchart")) return "bar-chart";
  if (t.includes("quote") || t.includes("testimonial")) return "quote";
  // Default fallback - never leave the screen blank!
  return "hero";
}

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
        if (resolvedStyle.transition === "fade") {
          children.push(
            <TransitionSeries.Transition
              key={`fade-${scene.id}`}
              timing={linearTiming({ durationInFrames: 12 })}
              presentation={fade()}
            />
          );
        } else if (resolvedStyle.transition === "slide") {
          children.push(
            <TransitionSeries.Transition
              key={`slide-${scene.id}`}
              timing={linearTiming({ durationInFrames: 12 })}
              presentation={slide({ direction: "from-right" })}
            />
          );
        } else {
          children.push(
            <TransitionSeries.Transition
              key={`wipe-${scene.id}`}
              timing={linearTiming({ durationInFrames: 12 })}
              presentation={wipe({ direction: "from-left" })}
            />
          );
        }
      }

      const normalizedScene = normalizeSceneType(scene.type);
      const safeResult = getSafeScene({ ...scene, type: normalizedScene });
      const SceneComponent = safeResult.entry.component;
      const safeScene = safeResult.scene;
      const splitMedia = safeScene.layout === "split-image";

      children.push(
        <TransitionSeries.Sequence key={safeScene.id} durationInFrames={duration}>
          <AbsoluteFill>
            <SceneMedia scene={safeScene} isPortrait={isPortrait} durationInFrames={duration} />
            <div
              style={{
                position: "absolute",
                inset: splitMedia ? "0 0 0 45%" : 0,
                zIndex: 2,
              }}
            >
              <SceneComponent scene={safeScene} isPortrait={isPortrait} />
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
        backgroundColor: "#07090e",
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
