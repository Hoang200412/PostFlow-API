import React from "react";
import { useCurrentFrame } from "remotion";
import { useStyle } from "../themeContext";
import { paletteTone } from "../themes";

interface TopHUDProps {
  topic: string;
  currentChapterTitle?: string;
  primaryColor?: string;
  isPortrait?: boolean;
  chapters?: import("../types").Chapter[];
}

export const TopHUD: React.FC<TopHUDProps> = ({
  topic,
  currentChapterTitle,
  primaryColor = "#3b82f6",
  isPortrait = false,
  chapters = [],
}) => {
  const frame = useCurrentFrame();
  const theme = useStyle();
  const tone = paletteTone(theme);
  const resolvedChapterTitle =
    currentChapterTitle ??
    chapters.find((chapter) => {
      const end = chapter.startFrame + chapter.durationInFrames;
      return frame >= chapter.startFrame && frame < end;
    })?.title;

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
        fontFamily: theme.fonts.body,
      }}
    >
      {/* Left: Branding & Topic */}
      <div style={{ display: "flex", alignItems: "center", gap: 14 }}>
        {!isPortrait && (
          <div
            style={{
              display: "flex",
              alignItems: "center",
              gap: 6,
              backgroundColor: tone.wash,
              border: `1px solid ${tone.line}`,
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
                fontSize: 14,
                fontWeight: 700,
                letterSpacing: "0.08em",
                textTransform: "uppercase",
                color: theme.palette.text,
              }}
            >
              PostFlow Explainer
            </span>
          </div>
        )}

        <span
          style={{
            fontSize: 18,
            fontWeight: 500,
            color: theme.palette.muted,
            maxWidth: isPortrait ? 520 : 700,
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
        {resolvedChapterTitle && (
          <div
            style={{
              backgroundColor: `${primaryColor}1f`,
              border: `1px solid ${primaryColor}55`,
              padding: "6px 14px",
              borderRadius: 8,
              fontSize: 16,
              fontWeight: 700,
              color: theme.palette.text,
            }}
          >
            {resolvedChapterTitle}
          </div>
        )}

      </div>
    </div>
  );
};
