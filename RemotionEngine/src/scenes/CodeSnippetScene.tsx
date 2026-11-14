import React from "react";
import { useCurrentFrame, useVideoConfig, spring, interpolate } from "remotion";
import { ExplainerScene } from "../types";
import { TechVisualIcon } from "../components/TechVisualIcon";

export const CodeSnippetScene: React.FC<{ scene: ExplainerScene; isPortrait?: boolean }> = ({
  scene,
  isPortrait = false,
}) => {
  const frame = useCurrentFrame();
  const { fps } = useVideoConfig();

  const codeData = scene.codeSnippet || {
    language: "bash",
    code: `# Cài đặt và cấu hình container\ndocker run -d -p 8080:80 \\\n  --name postflow-api \\\n  --restart always \\\n  postflow/core:latest`,
  };

  const windowProgress = spring({
    frame,
    fps,
    config: { damping: 14, stiffness: 90 },
  });

  const lines = codeData.code.split("\n");

  // Cursor blink
  const showCursor = Math.floor(frame / 15) % 2 === 0;

  return (
    <div
      style={{
        display: "flex",
        flexDirection: "column",
        alignItems: "center",
        justifyContent: "center",
        height: "100%",
        padding: isPortrait ? "0 24px" : "0 70px",
        fontFamily: "'Inter', system-ui, sans-serif",
      }}
    >
      {/* Title */}
      <h2
        style={{
          fontSize: isPortrait ? 36 : 42,
          fontWeight: 800,
          color: "#ffffff",
          marginBottom: 28,
          textAlign: "center",
          opacity: windowProgress,
          transform: `translateY(${interpolate(windowProgress, [0, 1], [-16, 0])}px)`,
        }}
      >
        {scene.title}
      </h2>

      {/* Code Editor Window */}
      <div
        style={{
          width: "100%",
          maxWidth: isPortrait ? 600 : 960,
          backgroundColor: "#0d1117",
          border: "1px solid rgba(59, 130, 246, 0.3)",
          borderRadius: 20,
          overflow: "hidden",
          boxShadow: "0 25px 60px rgba(0, 0, 0, 0.7), 0 0 35px rgba(59, 130, 246, 0.2)",
          opacity: Math.max(0, windowProgress),
          transform: `scale(${Math.max(0, windowProgress)})`,
        }}
      >
        {/* Window Title Bar */}
        <div
          style={{
            display: "flex",
            alignItems: "center",
            justifyContent: "space-between",
            padding: "14px 20px",
            backgroundColor: "#161b22",
            borderBottom: "1px solid rgba(255, 255, 255, 0.08)",
          }}
        >
          <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
            <div style={{ width: 12, height: 12, borderRadius: "50%", backgroundColor: "#ff5f56" }} />
            <div style={{ width: 12, height: 12, borderRadius: "50%", backgroundColor: "#ffbd2e" }} />
            <div style={{ width: 12, height: 12, borderRadius: "50%", backgroundColor: "#27c93f" }} />
            <span
              style={{
                marginLeft: 14,
                fontSize: 13,
                fontFamily: "monospace",
                color: "#8b949e",
                fontWeight: 600,
              }}
            >
              {codeData.language.toUpperCase()}
            </span>
          </div>

          {/* Small Tech Icon */}
          <div style={{ display: "flex", alignItems: "center", gap: 6 }}>
            <TechVisualIcon
              name="terminal"
              size={20}
              primaryColor="#38bdf8"
              showOrbitalRings={false}
            />
            <span style={{ fontSize: 12, color: "#64748b", fontFamily: "monospace" }}>bash</span>
          </div>
        </div>

        {/* Code Content Area */}
        <div
          style={{
            padding: isPortrait ? "20px 22px" : "28px 34px",
            fontFamily: "'Fira Code', 'JetBrains Mono', Consolas, monospace",
            fontSize: isPortrait ? 17 : 19,
            lineHeight: 1.7,
            color: "#e6edf3",
          }}
        >
          {lines.map((line, idx) => {
            const lineProgress = spring({
              frame: frame - (idx * 5) - 8,
              fps,
              config: { damping: 12, stiffness: 120 },
            });

            const isComment = line.trim().startsWith("#") || line.trim().startsWith("//");
            const isCommand = line.trim().startsWith("docker") || line.trim().startsWith("npm") || line.trim().startsWith("git");

            return (
              <div
                key={idx}
                style={{
                  display: "flex",
                  gap: 18,
                  opacity: Math.max(0, lineProgress),
                  transform: `translateX(${interpolate(
                    Math.max(0, lineProgress),
                    [0, 1],
                    [-10, 0]
                  )}px)`,
                }}
              >
                <span style={{ color: "#484f58", userSelect: "none", width: 22, textAlign: "right" }}>
                  {idx + 1}
                </span>
                <span
                  style={{
                    color: isComment
                      ? "#8b949e"
                      : isCommand
                      ? "#38bdf8"
                      : line.includes("--") || line.includes("-d") || line.includes("-p")
                      ? "#a78bfa"
                      : "#e6edf3",
                    fontWeight: isCommand ? 600 : 400,
                  }}
                >
                  {line}
                  {idx === lines.length - 1 && showCursor && (
                    <span style={{ color: "#38bdf8", marginLeft: 4 }}>▌</span>
                  )}
                </span>
              </div>
            );
          })}
        </div>
      </div>
    </div>
  );
};
