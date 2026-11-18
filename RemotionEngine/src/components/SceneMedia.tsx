import React from "react";
import { Img, OffthreadVideo, interpolate, useCurrentFrame } from "remotion";
import { ExplainerScene } from "../types";

export const SceneMedia: React.FC<{
  scene: ExplainerScene;
  isPortrait?: boolean;
  durationInFrames?: number;
}> = ({ scene, isPortrait = false, durationInFrames = 150 }) => {
  const frame = useCurrentFrame();
  const zoom = interpolate(frame, [0, durationInFrames], [1, 1.12], {
    extrapolateLeft: "clamp",
    extrapolateRight: "clamp",
  });
  const panX = interpolate(frame, [0, durationInFrames], [-1.5, 1.5], {
    extrapolateLeft: "clamp",
    extrapolateRight: "clamp",
  });
  const split = scene.layout === "split-image";

  if (!scene.imageUrl && !scene.videoUrl) return null;

  return (
    <div
      style={{
        position: "absolute",
        inset: split ? "12% 50% 12% 5%" : 0,
        overflow: "hidden",
        borderRadius: split ? 24 : 0,
        zIndex: split ? 1 : 0,
      }}
    >
      {scene.imageUrl ? (
        <Img
          src={scene.imageUrl}
          delayRenderRetries={3}
          delayRenderTimeoutInMilliseconds={15000}
          style={{
            width: "100%",
            height: "100%",
            objectFit: "cover",
            transform: `scale(${zoom}) translateX(${panX}% )`,
          }}
        />
      ) : (
        <OffthreadVideo
          src={scene.videoUrl as string}
          delayRenderRetries={3}
          delayRenderTimeoutInMilliseconds={15000}
          style={{
            width: "100%",
            height: "100%",
            objectFit: "cover",
            transform: `scale(${zoom}) translateX(${panX}% )`,
          }}
        />
      )}
      <div
        style={{
          position: "absolute",
          inset: 0,
          background: split
            ? "linear-gradient(90deg, rgba(0,0,0,0.15), rgba(0,0,0,0.55))"
            : "linear-gradient(180deg, rgba(0,0,0,0.12) 35%, rgba(0,0,0,0.82) 100%)",
          pointerEvents: "none",
        }}
      />
    </div>
  );
};
