import React, { useMemo } from "react";
import { interpolate, spring, useCurrentFrame, useVideoConfig } from "remotion";
import { SubtitleWord } from "../types";
import { useStyle } from "../themeContext";

interface DynamicSubtitlesProps {
  subtitles: SubtitleWord[];
  highlightColor?: string;
  isPortrait?: boolean;
}

interface Chunk {
  words: SubtitleWord[];
  startMs: number;
  endMs: number;
}

const MIN_WORDS = 2;
const MAX_WORDS = 5;
const SOFT_BREAK = /[,;:]$/;
const HARD_BREAK = /[.!?…]$/;

/** Group words into short, TikTok-style chunks, breaking on punctuation when possible. */
function buildChunks(subtitles: SubtitleWord[]): Chunk[] {
  const chunks: Chunk[] = [];
  let current: SubtitleWord[] = [];
  const flush = () => {
    if (current.length === 0) return;
    chunks.push({
      words: current,
      startMs: current[0].start_ms,
      endMs: current[current.length - 1].end_ms,
    });
    current = [];
  };
  for (const w of subtitles) {
    current.push(w);
    const text = (w.word || "").trim();
    const hard = HARD_BREAK.test(text);
    const soft = SOFT_BREAK.test(text) && current.length >= MIN_WORDS;
    if (hard || soft || current.length >= MAX_WORDS) flush();
  }
  flush();
  // Avoid orphan one-word chunks by merging them into the previous chunk when it fits.
  const merged: Chunk[] = [];
  for (const c of chunks) {
    const prev = merged[merged.length - 1];
    if (c.words.length < MIN_WORDS && prev && prev.words.length + c.words.length <= MAX_WORDS + 1 && !HARD_BREAK.test(prev.words[prev.words.length - 1].word || "")) {
      prev.words = [...prev.words, ...c.words];
      prev.endMs = c.endMs;
    } else {
      merged.push({ ...c });
    }
  }
  return merged;
}

export const DynamicSubtitles: React.FC<DynamicSubtitlesProps> = ({
  subtitles,
  highlightColor,
  isPortrait = false,
}) => {
  const frame = useCurrentFrame();
  const { fps, height } = useVideoConfig();
  const theme = useStyle();
  const activeColor = highlightColor ?? "#facc15";

  const chunks = useMemo(() => buildChunks(subtitles || []), [subtitles]);

  const currentMs = (frame / fps) * 1000;

  // A chunk stays visible until the next one begins (or 350ms after its last word).
  let activeIndex = -1;
  for (let i = 0; i < chunks.length; i++) {
    const next = chunks[i + 1];
    const holdUntil = Math.min(chunks[i].endMs + 350, next ? next.startMs : Infinity);
    if (currentMs >= chunks[i].startMs - 40 && currentMs < holdUntil) {
      activeIndex = i;
      break;
    }
  }
  if (activeIndex < 0) return null;
  const chunk = chunks[activeIndex];

  const chunkStartFrame = Math.round(((chunk.startMs - 40) / 1000) * fps);
  const enter = spring({
    frame: frame - chunkStartFrame,
    fps,
    config: { damping: 16, stiffness: 220 },
  });

  const fontSize = isPortrait ? 62 : 54;
  // Portrait: keep clear of platform UI (bottom ~20%); landscape: lower third.
  const positionStyle: React.CSSProperties = isPortrait
    ? { top: height * 0.72 }
    : { bottom: 78 };

  return (
    <div
      style={{
        position: "absolute",
        left: 0,
        right: 0,
        display: "flex",
        justifyContent: "center",
        padding: isPortrait ? "0 48px" : "0 120px",
        zIndex: 60,
        pointerEvents: "none",
        ...positionStyle,
      }}
    >
      <div
        style={{
          display: "flex",
          flexWrap: "wrap",
          justifyContent: "center",
          alignItems: "baseline",
          columnGap: isPortrait ? 16 : 14,
          rowGap: 4,
          maxWidth: isPortrait ? 900 : 1500,
          opacity: Math.min(1, Math.max(0, enter * 1.5)),
          transform: `translateY(${interpolate(Math.max(0, enter), [0, 1], [14, 0])}px)`,
        }}
      >
        {chunk.words.map((w, idx) => {
          const isActive = currentMs >= w.start_ms && currentMs <= w.end_ms;
          const isPast = currentMs > w.end_ms;
          const pop = isActive
            ? interpolate(currentMs, [w.start_ms, w.start_ms + 120], [1, 1.14], {
                extrapolateLeft: "clamp",
                extrapolateRight: "clamp",
              })
            : 1;
          const color = isActive ? activeColor : isPast ? "#ffffff" : "rgba(255, 255, 255, 0.62)";
          return (
            <span
              key={`${w.start_ms}-${idx}`}
              style={{
                fontSize,
                lineHeight: 1.2,
                fontWeight: 900,
                color,
                transform: `scale(${pop})`,
                transformOrigin: "center bottom",
                display: "inline-block",
                fontFamily: theme.fonts.body,
                // Outline + soft shadow keeps text legible on any background without a heavy box.
                WebkitTextStroke: `${isPortrait ? 8 : 7}px rgba(5, 8, 16, 0.92)`,
                paintOrder: "stroke fill",
                textShadow: "0 6px 18px rgba(0, 0, 0, 0.6)",
                letterSpacing: "-0.01em",
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
