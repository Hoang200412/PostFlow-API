import React from "react";
import { evolvePath } from "@remotion/paths";
import { interpolate, spring, useCurrentFrame, useVideoConfig } from "remotion";
import { ExplainerScene } from "../types";
import { useStyle } from "../themeContext";

export const TimelineScene: React.FC<{ scene: ExplainerScene; isPortrait?: boolean }> = ({ scene, isPortrait }) => {
  const frame = useCurrentFrame();
  const { fps } = useVideoConfig();
  const theme = useStyle();
  const items = scene.timelineItems?.length ? scene.timelineItems : [
    { date: "01", label: "Khởi đầu", description: "Xác định mục tiêu" },
    { date: "02", label: "Triển khai", description: "Thực hiện từng bước" },
    { date: "03", label: "Tăng tốc", description: "Đo lường kết quả" },
  ];
  const path = isPortrait ? "M 540 120 L 540 1750" : "M 120 540 L 1800 540";
  const lineProgress = interpolate(frame, [0, 50], [0, 1], { extrapolateLeft: "clamp", extrapolateRight: "clamp" });
  const evolved = evolvePath(lineProgress, path);

  return (
    <div style={{ height: "100%", position: "relative", fontFamily: theme.fonts.body, padding: 60 }}>
      <h2 style={{ position: "absolute", top: isPortrait ? 100 : 60, left: 0, width: "100%", textAlign: "center", color: theme.palette.text, fontFamily: theme.fonts.heading, fontSize: isPortrait ? 44 : 48 }}>{scene.title}</h2>
      <svg width="100%" height="100%" style={{ position: "absolute", inset: 0 }}>
        <path d={path} fill="none" stroke={theme.palette.primary} strokeWidth={isPortrait ? 8 : 6} strokeDasharray={evolved.strokeDasharray} strokeDashoffset={evolved.strokeDashoffset} />
      </svg>
      {items.map((item, index) => {
        const timed = scene.wordTimings?.find((timing) => timing.itemIndex === index);
        const progress = timed
          ? interpolate((frame / fps) * 1000, [timed.start_ms, timed.end_ms], [0, 1], { extrapolateLeft: "clamp", extrapolateRight: "clamp" })
          : spring({ frame: frame - index * 12 - 15, fps, config: { damping: 14, stiffness: 100 } });
        const verticalTop = 240 + (index * (isPortrait ? 420 : 0));
        const left = isPortrait ? "50%" : `${16 + index * (68 / Math.max(1, items.length - 1))}%`;
        return (
          <div key={index} style={{ position: "absolute", left, top: isPortrait ? verticalTop : "50%", transform: `translate(-50%, ${isPortrait ? "-25%" : "-20%"}) scale(${Math.max(0, progress)})`, width: isPortrait ? 760 : 360, opacity: Math.max(0, progress), background: theme.palette.surface, border: `2px solid ${theme.palette.primary}`, borderRadius: 20, padding: 22, color: theme.palette.text, textAlign: "center" }}>
            <div style={{ color: theme.palette.primary, fontWeight: 900, fontSize: 18 }}>{item.date || `Bước ${index + 1}`}</div>
            <div style={{ fontFamily: theme.fonts.heading, fontSize: 28, fontWeight: 800, marginTop: 4 }}>{item.label}</div>
            {item.description && <div style={{ color: theme.palette.muted, marginTop: 6 }}>{item.description}</div>}
          </div>
        );
      })}
    </div>
  );
};
