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
  | "key-takeaway"
  | "timeline"
  | "bar-chart"
  | "quote";

export interface WordTiming {
  word: string;
  start_ms: number;
  end_ms: number;
  itemIndex?: number;
}

export interface TimelineItem {
  label: string;
  description?: string;
  date?: string;
}

export interface BarChartItem {
  label: string;
  value: number;
  suffix?: string;
}

export interface QuoteItem {
  quote: string;
  author?: string;
  role?: string;
}

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
  videoUrl?: string;
  icon?: string;
  iconSvg?: string;
  iconifyName?: string;
  layout?: "fullbleed-image" | "split-image";
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
  wordTimings?: WordTiming[];
  timelineItems?: TimelineItem[];
  barItems?: BarChartItem[];
  quote?: QuoteItem;
}

export interface ExplainerVideoProps {
  topic: string;
  category?: "tech" | "finance" | "general";
  aspectRatio: "16:9" | "9:16";
  durationInFrames?: number;
  audioPath?: string;
  backgroundStyle?: "aurora" | "dotgrid" | "paper" | "mesh" | "none";
  stylePack?: import("./themes").StylePack;
  stylePackId?: string;
  subtitles: SubtitleWord[];
  chapters: Chapter[];
  scenes: ExplainerScene[];
  theme?: {
    primaryColor: string;
    accentColor: string;
    backgroundColor: string;
  };
}
