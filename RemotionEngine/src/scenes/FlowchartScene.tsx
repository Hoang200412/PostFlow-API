import React from "react";
import { useCurrentFrame, useVideoConfig, spring, interpolate } from "remotion";
import { ExplainerScene, FlowchartNode } from "../types";
import { TechVisualIcon } from "../components/TechVisualIcon";
import { useStyle } from "../themeContext";
import { paletteTone } from "../themes";

function getFlowchartIcon(node: FlowchartNode, index: number): string {
  if (node.icon) return node.icon;
  const text = (node.label + " " + (node.sublabel || "")).toLowerCase();

  // Tech defaults
  if (text.includes("client") || text.includes("user") || text.includes("browser") || text.includes("người dùng") || text.includes("app")) return "laptop";
  if (text.includes("gateway") || text.includes("auth") || text.includes("shield") || text.includes("security") || text.includes("xác thực")) return "shield";
  if (text.includes("database") || text.includes("db") || text.includes("sql") || text.includes("storage") || text.includes("lưu trữ") || text.includes("redis")) return "database";
  if (text.includes("service") || text.includes("worker") || text.includes("microservice") || text.includes("xử lý") || text.includes("logic")) return "cpu";
  if (text.includes("cloud") || text.includes("k8s") || text.includes("docker") || text.includes("container") || text.includes("deploy")) return "boxes";
  if (text.includes("network") || text.includes("queue") || text.includes("kafka") || text.includes("rabbit")) return "network";
  const techDefaults = ["laptop", "shield", "cpu", "database", "boxes"];
  return techDefaults[index % techDefaults.length];
}

