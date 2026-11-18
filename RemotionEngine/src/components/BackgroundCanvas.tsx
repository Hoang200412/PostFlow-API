import React, { useMemo } from "react";
import { useCurrentFrame, interpolate } from "remotion";
import { useStyle } from "../themeContext";
import { isLightPack } from "../themes";

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
  const pack = useStyle();
  const light = isLightPack(pack);
  const base = pack.palette.bg;
  // Light packs need softer color washes, otherwise the blobs look muddy.
  const blobAlpha = light ? "33" : "55";

  // Deterministic particles that drift upward slowly.
  const particles = useMemo(
    () =>
      Array.from({ length: 36 }).map((_, i) => ({
        id: i,
        x: (i * 23.7) % 100,
        y: (i * 37.3) % 100,
        size: (i % 3) + 2,
        speed: 0.25 + (i % 5) * 0.12,
        opacity: 0.18 + (i % 4) * 0.12,
      })),
    []
  );

  // Slowly drifting color blobs (aurora / mesh).
  const driftA = {
    x: 20 + Math.sin(frame / 90) * 10,
    y: 20 + Math.cos(frame / 110) * 8,
  };
  const driftB = {
    x: 80 + Math.cos(frame / 100) * 10,
    y: 72 + Math.sin(frame / 80) * 8,
  };
  const ambientPulse = interpolate(Math.sin(frame / 30), [-1, 1], [0.15, 0.3]);

  const backgrounds: Record<NonNullable<BackgroundCanvasProps["style"]>, string> = {
    aurora: `radial-gradient(circle at ${driftA.x}% ${driftA.y}%, ${primaryColor}${blobAlpha}, transparent 38%), radial-gradient(circle at ${driftB.x}% ${driftB.y}%, ${accentColor}${blobAlpha}, transparent 40%), ${base}`,
    mesh: `radial-gradient(circle at ${driftA.x}% ${driftA.y}%, ${primaryColor}${blobAlpha}, transparent 40%), radial-gradient(circle at ${driftB.x}% ${driftB.y}%, ${accentColor}${blobAlpha}, transparent 44%), ${base}`,
    dotgrid: `radial-gradient(circle at 50% 45%, ${primaryColor}22, transparent 50%), ${base}`,
    paper: `linear-gradient(135deg, ${base} 0%, ${light ? "#ede7d9" : base} 100%)`,
    none: base,
  };

  const dotColor = light ? "rgba(15, 23, 42, 0.10)" : "rgba(255, 255, 255, 0.08)";
  const showParticles = style === "aurora" || style === "dotgrid" || style === "mesh";
  const particleColor = light ? primaryColor : "#ffffff";

  return (
    <div
      style={{
        position: "absolute",
        inset: 0,
        background: backgrounds[style],
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
            backgroundImage: `radial-gradient(${dotColor} 1.5px, transparent 1.5px)`,
            backgroundSize: "32px 32px",
            backgroundPosition: `${(frame * 0.3) % 32}px ${(frame * 0.3) % 32}px`,
            opacity: 0.8,
            pointerEvents: "none",
          }}
        />
      )}

      {/* Floating particles, rising slowly */}
      {showParticles &&
        particles.map((p) => {
          const yOffset = (((p.y - frame * p.speed * 0.06) % 100) + 100) % 100;
          const opacity = interpolate(
            Math.sin(frame * 0.05 + p.id),
            [-1, 1],
            [p.opacity * 0.4, p.opacity]
          );
          return (
            <div
              key={p.id}
              style={{
                position: "absolute",
                left: `${p.x}%`,
                top: `${yOffset}%`,
                width: p.size,
                height: p.size,
                borderRadius: "50%",
                backgroundColor: particleColor,
                opacity,
                boxShadow: `0 0 8px ${particleColor}`,
                pointerEvents: "none",
              }}
            />
          );
        })}

      {/* Vignette for depth */}
      <div
        style={{
          position: "absolute",
          inset: 0,
          background: `radial-gradient(ellipse at center, transparent 55%, ${
            light ? "rgba(15, 23, 42, 0.10)" : "rgba(0, 0, 0, 0.45)"
          } 100%)`,
          pointerEvents: "none",
        }}
      />
    </div>
  );
};
