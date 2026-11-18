import React, { useMemo } from "react";
import { useCurrentFrame, interpolate } from "remotion";

interface BackgroundCanvasProps {
  primaryColor?: string;
  accentColor?: string;
  style?: "aurora" | "dotgrid" | "paper" | "mesh" | "none";
}

export const BackgroundCanvas: React.FC<BackgroundCanvasProps> = ({
  primaryColor = "#3b82f6",
  accentColor = "#8b5cf6",
  style = "aurora",
}) => {
  const frame = useCurrentFrame();

  // Pre-generate static random star positions
  const stars = useMemo(
    () => Array.from({ length: 45 }).map((_, i) => ({
      id: i,
      x: (i * 23.7) % 100,
      y: (i * 37.3) % 100,
      size: (i % 3) + 1.2,
      speed: 0.2 + (i % 5) * 0.15,
      opacity: 0.2 + (i % 4) * 0.2,
    })),
    []
  );

  const ambientPulse = interpolate(
    Math.sin(frame / 30),
    [-1, 1],
    [0.15, 0.28]
  );

  const backgrounds: Record<NonNullable<BackgroundCanvasProps["style"]>, string> = {
    aurora: `radial-gradient(circle at 20% 18%, ${primaryColor}55, transparent 35%), radial-gradient(circle at 78% 82%, ${accentColor}55, transparent 38%), #07090e`,
    dotgrid: `radial-gradient(circle at 50% 45%, ${primaryColor}22, transparent 48%), #07090e`,
    paper: "linear-gradient(135deg, #f7f3e8 0%, #ede7d9 100%)",
    mesh: `radial-gradient(circle at 20% 20%, ${primaryColor}55, transparent 38%), radial-gradient(circle at 80% 70%, ${accentColor}55, transparent 42%), #090c14`,
    none: "#07090e",
  };
  const background = backgrounds[style];

  return (
    <div
      style={{
        position: "absolute",
        inset: 0,
        background,
        overflow: "hidden",
        zIndex: 0,
      }}
    >
      {(style === "aurora" || style === "mesh") && (
        <div
          style={{
            position: "absolute",
            inset: "-10%",
            background: `radial-gradient(ellipse at 50% 50%, ${primaryColor}35 0%, transparent 42%)`,
            opacity: ambientPulse,
            pointerEvents: "none",
          }}
        />
      )}
      {style === "dotgrid" && (
        <div
        style={{
          position: "absolute",
          inset: 0,
          backgroundImage: `radial-gradient(rgba(255, 255, 255, 0.08) 1px, transparent 1px)`,
          backgroundSize: "32px 32px",
          opacity: 0.45,
          pointerEvents: "none",
        }}
        />
      )}

      {/* Floating Starfield Particles */}
      {(style === "aurora" || style === "dotgrid") && stars.map((star) => {
        const yOffset = (star.y + (frame * star.speed * 0.05)) % 100;
        const opacity = interpolate(
          Math.sin((frame * 0.05) + star.id),
          [-1, 1],
          [star.opacity * 0.5, star.opacity]
        );

        return (
          <div
            key={star.id}
            style={{
              position: "absolute",
              left: `${star.x}%`,
              top: `${yOffset}%`,
              width: `${star.size}px`,
              height: `${star.size}px`,
              borderRadius: "50%",
              backgroundColor: "#ffffff",
              opacity,
              boxShadow: `0 0 6px rgba(255, 255, 255, 0.8)`,
              pointerEvents: "none",
            }}
          />
        );
      })}
    </div>
  );
};
