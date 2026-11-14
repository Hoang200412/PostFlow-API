import React from "react";
import { useCurrentFrame, useVideoConfig, spring, interpolate } from "remotion";
import { ExplainerScene } from "../types";
import { TechVisualIcon } from "../components/TechVisualIcon";

export const MetricHighlightScene: React.FC<{ scene: ExplainerScene; isPortrait?: boolean }> = ({
  scene,
  isPortrait = false,
}) => {
  const frame = useCurrentFrame();
  const { fps } = useVideoConfig();

  const isFinance = scene.category === "finance";
  const defaultMetric = isFinance
    ? {
        value: "72",
        prefix: "Quy tắc ",
        suffix: "",
        label: "Thời Gian Nhân Đôi Tài Sản",
        description: "Lấy 72 chia cho tỷ suất sinh lời hàng năm để tính chính xác số năm số vốn của bạn tăng gấp 2 lần.",
      }
    : {
        value: "10x",
        prefix: "",
        suffix: "",
        label: "Tốc Độ Xử Lý Vượt Trội",
        description: "Tối ưu hóa độ trễ, giảm 80% tài nguyên CPU và RAM trong môi trường tải cao.",
      };

  const metric = scene.metric || defaultMetric;

  const animProgress = spring({
    frame,
    fps,
    config: { damping: 14, stiffness: 90 },
  });

  const counterScale = spring({
    frame: frame - 8,
    fps,
    config: { damping: 11, stiffness: 100 },
  });

  // Animated circular gauge fill
  const circleCircumference = 2 * Math.PI * 90;
  const strokeDashoffset = interpolate(
    Math.min(frame / 35, 1),
    [0, 1],
    [circleCircumference, circleCircumference * 0.25]
  );

  const iconName = scene.visualIcon || (isFinance ? "coins" : "zap");
  const gaugeColor = isFinance ? "#fbbf24" : "#38bdf8";
  const primaryCol = isFinance ? "#fbbf24" : "#38bdf8";
  const accentCol = isFinance ? "#10b981" : "#10b981";

  return (
    <div
      style={{
        display: "flex",
        flexDirection: "column",
        alignItems: "center",
        justifyContent: "center",
        height: "100%",
        padding: isPortrait ? "0 30px" : "0 80px",
        textAlign: "center",
        fontFamily: "'Inter', system-ui, sans-serif",
      }}
    >
      {/* Scene Title */}
      <h2
        style={{
          fontSize: isPortrait ? 36 : 42,
          fontWeight: 800,
          color: "#ffffff",
          margin: "0 0 32px 0",
          opacity: animProgress,
          transform: `translateY(${interpolate(animProgress, [0, 1], [-20, 0])}px)`,
        }}
      >
        {scene.title}
      </h2>

      {/* Main Glassmorphic Metric Card */}
      <div
        style={{
          display: "flex",
          flexDirection: isPortrait ? "column" : "row",
          alignItems: "center",
          gap: isPortrait ? 24 : 48,
          backgroundColor: isFinance ? "rgba(10, 24, 18, 0.85)" : "rgba(15, 23, 42, 0.8)",
          border: `2px solid ${primaryCol}55`,
          borderRadius: 32,
          padding: isPortrait ? "36px 28px" : "48px 64px",
          boxShadow: `0 20px 60px rgba(0, 0, 0, 0.6), 0 0 50px ${primaryCol}33`,
          backdropFilter: "blur(20px)",
          maxWidth: isPortrait ? 540 : 1000,
          opacity: Math.max(0, counterScale),
          transform: `scale(${Math.max(0, counterScale)})`,
          position: "relative",
        }}
      >
        {/* Visual Gauge & Center Icon */}
        <div
          style={{
            position: "relative",
            width: isPortrait ? 180 : 220,
            height: isPortrait ? 180 : 220,
            display: "flex",
            alignItems: "center",
            justifyContent: "center",
            flexShrink: 0,
          }}
        >
          {/* SVG Animated Circular Gauge */}
          <svg
            width={isPortrait ? 180 : 220}
            height={isPortrait ? 180 : 220}
            viewBox="0 0 200 200"
            style={{ position: "absolute", top: 0, left: 0, transform: "rotate(-90deg)" }}
          >
            {/* Background Track */}
            <circle
              cx="100"
              cy="100"
              r="90"
              fill="transparent"
              stroke="rgba(255, 255, 255, 0.08)"
              strokeWidth="10"
            />
            {/* Animated Gradient Gauge Progress */}
            <circle
              cx="100"
              cy="100"
              r="90"
              fill="transparent"
              stroke={gaugeColor}
              strokeWidth="10"
              strokeDasharray={circleCircumference}
              strokeDashoffset={strokeDashoffset}
              strokeLinecap="round"
              style={{
                filter: `drop-shadow(0 0 10px ${gaugeColor})`,
              }}
            />
          </svg>

          {/* Center Glowing Tech / Finance Icon */}
          <TechVisualIcon
            name={iconName}
            size={isPortrait ? 60 : 76}
            primaryColor={primaryCol}
            accentColor={accentCol}
            showOrbitalRings={true}
          />
        </div>

        {/* Metric Values & Explanations */}
        <div style={{ textAlign: isPortrait ? "center" : "left", flex: 1 }}>
          {/* Huge Stat Highlight */}
          <div
            style={{
              fontSize: isPortrait ? 84 : 104,
              fontWeight: 900,
              letterSpacing: "-0.04em",
              lineHeight: 1,
              background: `linear-gradient(135deg, ${primaryCol} 0%, ${accentCol} 100%)`,
              WebkitBackgroundClip: "text",
              WebkitTextFillColor: "transparent",
              filter: `drop-shadow(0 10px 24px ${primaryCol}55)`,
              marginBottom: 14,
            }}
          >
            {metric.prefix || ""}
            {metric.value}
            {metric.suffix || ""}
          </div>

          {/* Metric Label */}
          <div
            style={{
              fontSize: isPortrait ? 26 : 28,
              fontWeight: 800,
              color: "#ffffff",
              marginBottom: 12,
              letterSpacing: "-0.01em",
            }}
          >
            {metric.label}
          </div>

          {/* Metric Description */}
          {metric.description && (
            <p
              style={{
                fontSize: isPortrait ? 17 : 18,
                color: "#94a3b8",
                lineHeight: 1.5,
                margin: 0,
                fontWeight: 400,
              }}
            >
              {metric.description}
            </p>
          )}
        </div>
      </div>
    </div>
  );
};
