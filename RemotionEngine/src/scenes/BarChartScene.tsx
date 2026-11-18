import React from "react";
import { interpolate, spring, useCurrentFrame, useVideoConfig } from "remotion";
import { ExplainerScene } from "../types";
import { useStyle } from "../themeContext";

export const BarChartScene: React.FC<{ scene: ExplainerScene; isPortrait?: boolean }> = ({ scene, isPortrait }) => {
  const frame = useCurrentFrame();
  const { fps } = useVideoConfig();
  const theme = useStyle();
  const items = scene.barItems?.length ? scene.barItems : [
    { label: "Alpha", value: 72, suffix: "%" },
    { label: "Beta", value: 54, suffix: "%" },
    { label: "Gamma", value: 88, suffix: "%" },
  ];
  const maxValue = Math.max(...items.map((item) => item.value), 1);

  return (
    <div style={{ height: "100%", display: "flex", flexDirection: "column", justifyContent: "center", alignItems: "center", padding: isPortrait ? "120px 44px 300px" : "80px 120px", fontFamily: theme.fonts.body }}>
      <h2 style={{ fontFamily: theme.fonts.heading, color: theme.palette.text, fontSize: isPortrait ? 44 : 48, margin: "0 0 42px" }}>{scene.title}</h2>
      <div style={{ display: "flex", alignItems: "flex-end", justifyContent: "center", gap: isPortrait ? 28 : 48, width: "100%", height: isPortrait ? 700 : 500, borderBottom: `3px solid ${theme.palette.muted}55` }}>
        {items.map((item, index) => {
          const timed = scene.wordTimings?.find((timing) => timing.itemIndex === index);
          const progress = timed
            ? interpolate((frame / fps) * 1000, [timed.start_ms, timed.end_ms], [0, 1], { extrapolateLeft: "clamp", extrapolateRight: "clamp" })
            : spring({ frame: frame - index * 8, fps, config: { damping: 14, stiffness: 100 } });
          const value = interpolate(Math.max(0, progress), [0, 1], [0, item.value]);
          const height = (item.value / maxValue) * (isPortrait ? 620 : 430) * Math.max(0, progress);
          return (
            <div key={item.label} style={{ width: isPortrait ? 180 : 220, height: "100%", display: "flex", flexDirection: "column", justifyContent: "flex-end", alignItems: "center" }}>
              <div style={{ color: theme.palette.text, fontSize: isPortrait ? 32 : 28, fontWeight: 900, marginBottom: 12 }}>{Math.round(value)}{item.suffix || ""}</div>
              <div style={{ width: "100%", height, minHeight: progress > 0 ? 3 : 0, background: `linear-gradient(180deg, ${theme.palette.primary}, ${theme.palette.accent})`, borderRadius: "14px 14px 0 0", boxShadow: `0 12px 32px ${theme.palette.primary}55` }} />
              <div style={{ width: "100%", minHeight: 58, display: "grid", placeItems: "center", color: theme.palette.text, fontSize: isPortrait ? 22 : 20, fontWeight: 700, textAlign: "center" }}>{item.label}</div>
            </div>
          );
        })}
      </div>
    </div>
  );
};
