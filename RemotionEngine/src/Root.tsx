import React from "react";
import { Composition } from "remotion";
import { ExplainerVideo } from "./Video";
import { ExplainerVideoProps } from "./types";
import type { ComponentType } from "react";

const defaultProps: ExplainerVideoProps = {
  topic: "Cách Hoạt Động Của Docker Container",
  aspectRatio: "16:9",
  subtitles: [
    { word: "Chào", start_ms: 100, end_ms: 350 },
    { word: "mừng", start_ms: 350, end_ms: 600 },
    { word: "bạn", start_ms: 600, end_ms: 850 },
    { word: "đến", start_ms: 850, end_ms: 1100 },
    { word: "với", start_ms: 1100, end_ms: 1350 },
    { word: "Docker.", start_ms: 1350, end_ms: 1800 },
  ],
  chapters: [
    { id: "c1", title: "Khái Niệm", startFrame: 0, durationInFrames: 120 },
    { id: "c2", title: "Cơ Chế Hoạt Động", startFrame: 120, durationInFrames: 150 },
    { id: "c3", title: "So Sánh", startFrame: 270, durationInFrames: 150 },
  ],
  scenes: [
    {
      id: "s1",
      type: "hero",
      title: "Docker Container Là Gì?",
      subtitle: "Giải pháp đóng gói và vận hành ứng dụng cô lập, siêu nhẹ trên mọi hệ điều hành.",
      durationInFrames: 120,
    },
    {
      id: "s2",
      type: "flowchart",
      title: "Quy Trình Khởi Tạo Container",
      subtitle: "Từ Dockerfile đến ứng dụng chạy thực tế",
      durationInFrames: 150,
      flowchartNodes: [
        { id: "1", label: "Dockerfile", sublabel: "Kịch bản build" },
        { id: "2", label: "Docker Image", sublabel: "Bản snapshot bất biến", status: "active" },
        { id: "3", label: "Docker Engine", sublabel: "Bộ máy điều phối", status: "highlight" },
        { id: "4", label: "Container", sublabel: "Ứng dụng hoạt động" },
      ],
    },
    {
      id: "s3",
      type: "card-comparison",
      title: "Docker Container vs Máy Ảo (VM)",
      subtitle: "Tại sao Container vượt trội hơn hẳn?",
      durationInFrames: 150,
      comparisonCards: [
        {
          name: "Máy Ảo (Virtual Machine)",
          badge: "Nặng nề",
          pros: ["Cần cài đặt full Guest OS", "Chiếm vài GB đến vài chục GB", "Khởi động mất vài phút"],
          isWinner: false,
        },
        {
          name: "Docker Container",
          badge: "Đột phá",
          pros: ["Dùng chung Kernel hệ điều hành", "Dung lượng siêu nhẹ (vài MB)", "Khởi động trong vài mili-giây"],
          isWinner: true,
        },
      ],
    },
  ],
};

export const RemotionRoot: React.FC = () => {
  const compositionComponent = ExplainerVideo as unknown as ComponentType<Record<string, unknown>>;
  const calculateTotalFrames = (props: ExplainerVideoProps) => {
    const explicitDuration = props.durationInFrames;
    if (typeof explicitDuration === "number" && explicitDuration > 0) {
      return explicitDuration;
    }

    if (!props.scenes || props.scenes.length === 0) return 300;
    const sceneFrames = props.scenes.reduce((acc, s) => {
      const dur = s.durationInFrames ?? 150;
      return acc + dur;
    }, 0);
    const transitionFrames = Math.max(0, props.scenes.length - 1) * 12;
    return Math.max(1, sceneFrames - transitionFrames);
  };

  return (
    <>
      <Composition
        id="ExplainerLandscape"
        component={compositionComponent}
        durationInFrames={calculateTotalFrames(defaultProps)}
        fps={30}
        width={1920}
        height={1080}
        defaultProps={defaultProps}
        calculateMetadata={({ props }) => {
          return {
            durationInFrames: calculateTotalFrames((props as unknown) as ExplainerVideoProps),
          };
        }}
      />

      <Composition
        id="ExplainerPortrait"
        component={compositionComponent}
        durationInFrames={calculateTotalFrames(defaultProps)}
        fps={30}
        width={1080}
        height={1920}
        defaultProps={{ ...defaultProps, aspectRatio: "9:16" }}
        calculateMetadata={({ props }) => {
          return {
            durationInFrames: calculateTotalFrames((props as unknown) as ExplainerVideoProps),
          };
        }}
      />
    </>
  );
};