export const FlowchartScene: React.FC<{ scene: ExplainerScene; isPortrait?: boolean }> = ({
  scene,
  isPortrait = false,
}) => {
  const frame = useCurrentFrame();
  const { fps } = useVideoConfig();
  const theme = useStyle();
  const tone = paletteTone(theme);

  const defaultTechNodes: FlowchartNode[] = [
    { id: "1", label: "Client Request", sublabel: "Gửi HTTP Request", icon: "laptop", status: "normal" as const },
    { id: "2", label: "API Gateway", sublabel: "Xác thực & Điều phối", icon: "shield", status: "active" as const },
    { id: "3", label: "Microservice", sublabel: "Xử lý logic nghiệp vụ", icon: "cpu", status: "highlight" as const },
    { id: "4", label: "Database", sublabel: "Lưu trữ an toàn", icon: "database", status: "normal" as const },
  ];

  const nodes = scene.flowchartNodes && scene.flowchartNodes.length > 0
    ? scene.flowchartNodes
    : defaultTechNodes;

  const headerProgress = spring({
    frame,
    fps,
    config: { damping: 14, stiffness: 90 },
  });

  const packetColor = theme.palette.primary;

  return (
    <div
      style={{
        display: "flex",
        flexDirection: "column",
        alignItems: "center",
        justifyContent: "center",
        height: "100%",
        padding: isPortrait ? "0 30px" : "0 60px",
        fontFamily: theme.fonts.body,
      }}
    >
      {/* Title & Subtitle */}
      <div
        style={{
          textAlign: "center",
          marginBottom: isPortrait ? 30 : 50,
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

      {/* Nodes & Dynamic Flow Pipeline */}
      <div
        style={{
          display: "flex",
          flexDirection: isPortrait ? "column" : "row",
          alignItems: "center",
          justifyContent: "center",
          gap: isPortrait ? 12 : 20,
          width: "100%",
          maxWidth: isPortrait ? 900 : 1380,
        }}
      >
        {nodes.map((node, index) => {
          const delay = index * 10;
          const nodeProgress = spring({
            frame: frame - delay,
            fps,
            config: { damping: 12, stiffness: 100 },
          });

          const isActive = node.status === "active";
          const isHighlight = node.status === "highlight";

          const borderCol = isHighlight
            ? theme.palette.accent
            : isActive
            ? theme.palette.primary
            : tone.line;

          const glowCol = isHighlight
            ? `${theme.palette.accent}66`
            : isActive
            ? `${theme.palette.primary}55`
            : tone.shadow;

          const iconName = getFlowchartIcon(node, index);
          const iconColor = isHighlight ? theme.palette.accent : isActive ? theme.palette.primary : theme.palette.muted;

          // Animated pulse for highlight/active
          const pulse = (isActive || isHighlight)
            ? interpolate(Math.sin((frame + index * 10) / 8), [-1, 1], [0.98, 1.02])
            : 1;

          // Packet progress along connection beam
          const packetProgress = ((frame * 2.5 + index * 35) % 100) / 100;

          return (
            <React.Fragment key={node.id || index}>
              {/* Node Card */}
              <div
                style={{
                  flex: isPortrait ? "none" : 1,
                  width: isPortrait ? "100%" : "auto",
                  opacity: Math.max(0, nodeProgress),
                  transform: `scale(${Math.max(0, nodeProgress) * pulse})`,
                  backgroundColor: theme.palette.surface,
                  border: `2px solid ${borderCol}`,
                  borderRadius: 20,
                  padding: isPortrait ? "16px 20px" : "24px 20px",
                  textAlign: "center",
                  boxShadow: `0 14px 34px ${glowCol}`,
                  position: "relative",
                  display: "flex",
                  flexDirection: isPortrait ? "row" : "column",
                  alignItems: "center",
                  gap: isPortrait ? 16 : 14,
                }}
              >
                {/* Step Index Badge */}
                <div
                  style={{
                    position: "absolute",
                    top: -12,
                    left: 20,
                    backgroundColor: isHighlight ? theme.palette.accent : isActive ? theme.palette.primary : theme.palette.muted,
                    color: "#ffffff",
                    fontSize: 11,
                    fontWeight: 800,
                    padding: "3px 10px",
                    borderRadius: 12,
                    letterSpacing: "0.06em",
                    boxShadow: `0 4px 12px ${tone.shadow}`,
                  }}
                >
                  BƯỚC {index + 1}
                </div>

                {/* Animated Tech / Finance Visual Icon */}
                <div
                  style={{
                    display: "flex",
                    alignItems: "center",
                    justifyContent: "center",
                    width: isPortrait ? 52 : 68,
                    height: isPortrait ? 52 : 68,
                    borderRadius: 16,
                    backgroundColor: tone.wash,
                    border: `1px solid ${borderCol}`,
                    flexShrink: 0,
                  }}
                >
                  <TechVisualIcon
                    name={iconName}
                    size={isPortrait ? 32 : 40}
                    primaryColor={iconColor}
                    delay={delay}
                    showOrbitalRings={false}
                  />
                </div>

                {/* Text Labels */}
                <div style={{ textAlign: isPortrait ? "left" : "center", flex: 1 }}>
                  <div
                    style={{
                      fontSize: isPortrait ? 22 : 24,
                      fontWeight: 700,
                      color: theme.palette.text,
                      marginBottom: 4,
                    }}
                  >
                    {node.label}
                  </div>

                  {node.sublabel && (
                    <div
                      style={{
                        fontSize: isPortrait ? 17 : 18,
                        color: theme.palette.muted,
                        lineHeight: 1.4,
                      }}
                    >
                      {node.sublabel}
                    </div>
                  )}
                </div>
              </div>

              {/* Animated Connection Beam */}
              {index < nodes.length - 1 && (
                isPortrait ? (
                  /* Vertical Connection Beam */
                  <div
                    style={{
                      display: "flex",
                      alignItems: "center",
                      justifyContent: "center",
                      height: 24,
                      position: "relative",
                      opacity: Math.max(0, nodeProgress),
                    }}
                  >
                    <div
                      style={{
                        width: 3,
                        height: 24,
                        backgroundColor: `${theme.palette.primary}55`,
                        borderRadius: 2,
                        position: "relative",
                        overflow: "hidden",
                      }}
                    >
                      <div
                        style={{
                          position: "absolute",
                          top: `${packetProgress * 100}%`,
                          left: -2,
                          width: 7,
                          height: 7,
                          borderRadius: "50%",
                          backgroundColor: packetColor,
                          boxShadow: `0 0 8px ${packetColor}`,
                        }}
                      />
                    </div>
                  </div>
                ) : (
                  /* Horizontal Connection Beam */
                  <div
                    style={{
                      display: "flex",
                      alignItems: "center",
                      justifyContent: "center",
                      width: 44,
                      position: "relative",
                      opacity: Math.max(0, nodeProgress),
                    }}
                  >
                    <div
                      style={{
                        width: "100%",
                        height: 3,
                        backgroundColor: `${theme.palette.primary}55`,
                        borderRadius: 2,
                        position: "relative",
                      }}
                    >
                      {/* Animated traveling data packet */}
                      <div
                        style={{
                          position: "absolute",
                          left: `${packetProgress * 100}%`,
                          top: -3,
                          width: 8,
                          height: 8,
                          borderRadius: "50%",
                          backgroundColor: packetColor,
                          boxShadow: `0 0 10px ${packetColor}, 0 0 18px ${packetColor}`,
                        }}
                      />
                    </div>
                    {/* Small arrow head indicator */}
                    <div
                      style={{
                        position: "absolute",
                        right: -3,
                        width: 0,
                        height: 0,
                        borderTop: "5px solid transparent",
                        borderBottom: "5px solid transparent",
                        borderLeft: `7px solid ${packetColor}`,
                      }}
                    />
                  </div>
                )
              )}
            </React.Fragment>
          );
        })}
      </div>
    </div>
  );
};
