import React, { useMemo } from "react";
import { interpolate, interpolateColors, useCurrentFrame, useVideoConfig } from "remotion";
import { SubtitleWord } from "../types";

interface DynamicSubtitlesProps {
  subtitles: SubtitleWord[];
  highlightColor?: string;
  isPortrait?: boolean;
}

export const DynamicSubtitles: React.FC<DynamicSubtitlesProps> = ({
  subtitles,
  highlightColor = "#facc15",
  isPortrait = false,
}) => {
  const frame = useCurrentFrame();
  const { fps } = useVideoConfig();

  // Current playback time in milliseconds
  const currentMs = (frame / fps) * 1000;

  // Group words into phrases/chunks of 10 words, then balance them into 2 lines.
  const phrases = useMemo(() => {
    if (!subtitles || subtitles.length === 0) return [];
    const chunks: { lines: SubtitleWord[][]; words: SubtitleWord[]; startMs: number; endMs: number }[] = [];
    const chunkSize = 10;
    for (let i = 0; i < subtitles.length; i += chunkSize) {
      const slice = subtitles.slice(i, i + chunkSize);
      const firstLineSize = slice.length <= 5 ? slice.length : Math.ceil(slice.length / 2);
      chunks.push({
        lines: [slice.slice(0, firstLineSize), slice.slice(firstLineSize)],
        words: slice,
        startMs: slice[0].start_ms,
        endMs: slice[slice.length - 1].end_ms + 250,
      });
    }
    return chunks;
  }, [subtitles]);

  // Find active phrase
  const activeChunk = phrases.find(
    (chunk) => currentMs >= chunk.startMs - 50 && currentMs <= chunk.endMs
  );

  if (!activeChunk) return null;

  return (
    <div
      style={{
        position: "absolute",
        bottom: isPortrait ? 280 : 44,
        left: 0,
        right: 0,
        display: "flex",
        justifyContent: "center",
        alignItems: "center",
        padding: isPortrait ? "0 40px" : "0 60px",
        zIndex: 60,
        pointerEvents: "none",
        minHeight: 150,
      }}
    >
      <div
        style={{
          display: "flex",
          flexDirection: "column",
          justifyContent: "center",
          gap: isPortrait ? 8 : 6,
          maxWidth: isPortrait ? 930 : 1500,
          backgroundColor: "rgba(5, 10, 20, 0.82)",
          padding: isPortrait ? "14px 30px" : "10px 28px",
          borderRadius: 16,
          boxShadow: "0 8px 24px rgba(0, 0, 0, 0.55)",
        }}
      >
        {activeChunk.lines.filter((line) => line.length > 0).map((line, lineIndex) => (
          <div
            key={lineIndex}
            style={{
              display: "flex",
              flexWrap: "nowrap",
              justifyContent: "center",
              gap: isPortrait ? "10px" : "12px",
              whiteSpace: "nowrap",
            }}
          >
            {line.map((w, idx) => {
              const isActive = currentMs >= w.start_ms && currentMs <= w.end_ms;
              const isPast = currentMs > w.end_ms;
              const wordProgress = isActive
                ? interpolate(currentMs, [w.start_ms, w.end_ms], [0, 1], {
                    extrapolateLeft: "clamp",
                    extrapolateRight: "clamp",
                  })
                : 0;
              const scale = 1 + wordProgress * 0.08;
              const color = isActive
                ? interpolateColors(wordProgress, [0, 1], [highlightColor, "#fff3a3"])
                : isPast
                  ? "#ffffff"
                  : "rgba(255, 255, 255, 0.52)";

              return (
                <span
                  key={`${w.start_ms}-${idx}`}
                  style={{
                    fontSize: isPortrait ? 64 : 48,
                    fontWeight: isActive ? 900 : isPast ? 800 : 700,
                    color,
                    transform: `scale(${scale})`,
                    transformOrigin: "center bottom",
                    textShadow:
                      "-2px -2px 0 #050505, 2px -2px 0 #050505, -2px 2px 0 #050505, 2px 2px 0 #050505, 0 4px 8px rgba(0,0,0,0.95)",
                    WebkitTextStroke: "1px rgba(0, 0, 0, 0.75)",
                    fontFamily: "Be Vietnam Pro, Inter, system-ui, sans-serif",
                    display: "inline-block",
                  }}
                >
                  {w.word}
                </span>
              );
            })}
          </div>
        ))}
      </div>
    </div>
  );
};
