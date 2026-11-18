import React from "react";
import { useCurrentFrame, useVideoConfig, spring, interpolate } from "remotion";
import { ExplainerScene } from "../types";
import { useStyle } from "../themeContext";

export const KeyTakeawayScene: React.FC<{ scene: ExplainerScene; isPortrait?: boolean }> = ({ scene, isPortrait }) => {
  const frame = useCurrentFrame();
  const { fps } = useVideoConfig();
  const theme = useStyle();
  const items = scene.takeaways?.length ? scene.takeaways : ["Điểm chính 1", "Điểm chính 2", "Điểm chính 3"];

  return (
    <div style={{ height: "100%", display: "flex", flexDirection: "column", justifyContent: "center", alignItems: "center", padding: isPortrait ? "0 44px" : "0 120px", fontFamily: theme.fonts.body }}>
      <h2 style={{ fontFamily: theme.fonts.heading, color: theme.palette.text, fontSize: isPortrait ? 42 : 48, margin: "0 0 34px" }}>{scene.title}</h2>
      <div style={{ display: "flex", flexDirection: "column", gap: isPortrait ? 18 : 16, width: "100%", maxWidth: isPortrait ? 760 : 1200 }}>
        {items.map((item, index) => {
          const timed = scene.wordTimings?.find((timing) => timing.itemIndex === index);
          const progress = timed
            ? interpolate((frame / fps) * 1000, [timed.start_ms, timed.end_ms], [0, 1], { extrapolateLeft: "clamp", extrapolateRight: "clamp" })
            : spring({ frame: frame - index * 8, fps, config: { damping: 14, stiffness: 100 } });
          return (
            <div key={index} style={{ display: "flex", alignItems: "center", gap: 18, opacity: Math.max(0, progress), transform: `translateX(${interpolate(Math.max(0, progress), [0, 1], [-30, 0])}px)`, background: theme.palette.surface, border: `1px solid ${theme.palette.primary}55`, borderRadius: 18, padding: isPortrait ? "18px 20px" : "18px 26px" }}>
              <div style={{ flexShrink: 0, width: isPortrait ? 54 : 48, height: isPortrait ? 54 : 48, borderRadius: "50%", display: "grid", placeItems: "center", background: theme.palette.primary, color: theme.palette.bg, fontWeight: 900, fontSize: isPortrait ? 24 : 20 }}>{index + 1}</div>
              <span style={{ color: theme.palette.text, fontSize: isPortrait ? 30 : 26, lineHeight: 1.25, fontWeight: 700 }}>{item}</span>
            </div>
          );
        })}
      </div>
    </div>
  );
};
