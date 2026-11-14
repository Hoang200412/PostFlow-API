import React from "react";
import { useCurrentFrame, useVideoConfig, spring, interpolate } from "remotion";
import { ExplainerScene, FlowchartNode } from "../types";
import { TechVisualIcon } from "../components/TechVisualIcon";

function getFlowchartIcon(node: FlowchartNode, index: number, isFinance: boolean): string {
  if (node.icon) return node.icon;
  const text = (node.label + " " + (node.sublabel || "")).toLowerCase();

  if (isFinance) {
    if (text.includes("thu nhập") || text.includes("income") || text.includes("lương") || text.includes("tiền vào")) return "wallet";
    if (text.includes("chi phí") || text.includes("tiêu dùng") || text.includes("cố định") || text.includes("chi tiêu")) return "creditcard";
    if (text.includes("đầu tư") || text.includes("tăng trưởng") || text.includes("lợi nhuận") || text.includes("lãi")) return "trendingup";
    if (text.includes("tự do") || text.includes("tài sản") || text.includes("vốn") || text.includes("tiền tệ")) return "coins";
    if (text.includes("tiết kiệm") || text.includes("dự phòng") || text.includes("quỹ")) return "piggybank";
    if (text.includes("ngân hàng") || text.includes("thể chế")) return "landmark";
    const financeDefaults = ["wallet", "creditcard", "trendingup", "coins"];
    return financeDefaults[index % financeDefaults.length];
  }

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

  const isFinance = scene.category === "finance";

  const defaultFinanceNodes: FlowchartNode[] = [
    { id: "1", label: "Thu Nhập", sublabel: "Dòng tiền vào hàng tháng", icon: "wallet", status: "normal" as const },
    { id: "2", label: "Chi Phí Thiết Yếu", sublabel: "Nguyên tắc quản lý 50%", icon: "creditcard", status: "normal" as const },
    { id: "3", label: "Quỹ Đầu Tư Kỷ Luật", sublabel: "Tích lũy & Tận dụng lãi kép", icon: "trendingup", status: "highlight" as const },
    { id: "4", label: "Tự Do Tài Chính", sublabel: "Thu nhập thụ động bền vững", icon: "coins", status: "active" as const },
  ];

  const defaultTechNodes: FlowchartNode[] = [
    { id: "1", label: "Client Request", sublabel: "Gửi HTTP Request", icon: "laptop", status: "normal" as const },
    { id: "2", label: "API Gateway", sublabel: "Xác thực & Điều phối", icon: "shield", status: "active" as const },
    { id: "3", label: "Microservice", sublabel: "Xử lý logic nghiệp vụ", icon: "cpu", status: "highlight" as const },
    { id: "4", label: "Database", sublabel: "Lưu trữ an toàn", icon: "database", status: "normal" as const },
  ];

  const nodes = scene.flowchartNodes && scene.flowchartNodes.length > 0
    ? scene.flowchartNodes
    : isFinance
    ? defaultFinanceNodes
    : defaultTechNodes;

  const headerProgress = spring({
    frame,
    fps,
    config: { damping: 14, stiffness: 90 },
  });

  const packetColor = isFinance ? "#fbbf24" : "#38bdf8";

  return (
    <div
      style={{
        display: "flex",
        flexDirection: "column",
        alignItems: "center",
        justifyContent: "center",
        height: "100%",
        padding: isPortrait ? "0 30px" : "0 60px",
        fontFamily: "'Inter', system-ui, sans-serif",
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
            color: "#ffffff",
            margin: "0 0 10px 0",
            letterSpacing: "-0.02em",
          }}
        >
          {scene.title}
        </h2>
        {scene.subtitle && (
          <p style={{ fontSize: isPortrait ? 20 : 20, color: "#94a3b8", margin: 0, fontWeight: 500 }}>
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
          maxWidth: isPortrait ? 520 : 1380,
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
            ? (isFinance ? "#10b981" : "#10b981")
            : isActive
            ? (isFinance ? "#eab308" : "#3b82f6")
            : "rgba(255, 255, 255, 0.15)";

          const glowCol = isHighlight
            ? (isFinance ? "rgba(16, 185, 129, 0.4)" : "rgba(16, 185, 129, 0.35)")
            : isActive
            ? (isFinance ? "rgba(234, 179, 8, 0.4)" : "rgba(59, 130, 246, 0.35)")
            : "rgba(0, 0, 0, 0.4)";

          const iconName = getFlowchartIcon(node, index, isFinance);
          const iconColor = isHighlight ? "#10b981" : isActive ? (isFinance ? "#eab308" : "#3b82f6") : (isFinance ? "#fbbf24" : "#a855f7");

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
                  backgroundColor: isFinance ? "rgba(10, 24, 18, 0.85)" : "rgba(13, 19, 35, 0.85)",
                  border: `2px solid ${borderCol}`,
                  borderRadius: 20,
                  padding: isPortrait ? "16px 20px" : "24px 20px",
                  textAlign: "center",
                  boxShadow: `0 14px 34px ${glowCol}`,
                  backdropFilter: "blur(16px)",
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
                    backgroundColor: isHighlight ? "#10b981" : isActive ? (isFinance ? "#eab308" : "#3b82f6") : "#475569",
                    color: "#ffffff",
                    fontSize: 11,
                    fontWeight: 800,
                    padding: "3px 10px",
                    borderRadius: 12,
                    letterSpacing: "0.06em",
                    boxShadow: "0 4px 12px rgba(0,0,0,0.5)",
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
                    backgroundColor: "rgba(255, 255, 255, 0.05)",
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
                      fontSize: isPortrait ? 20 : 21,
                      fontWeight: 700,
                      color: "#ffffff",
                      marginBottom: 4,
                    }}
                  >
                    {node.label}
                  </div>

                  {node.sublabel && (
                    <div
                      style={{
                        fontSize: isPortrait ? 14 : 14,
                        color: "#94a3b8",
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
                        backgroundColor: isFinance ? "rgba(234, 179, 8, 0.3)" : "rgba(59, 130, 246, 0.3)",
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
                        backgroundColor: isFinance ? "rgba(234, 179, 8, 0.3)" : "rgba(59, 130, 246, 0.3)",
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
