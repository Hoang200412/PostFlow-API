import { spring, useCurrentFrame, useVideoConfig } from "remotion";
import { MotionStyle } from "./themes";

export function useStagger(index: number, motion: MotionStyle = "springy") {
  const frame = useCurrentFrame();
  const { fps } = useVideoConfig();
  const delay = index * (motion === "snappy" ? 4 : motion === "smooth" ? 9 : 6);
  const config =
    motion === "snappy"
      ? { damping: 28, stiffness: 220 }
      : motion === "smooth"
        ? { damping: 20, stiffness: 80 }
        : { damping: 12, stiffness: 100 };
  const progress = spring({ frame: frame - delay, fps, config });
  return { progress: Math.max(0, progress), delay };
}
