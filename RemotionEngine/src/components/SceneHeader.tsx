import React from "react";
import { spring, useCurrentFrame, useVideoConfig } from "remotion";
import { useStyle } from "../themeContext";

export const SceneHeader: React.FC<{ title: string; subtitle?: string; delay?: number }> = ({
  title,
  subtitle,
  delay = 0,
}) => {
  const frame = useCurrentFrame();
  const { fps } = useVideoConfig();
  const theme = useStyle();
  const progress = spring({
    frame: frame - delay,
    fps,
    config: theme.motion === "snappy"
      ? { damping: 30, stiffness: 240 }
      : theme.motion === "smooth"
        ? { damping: 22, stiffness: 80 }
        : { damping: 12, stiffness: 100 },
  });

  return (
    <div style={{ opacity: Math.max(0, progress), transform: `translateY(${(1 - progress) * 20}px)`, marginBottom: 24 }}>
      <h2
        style={{
          margin: 0,
          color: theme.palette.text,
          fontFamily: theme.fonts.heading,
          fontSize: 52,
          lineHeight: 1.12,
          fontWeight: 800,
          textAlign: "center",
        }}
      >
        {title}
      </h2>
      {subtitle && (
        <p
          style={{
            margin: "10px 0 0",
            color: theme.palette.muted,
            fontFamily: theme.fonts.body,
            fontSize: 20,
            textAlign: "center",
          }}
        >
          {subtitle}
        </p>
      )}
    </div>
  );
};
