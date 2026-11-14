import React, { useMemo } from "react";
import { useCurrentFrame, useVideoConfig } from "remotion";
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

  // Group words into phrases/chunks of ~6-8 words for clean readability
  const phrases = useMemo(() => {
    if (!subtitles || subtitles.length === 0) return [];
    const chunks: { words: SubtitleWord[]; startMs: number; endMs: number }[] = [];
    const chunkSize = 7;
    for (let i = 0; i < subtitles.length; i += chunkSize) {
      const slice = subtitles.slice(i, i + chunkSize);
      chunks.push({
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
        bottom: isPortrait ? 140 : 44,
        left: 0,
        right: 0,
        display: "flex",
        justifyContent: "center",
        alignItems: "center",
        padding: "0 40px",
        zIndex: 60,
        pointerEvents: "none",
      }}
    >
      <div
        style={{
          display: "flex",
          flexWrap: "wrap",
          justifyContent: "center",
          gap: "8px 12px",
          maxWidth: isPortrait ? 640 : 1000,
          backgroundColor: "rgba(10, 15, 29, 0.75)",
          border: "1px solid rgba(255, 255, 255, 0.12)",
          backdropFilter: "blur(16px)",
          padding: "12px 24px",
          borderRadius: 16,
          boxShadow: "0 10px 30px rgba(0, 0, 0, 0.5)",
        }}
      >
        {activeChunk.words.map((w, idx) => {
          const isActive = currentMs >= w.start_ms && currentMs <= w.end_ms;
          const isPast = currentMs > w.end_ms;

          return (
            <span
              key={idx}
              style={{
                fontSize: isPortrait ? 28 : 24,
                fontWeight: isActive ? 800 : isPast ? 600 : 500,
                color: isActive
                  ? highlightColor
                  : isPast
                  ? "#ffffff"
                  : "rgba(255, 255, 255, 0.4)",
                transform: isActive ? "scale(1.08)" : "scale(1)",
                transition: "all 0.1s ease",
                textShadow: isActive
                  ? `0 0 12px ${highlightColor}88, 0 2px 4px rgba(0,0,0,0.8)`
                  : "0 2px 4px rgba(0,0,0,0.6)",
                fontFamily: "'Inter', system-ui, sans-serif",
                display: "inline-block",
              }}
            >
              {w.word}
            </span>
          );
        })}
      </div>
    </div>
  );
};

