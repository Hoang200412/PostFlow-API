export interface SubtitleWord {
  word: string;
  start_ms: number;
  end_ms: number;
  duration_ms?: number;
}

export interface Chapter {
  id: string;
  title: string;
  startFrame: number;
  durationInFrames: number;
}

export type SceneType =
  | "hero"
  | "flowchart"
  | "card-comparison"
  | "metric-highlight"
  | "code-snippet"
  | "key-takeaway";

export interface FlowchartNode {
  id: string;
  label: string;
  sublabel?: string;
  icon?: string;
  status?: "normal" | "active" | "highlight";
}

export interface CardComparisonItem {
  name: string;
  badge?: string;
  icon?: string;
  pros: string[];
  cons?: string[];
  isWinner?: boolean;
}

export interface MetricItem {
  value: string;
  prefix?: string;
  suffix?: string;
  label: string;
  description?: string;
}

export interface ExplainerScene {
  id: string;
  type: SceneType;
  category?: "tech" | "finance" | "general";
  title: string;
  subtitle?: string;
  narration?: string;
  durationInFrames: number;
  
  // Visual Motion Elements
  visualIcon?: string;
  visualTheme?: string;
  imageUrl?: string;
  icon?: string;
  tags?: string[];

  // Custom props per scene type
  flowchartNodes?: FlowchartNode[];
  comparisonCards?: CardComparisonItem[];
  metric?: MetricItem;
  codeSnippet?: {
    language: string;
    code: string;
    highlightLine?: number;
  };
  takeaways?: string[];
}

export interface ExplainerVideoProps {
  topic: string;
  category?: "tech" | "finance" | "general";
  aspectRatio: "16:9" | "9:16";
  durationInFrames?: number;
  audioPath?: string;
  subtitles: SubtitleWord[];
  chapters: Chapter[];
  scenes: ExplainerScene[];
  theme?: {
    primaryColor: string;
    accentColor: string;
    backgroundColor: string;
  };
}

