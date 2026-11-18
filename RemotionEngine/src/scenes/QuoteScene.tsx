import React from "react";
import { interpolate, spring, useCurrentFrame, useVideoConfig } from "remotion";
import { ExplainerScene } from "../types";
import { useStyle } from "../themeContext";

export const QuoteScene: React.FC<{ scene: ExplainerScene; isPortrait?: boolean }> = ({ scene, isPortrait }) => {
  const frame = useCurrentFrame();
  const { fps } = useVideoConfig();
  const theme = useStyle();
  const quote = scene.quote || { quote: "Sự đơn giản là đỉnh cao của hiệu quả.", author: "Người truyền cảm hứng" };
  const progress = spring({ frame, fps, config: theme.motion === "snappy"
    ? { damping: 28, stiffness: 220 }
    : theme.motion === "smooth"
      ? { damping: 20, stiffness: 80 }
      : { damping: 12, stiffness: 100 } });

  return (
    <div style={{ height: "100%", display: "grid", placeItems: "center", padding: isPortrait ? "140px 54px 320px" : "100px 180px", textAlign: "center", fontFamily: theme.fonts.body }}>
      <div style={{ maxWidth: isPortrait ? 780 : 1250, opacity: Math.max(0, progress), transform: `scale(${interpolate(Math.max(0, progress), [0, 1], [0.94, 1])})` }}>
        <div style={{ color: theme.palette.primary, fontFamily: theme.fonts.heading, fontSize: isPortrait ? 120 : 100, lineHeight: 0.5 }}>&ldquo;</div>
        <blockquote style={{ margin: "20px 0", color: theme.palette.text, fontFamily: theme.fonts.heading, fontSize: isPortrait ? 48 : 58, lineHeight: 1.25, fontWeight: 700 }}>{quote.quote}</blockquote>
        {quote.author && <div style={{ color: theme.palette.accent, fontSize: isPortrait ? 26 : 24, fontWeight: 800 }}>— {quote.author}</div>}
        {quote.role && <div style={{ color: theme.palette.muted, marginTop: 8, fontSize: isPortrait ? 22 : 20 }}>{quote.role}</div>}
      </div>
    </div>
  );
};
