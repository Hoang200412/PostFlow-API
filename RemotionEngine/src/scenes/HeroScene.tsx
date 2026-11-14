import React from "react";
import { useCurrentFrame, useVideoConfig, spring, interpolate, Img } from "remotion";
import { ExplainerScene } from "../types";
import { TechVisualIcon } from "../components/TechVisualIcon";

export const HeroScene: React.FC<{ scene: ExplainerScene; isPortrait?: boolean }> = ({
  scene,
  isPortrait = false,
}) => {
  const frame = useCurrentFrame();
  const { fps } = useVideoConfig();

  const iconProgress = spring({
    frame,
    fps,
    config: { damping: 12, stiffness: 100 },
  });

  const titleProgress = spring({
    frame: frame - 10,
    fps,
    config: { damping: 14, stiffness: 90 },
  });

  const subtitleProgress = spring({
    frame: frame - 20,
    fps,
    config: { damping: 14, stiffness: 90 },
  });

  const badgeScale = spring({
    frame: frame - 3,
    fps,
    config: { damping: 12, stiffness: 120 },
  });

  const leftTagFloat = interpolate(Math.sin(frame / 20), [-1, 1], [-6, 6]);
  const rightTagFloat = interpolate(Math.cos(frame / 22), [-1, 1], [-6, 6]);

  const isFinance = scene.category === "finance";
  const iconName = scene.visualIcon || scene.icon || (isFinance ? "dollar" : "boxes");

  const primaryCol = isFinance ? "#eab308" : "#3b82f6";
  const accentCol = isFinance ? "#10b981" : "#8b5cf6";

  const defaultTags = isFinance
    ? ["📈 TĂNG TRƯỞNG DÀI HẠN", "🛡️ QUẢN TRỊ RỦI RO"]
    : ["⚡ CÔNG NGHỆ ĐỘT PHÁ", "🛡️ TỐI ƯU HIỆU NĂNG"];

  const tags = scene.tags && scene.tags.length >= 2 ? scene.tags : defaultTags;
  const leftTag = tags[0];
  const rightTag = tags[1];

  const badgeText = isFinance ? "Chiến Lược Cốt Lõi" : "Khái Niệm Cốt Lõi";

  return (
    <div
      style={{
        display: "flex",
        flexDirection: "column",
        alignItems: "center",
        justifyContent: "center",
        height: "100%",
        textAlign: "center",
        padding: isPortrait ? "0 40px" : "0 80px",
        fontFamily: "'Inter', system-ui, sans-serif",
        position: "relative",
      }}
    >
      {/* Category Badge */}
      <div
        style={{
          transform: `scale(${Math.max(0, badgeScale)})`,
          opacity: Math.max(0, badgeScale),
          backgroundColor: `${primaryCol}25`,
          border: `1px solid ${primaryCol}66`,
          padding: "8px 24px",
          borderRadius: 30,
          color: primaryCol,
          fontSize: isPortrait ? 18 : 16,
          fontWeight: 700,
          letterSpacing: "0.12em",
          textTransform: "uppercase",
          marginBottom: 20,
          boxShadow: `0 0 25px ${primaryCol}35`,
        }}
      >
        {badgeText}
      </div>

      {/* Visual Centerpiece: Animated 3D Glowing Tech Icon or Image */}
      {scene.imageUrl ? (
        <div
          style={{
            marginBottom: 24,
            width: isPortrait ? 260 : 320,
            height: isPortrait ? 160 : 190,
            borderRadius: 20,
            overflow: "hidden",
            border: "2px solid rgba(59, 130, 246, 0.5)",
            boxShadow: "0 0 35px rgba(59, 130, 246, 0.4)",
            transform: `scale(${Math.max(0, iconProgress)})`,
          }}
        >
          <Img
            src={scene.imageUrl}
            style={{
              width: "100%",
              height: "100%",
              objectFit: "cover",
              transform: `scale(${interpolate(frame, [0, 150], [1, 1.1])})`,
            }}
          />
        </div>
      ) : (
        <div
          style={{
            marginBottom: 20,
            transform: `scale(${Math.max(0, iconProgress)})`,
          }}
        >
          <TechVisualIcon
            name={iconName}
            size={isPortrait ? 88 : 100}
            primaryColor={primaryCol}
            accentColor={accentCol}
          />
        </div>
      )}

      {/* Floating Decorative Tags in Landscape */}
      {!isPortrait && (
        <>
          <div
            style={{
              position: "absolute",
              left: "10%",
              top: "42%",
              transform: `translateY(${leftTagFloat}px)`,
              backgroundColor: "rgba(15, 23, 42, 0.75)",
              border: `1px solid ${primaryCol}55`,
              padding: "8px 18px",
              borderRadius: 14,
              color: isFinance ? "#fde047" : "#93c5fd",
              fontSize: 13,
              fontWeight: 700,
              letterSpacing: "0.05em",
              boxShadow: `0 8px 24px rgba(0, 0, 0, 0.5), 0 0 15px ${primaryCol}22`,
            }}
          >
            {leftTag}
          </div>

          <div
            style={{
              position: "absolute",
              right: "10%",
              top: "42%",
              transform: `translateY(${rightTagFloat}px)`,
              backgroundColor: "rgba(15, 23, 42, 0.75)",
              border: `1px solid ${accentCol}55`,
              padding: "8px 18px",
              borderRadius: 14,
              color: isFinance ? "#6ee7b7" : "#c4b5fd",
              fontSize: 13,
              fontWeight: 700,
              letterSpacing: "0.05em",
              boxShadow: `0 8px 24px rgba(0, 0, 0, 0.5), 0 0 15px ${accentCol}22`,
            }}
          >
            {rightTag}
          </div>
        </>
      )}

      {/* Main Big Title */}
      <h1
        style={{
          fontSize: isPortrait ? 52 : 62,
          fontWeight: 900,
          lineHeight: 1.18,
          color: "#ffffff",
          maxWidth: isPortrait ? 820 : 1200,
          margin: 0,
          opacity: Math.max(0, titleProgress),
          transform: `translateY(${interpolate(Math.max(0, titleProgress), [0, 1], [30, 0])}px)`,
          textShadow: "0 10px 30px rgba(0, 0, 0, 0.7)",
          background: "linear-gradient(135deg, #ffffff 65%, #e2e8f0 100%)",
          WebkitBackgroundClip: "text",
          WebkitTextFillColor: "transparent",
          filter: "drop-shadow(0 4px 20px rgba(0, 0, 0, 0.8))",
        }}
      >
        {scene.title}
      </h1>

      {/* Subtitle / Description */}
      {scene.subtitle && (
        <p
          style={{
            fontSize: isPortrait ? 24 : 22,
            fontWeight: 400,
            lineHeight: 1.5,
            color: "#94a3b8",
            maxWidth: isPortrait ? 740 : 920,
            marginTop: 18,
            opacity: Math.max(0, subtitleProgress),
            transform: `translateY(${interpolate(
              Math.max(0, subtitleProgress),
              [0, 1],
              [25, 0]
            )}px)`,
          }}
        >
          {scene.subtitle}
        </p>
      )}
    </div>
  );
};
