import React from "react";
import { Sequence, Audio, staticFile } from "remotion";
import { ExplainerVideoProps, SceneType } from "./types";
import { BackgroundCanvas } from "./components/BackgroundCanvas";
import { TopHUD } from "./components/TopHUD";
import { ChapterProgressBar } from "./components/ChapterProgressBar";
import { DynamicSubtitles } from "./components/DynamicSubtitles";
import { HeroScene } from "./scenes/HeroScene";
import { FlowchartScene } from "./scenes/FlowchartScene";
import { CardComparisonScene } from "./scenes/CardComparisonScene";
import { MetricHighlightScene } from "./scenes/MetricHighlightScene";
import { CodeSnippetScene } from "./scenes/CodeSnippetScene";

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
}) => {
  const isPortrait = aspectRatio === "9:16";

  let currentFrameOffset = 0;

  return (
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
        primaryColor={theme?.primaryColor || "#3b82f6"}
        accentColor={theme?.accentColor || "#8b5cf6"}
      />

      {/* 2. Top HUD Header */}
      <TopHUD topic={topic} primaryColor={theme?.primaryColor || "#3b82f6"} />

      {/* 3. Audio Narration Track */}
      {audioPath && <Audio src={staticFile(audioPath)} />}

      {/* 4. Sequenced Scene Timeline */}
      {scenes.map((scene) => {
        const from = currentFrameOffset;
        const rawScene = scene as any;
        const duration = scene.durationInFrames ?? rawScene?.DurationInFrames ?? 150;
        currentFrameOffset += duration;

        const rawType = scene.type ?? rawScene?.Type;
        const category = scene.category ?? rawScene?.Category;
        const normalizedType = normalizeSceneType(rawType);
        const effectiveType = (category === "finance" && normalizedType === "code-snippet")
          ? "card-comparison"
          : normalizedType;

        return (
          <Sequence key={scene.id} from={from} durationInFrames={duration}>
            <div style={{ position: "relative", width: "100%", height: "100%" }}>
              {effectiveType === "flowchart" && (
                <FlowchartScene scene={scene} isPortrait={isPortrait} />
              )}
              {effectiveType === "card-comparison" && (
                <CardComparisonScene scene={scene} isPortrait={isPortrait} />
              )}
              {effectiveType === "metric-highlight" && (
                <MetricHighlightScene scene={scene} isPortrait={isPortrait} />
              )}
              {effectiveType === "code-snippet" && (
                <CodeSnippetScene scene={scene} isPortrait={isPortrait} />
              )}
              {(effectiveType === "hero" || effectiveType === "key-takeaway") && (
                <HeroScene scene={scene} isPortrait={isPortrait} />
              )}
            </div>
          </Sequence>
        );
      })}

      {/* 5. Dynamic Synchronized Subtitles */}
      <DynamicSubtitles subtitles={subtitles} isPortrait={isPortrait} />

      {/* 6. Bottom Chapter Progress Bar */}
      <ChapterProgressBar
        chapters={chapters}
        primaryColor={theme?.primaryColor || "#3b82f6"}
      />
    </div>
  );
};
