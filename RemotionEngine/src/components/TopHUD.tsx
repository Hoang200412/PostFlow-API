import React from "react";
import { useCurrentFrame, useVideoConfig } from "remotion";

interface TopHUDProps {
  topic: string;
  currentChapterTitle?: string;
  primaryColor?: string;
}

export const TopHUD: React.FC<TopHUDProps> = ({
  topic,
  currentChapterTitle,
  primaryColor = "#3b82f6",
}) => {
  const frame = useCurrentFrame();
  const { fps } = useVideoConfig();

  const totalSeconds = Math.floor(frame / fps);
  const minutes = Math.floor(totalSeconds / 60);
  const seconds = totalSeconds % 60;
  const timeString = `${String(minutes).padStart(2, "0")}:${String(seconds).padStart(2, "0")}`;

  return (
    <div
      style={{
        position: "absolute",
        top: 28,
        left: 40,
        right: 40,
        display: "flex",
        justifyContent: "space-between",
        alignItems: "center",
        zIndex: 50,
        fontFamily: "'Inter', system-ui, -apple-system, sans-serif",
      }}
    >
      {/* Left: Branding & Topic */}
      <div style={{ display: "flex", alignItems: "center", gap: 14 }}>
        <div
          style={{
            display: "flex",
            alignItems: "center",
            gap: 6,
            backgroundColor: "rgba(255, 255, 255, 0.08)",
            border: "1px solid rgba(255, 255, 255, 0.15)",
            backdropFilter: "blur(12px)",
            padding: "6px 14px",
            borderRadius: 20,
          }}
        >
          <div
            style={{
              width: 8,
              height: 8,
              borderRadius: "50%",
              backgroundColor: primaryColor,
              boxShadow: `0 0 8px ${primaryColor}`,
            }}
          />
          <span
            style={{
              fontSize: 12,
              fontWeight: 700,
              letterSpacing: "0.08em",
              textTransform: "uppercase",
              color: "#e2e8f0",
            }}
          >
            PostFlow Explainer
          </span>
        </div>

        <span
          style={{
            fontSize: 14,
            fontWeight: 500,
            color: "#94a3b8",
            maxWidth: 400,
            whiteSpace: "nowrap",
            overflow: "hidden",
            textOverflow: "ellipsis",
          }}
        >
          {topic}
        </span>
      </div>

      {/* Right: Chapter Title & Frame Timer */}
      <div style={{ display: "flex", alignItems: "center", gap: 12 }}>
        {currentChapterTitle && (
          <div
            style={{
              backgroundColor: "rgba(59, 130, 246, 0.12)",
              border: "1px solid rgba(59, 130, 246, 0.3)",
              padding: "5px 12px",
              borderRadius: 8,
              fontSize: 12,
              fontWeight: 600,
              color: "#93c5fd",
            }}
          >
            {currentChapterTitle}
          </div>
        )}

        <div
          style={{
            backgroundColor: "rgba(0, 0, 0, 0.4)",
            border: "1px solid rgba(255, 255, 255, 0.1)",
            padding: "5px 10px",
            borderRadius: 8,
            fontSize: 13,
            fontFamily: "monospace",
            color: "#64748b",
          }}
        >
          {timeString}
        </div>
      </div>
    </div>
  );
};

