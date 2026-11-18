import React from "react";
import { useCurrentFrame, useVideoConfig, spring, interpolate } from "remotion";
import { ExplainerScene } from "../types";
import { TechVisualIcon } from "../components/TechVisualIcon";
import { useStyle } from "../themeContext";

export const CardComparisonScene: React.FC<{ scene: ExplainerScene; isPortrait?: boolean }> = ({
  scene,
  isPortrait = false,
}) => {
  const frame = useCurrentFrame();
  const { fps } = useVideoConfig();

  const theme = useStyle();

  const defaultTechCards = [
    {
      name: "Giải Pháp Cũ",
      badge: "Truyền Thống",
      icon: "server",
      pros: ["Dễ tiếp cận ban đầu", "Nhiều tài liệu cũ"],
      cons: ["Khởi động tính bằng phút", "Chiếm dụng tài nguyên lớn"],
      isWinner: false,
    },
    {
      name: "Kiến Trúc Đột Phá",
      badge: "Đột Phá",
      icon: "zap",
      pros: ["Khởi động chỉ vài mili-giây", "Tối ưu hóa 90% tài nguyên", "Auto-scaling linh hoạt"],
      cons: [],
      isWinner: true,
    },
  ];

  const cards = scene.comparisonCards && scene.comparisonCards.length > 0
    ? scene.comparisonCards
    : defaultTechCards;

  const headerProgress = spring({
    frame,
    fps,
    config: { damping: 14, stiffness: 90 },
  });

  return (
    <div
      style={{
        display: "flex",
        flexDirection: "column",
        alignItems: "center",
        justifyContent: "center",
        height: "100%",
        padding: isPortrait ? "0 30px" : "0 80px",
        fontFamily: theme.fonts.body,
      }}
    >
      {/* Header */}
      <div
        style={{
          textAlign: "center",
          marginBottom: isPortrait ? 26 : 46,
          opacity: headerProgress,
          transform: `translateY(${interpolate(headerProgress, [0, 1], [-20, 0])}px)`,
        }}
      >
        <h2
          style={{
            fontSize: isPortrait ? 38 : 46,
            fontWeight: 800,
            color: theme.palette.text,
            fontFamily: theme.fonts.heading,
            margin: "0 0 10px 0",
            letterSpacing: "-0.02em",
          }}
        >
          {scene.title}
        </h2>
        {scene.subtitle && (
          <p style={{ fontSize: isPortrait ? 20 : 20, color: theme.palette.muted, margin: 0, fontWeight: 500 }}>
            {scene.subtitle}
          </p>
        )}
      </div>

      {/* Comparison Grid */}
      <div
        style={{
          display: "flex",
          flexDirection: isPortrait ? "column" : "row",
          gap: isPortrait ? 20 : 36,
          width: "100%",
          maxWidth: isPortrait ? 560 : 1180,
          justifyContent: "center",
          alignItems: "stretch",
        }}
      >
        {cards.map((card, idx) => {
          const delay = idx * 12;
          const cardProgress = spring({
            frame: frame - delay,
            fps,
            config: { damping: 12, stiffness: 100 },
          });

          const isWin = card.isWinner ?? idx === 1;
          const borderColor = isWin ? "#10b981" : "rgba(255, 255, 255, 0.15)";
          const glowColor = isWin ? "rgba(16, 185, 129, 0.3)" : "rgba(0, 0, 0, 0.4)";
          const cardIcon = card.icon || (isWin ? "zap" : "harddrive");

          const winnerPulse = isWin
            ? interpolate(Math.sin(frame / 12), [-1, 1], [0.99, 1.02])
            : 1;

          return (
            <div
              key={idx}
              style={{
                flex: 1,
                opacity: Math.max(0, cardProgress),
                transform: `scale(${Math.max(0, cardProgress) * winnerPulse})`,
                backgroundColor: isWin ? `${theme.palette.primary}18` : theme.palette.surface,
                border: `2px solid ${borderColor}`,
                borderRadius: 24,
                padding: isPortrait ? "22px 24px" : "32px 36px",
                boxShadow: `0 18px 45px ${glowColor}`,
                display: "flex",
                flexDirection: "column",
                position: "relative",
              }}
            >
              {/* Winner Tag / Badge */}
              <div
                style={{
                  display: "flex",
                  alignItems: "center",
                  justifyContent: "space-between",
                  marginBottom: 16,
                }}
              >
                <div
                  style={{
                    backgroundColor: isWin ? "#10b981" : "#475569",
                    color: "#ffffff",
                    fontSize: 12,
                    fontWeight: 800,
                    padding: "4px 14px",
                    borderRadius: 14,
                    textTransform: "uppercase",
                    letterSpacing: "0.08em",
                    boxShadow: isWin ? "0 4px 14px rgba(16, 185, 129, 0.4)" : "none",
                  }}
                >
                  {isWin ? "★ " + (card.badge || "KHUYÊN DÙNG") : (card.badge || "TRUYỀN THỐNG")}
                </div>

                {/* Animated Tech Visual Icon in Card Header */}
                <div
                  style={{
                    width: 44,
                    height: 44,
                    borderRadius: 12,
                    backgroundColor: "rgba(255, 255, 255, 0.05)",
                    display: "flex",
                    alignItems: "center",
                    justifyContent: "center",
                    border: `1px solid ${borderColor}`,
                  }}
                >
                  <TechVisualIcon
                    name={cardIcon}
                    size={26}
                    primaryColor={isWin ? "#10b981" : "#94a3b8"}
                    delay={delay}
                    showOrbitalRings={false}
                  />
                </div>
              </div>

              {/* Card Title */}
              <h3
                style={{
                  fontSize: isPortrait ? 24 : 28,
                  fontWeight: 800,
                  color: isWin ? "#ffffff" : "#cbd5e1",
                  margin: "0 0 20px 0",
                  letterSpacing: "-0.01em",
                }}
              >
                {card.name}
              </h3>

              {/* Pros List */}
              <div style={{ display: "flex", flexDirection: "column", gap: 12, marginBottom: card.cons && card.cons.length > 0 ? 16 : 0 }}>
                {card.pros.map((item, pIdx) => {
                  const itemProgress = spring({
                    frame: frame - delay - (pIdx * 5) - 8,
                    fps,
                    config: { damping: 12, stiffness: 120 },
                  });

                  return (
                    <div
                      key={pIdx}
                      style={{
                        display: "flex",
                        alignItems: "center",
                        gap: 12,
                        opacity: Math.max(0, itemProgress),
                        transform: `translateX(${interpolate(
                          Math.max(0, itemProgress),
                          [0, 1],
                          [-12, 0]
                        )}px)`,
                      }}
                    >
                      <span
                        style={{
                          display: "inline-flex",
                          alignItems: "center",
                          justifyContent: "center",
                          width: 22,
                          height: 22,
                          borderRadius: "50%",
                          backgroundColor: isWin ? "rgba(16, 185, 129, 0.2)" : "rgba(255, 255, 255, 0.1)",
                          color: isWin ? "#10b981" : "#94a3b8",
                          fontSize: 14,
                          fontWeight: 900,
                          flexShrink: 0,
                        }}
                      >
                        ✓
                      </span>
                      <span
                        style={{
                          fontSize: isPortrait ? 16 : 17,
                          color: "#e2e8f0",
                          lineHeight: 1.4,
                          fontWeight: 500,
                        }}
                      >
                        {item}
                      </span>
                    </div>
                  );
                })}
              </div>

              {/* Cons List (if present) */}
              {card.cons && card.cons.length > 0 && (
                <div style={{ display: "flex", flexDirection: "column", gap: 10, borderTop: "1px solid rgba(255,255,255,0.08)", paddingTop: 14 }}>
                  {card.cons.map((cItem, cIdx) => (
                    <div key={cIdx} style={{ display: "flex", alignItems: "center", gap: 12 }}>
                      <span
                        style={{
                          display: "inline-flex",
                          alignItems: "center",
                          justifyContent: "center",
                          width: 22,
                          height: 22,
                          borderRadius: "50%",
                          backgroundColor: "rgba(239, 68, 68, 0.15)",
                          color: "#ef4444",
                          fontSize: 13,
                          fontWeight: 900,
                          flexShrink: 0,
                        }}
                      >
                        ✕
                      </span>
                      <span style={{ fontSize: isPortrait ? 15 : 16, color: "#94a3b8", lineHeight: 1.4 }}>
                        {cItem}
                      </span>
                    </div>
                  ))}
                </div>
              )}
            </div>
          );
        })}
      </div>
    </div>
  );
};
