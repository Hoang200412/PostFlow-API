import React, { useMemo } from "react";
import { useCurrentFrame, interpolate } from "remotion";

interface BackgroundCanvasProps {
  primaryColor?: string;
  accentColor?: string;
  style?: "starfield" | "dotgrid" | "ambient";
}

export const BackgroundCanvas: React.FC<BackgroundCanvasProps> = ({
  primaryColor = "#3b82f6",
  accentColor = "#8b5cf6",
}) => {
  const frame = useCurrentFrame();

  // Pre-generate static random star positions
  const stars = useMemo(() => {
    return Array.from({ length: 45 }).map((_, i) => ({
      id: i,
      x: (i * 23.7) % 100,
      y: (i * 37.3) % 100,
      size: (i % 3) + 1.2,
      speed: 0.2 + (i % 5) * 0.15,
      opacity: 0.2 + (i % 4) * 0.2,
    }));
  }, []);

  const ambientPulse = interpolate(
    Math.sin(frame / 30),
    [-1, 1],
    [0.15, 0.28]
  );

  return (
    <div
      style={{
        position: "absolute",
        inset: 0,
        backgroundColor: "#07090e",
        overflow: "hidden",
        zIndex: 0,
      }}
    >
      {/* Ambient background glowing orbs */}
      <div
        style={{
          position: "absolute",
          top: "-15%",
          left: "20%",
          width: "55vw",
          height: "55vw",
          borderRadius: "50%",
          background: `radial-gradient(circle, ${primaryColor} 0%, transparent 65%)`,
          opacity: ambientPulse,
          filter: "blur(90px)",
          pointerEvents: "none",
        }}
      />
      <div
        style={{
          position: "absolute",
          bottom: "-20%",
          right: "15%",
          width: "50vw",
          height: "50vw",
          borderRadius: "50%",
          background: `radial-gradient(circle, ${accentColor} 0%, transparent 70%)`,
          opacity: ambientPulse * 0.8,
          filter: "blur(110px)",
          pointerEvents: "none",
        }}
      />

      {/* Subtle Dot Grid Matrix */}
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

      {/* Floating Starfield Particles */}
      {stars.map((star) => {
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

