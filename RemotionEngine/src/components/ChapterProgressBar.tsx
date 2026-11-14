import React from "react";
import { useCurrentFrame, useVideoConfig } from "remotion";
import { Chapter } from "../types";

interface ChapterProgressBarProps {
  chapters: Chapter[];
  primaryColor?: string;
}

export const ChapterProgressBar: React.FC<ChapterProgressBarProps> = ({
  chapters,
  primaryColor = "#3b82f6",
}) => {
  const frame = useCurrentFrame();
  const { durationInFrames } = useVideoConfig();

  if (!chapters || chapters.length === 0) {
    const progress = Math.min(100, Math.max(0, (frame / durationInFrames) * 100));
    return (
      <div
        style={{
          position: "absolute",
          bottom: 0,
          left: 0,
          right: 0,
          height: 4,
          backgroundColor: "rgba(255, 255, 255, 0.1)",
          zIndex: 50,
        }}
      >
        <div
          style={{
            height: "100%",
            width: `${progress}%`,
            backgroundColor: primaryColor,
            boxShadow: `0 0 10px ${primaryColor}`,
          }}
        />
      </div>
    );
  }

  return (
    <div
      style={{
        position: "absolute",
        bottom: 12,
        left: 40,
        right: 40,
        display: "flex",
        gap: 6,
        alignItems: "center",
        zIndex: 50,
      }}
    >
      {chapters.map((ch, idx) => {
        const chStart = ch.startFrame;
        const chEnd = ch.startFrame + ch.durationInFrames;
        let pct = 0;
        if (frame >= chEnd) {
          pct = 100;
        } else if (frame > chStart) {
          pct = ((frame - chStart) / ch.durationInFrames) * 100;
        }

        return (
          <div
            key={ch.id || idx}
            style={{
              flex: 1,
              height: 4,
              backgroundColor: "rgba(255, 255, 255, 0.12)",
              borderRadius: 2,
              overflow: "hidden",
              position: "relative",
            }}
          >
            <div
              style={{
                height: "100%",
                width: `${pct}%`,
                backgroundColor: primaryColor,
                borderRadius: 2,
                boxShadow: pct > 0 ? `0 0 8px ${primaryColor}` : "none",
              }}
            />
          </div>
        );
      })}
    </div>
  );
};

