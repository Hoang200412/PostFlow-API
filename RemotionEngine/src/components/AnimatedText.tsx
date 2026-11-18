import React from "react";
import { interpolate, spring, useCurrentFrame, useVideoConfig } from "remotion";
import { useStyle } from "../themeContext";

export const AnimatedText: React.FC<{ children: React.ReactNode; delay?: number; as?: "h1" | "h2" | "p" | "span"; style?: React.CSSProperties }> = ({
  children,
  delay = 0,
  as = "span",
  style,
}) => {
  const frame = useCurrentFrame();
  const { fps } = useVideoConfig();
  const theme = useStyle();
  const progress = spring({ frame: frame - delay, fps, config: theme.motion === "snappy"
    ? { damping: 28, stiffness: 220 }
    : theme.motion === "smooth"
      ? { damping: 20, stiffness: 80 }
      : { damping: 12, stiffness: 100 } });
  const Component = as;

  return (
    <Component
      style={{
        display: "inline-block",
        opacity: Math.max(0, progress),
        transform: `translateY(${interpolate(Math.max(0, progress), [0, 1], [24, 0])}px)`,
        ...style,
      }}
    >
      {children}
    </Component>
  );
};
