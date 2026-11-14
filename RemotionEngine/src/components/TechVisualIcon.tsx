import React from "react";
import { useCurrentFrame, useVideoConfig, spring, interpolate } from "remotion";
import {
  Server,
  Database,
  Cpu,
  Cloud,
  Shield,
  Boxes,
  Terminal,
  Layers,
  Globe,
  GitBranch,
  Activity,
  Sparkles,
  Laptop,
  HardDrive,
  Code2,
  Workflow,
  Zap,
  Lock,
  Compass,
  FileCode,
  Network,
  DollarSign,
  TrendingUp,
  Coins,
  Wallet,
  CreditCard,
  BarChart3,
  PieChart,
  Percent,
  Landmark,
  ShieldCheck,
  Target,
  Briefcase,
  PiggyBank,
  ArrowUpRight,
  LineChart,
  Lightbulb,
  BookOpen,
  GraduationCap,
  Flame,
  Rocket,
  Brain,
  Clock,
  LucideIcon,
} from "lucide-react";

interface TechVisualIconProps {
  name?: string;
  size?: number;
  primaryColor?: string;
  accentColor?: string;
  delay?: number;
  showOrbitalRings?: boolean;
}

const ICON_MAP: Record<string, LucideIcon> = {
  // Tech & Infrastructure
  server: Server,
  database: Database,
  db: Database,
  cpu: Cpu,
  chip: Cpu,
  cloud: Cloud,
  shield: Shield,
  security: Shield,
  boxes: Boxes,
  container: Boxes,
  docker: Boxes,
  terminal: Terminal,
  layers: Layers,
  globe: Globe,
  gitbranch: GitBranch,
  git: GitBranch,
  activity: Activity,
  sparkles: Sparkles,
  laptop: Laptop,
  client: Laptop,
  harddrive: HardDrive,
  code: Code2,
  workflow: Workflow,
  pipeline: Workflow,
  zap: Zap,
  fast: Zap,
  lock: Lock,
  compass: Compass,
  filecode: FileCode,
  network: Network,
  microservice: Boxes,
  api: Terminal,

  // Finance & Wealth Management (Hot Trend)
  dollar: DollarSign,
  dollarsign: DollarSign,
  money: DollarSign,
  trendingup: TrendingUp,
  growth: TrendingUp,
  profit: TrendingUp,
  coins: Coins,
  crypto: Coins,
  wealth: Coins,
  wallet: Wallet,
  income: Wallet,
  creditcard: CreditCard,
  expense: CreditCard,
  barchart: BarChart3,
  chart: BarChart3,
  piechart: PieChart,
  allocation: PieChart,
  percent: Percent,
  interest: Percent,
  roi: Percent,
  landmark: Landmark,
  bank: Landmark,
  institution: Landmark,
  shieldcheck: ShieldCheck,
  riskmanagement: ShieldCheck,
  target: Target,
  goal: Target,
  briefcase: Briefcase,
  portfolio: Briefcase,
  piggybank: PiggyBank,
  saving: PiggyBank,
  arrowupright: ArrowUpRight,
  linechart: LineChart,

  // General & Educational
  lightbulb: Lightbulb,
  idea: Lightbulb,
  bookopen: BookOpen,
  education: BookOpen,
  graduationcap: GraduationCap,
  flame: Flame,
  hot: Flame,
  rocket: Rocket,
  launch: Rocket,
  brain: Brain,
  mind: Brain,
  clock: Clock,
  time: Clock,
};

export const TechVisualIcon: React.FC<TechVisualIconProps> = ({
  name = "sparkles",
  size = 64,
  primaryColor = "#3b82f6",
  accentColor = "#8b5cf6",
  delay = 0,
  showOrbitalRings = true,
}) => {
  const frame = useCurrentFrame();
  const { fps } = useVideoConfig();

  const normalizedName = name.toLowerCase().replace(/[^a-z0-9]/g, "");
  const IconComponent = ICON_MAP[normalizedName] || Sparkles;

  // Spring entry animation
  const scaleSpring = spring({
    frame: frame - delay,
    fps,
    config: { damping: 12, stiffness: 100 },
  });

  // Floating vertical hover
  const floatOffset = interpolate(
    Math.sin((frame + delay * 10) / 18),
    [-1, 1],
    [-7, 7]
  );

  // Rotating orbital angles
  const orbitRotation1 = ((frame + delay * 5) * 1.5) % 360;
  const orbitRotation2 = -(((frame + delay * 5) * 1.2) % 360);

  // Pulse glow scale
  const pulseGlow = interpolate(
    Math.sin((frame + delay * 8) / 14),
    [-1, 1],
    [0.85, 1.15]
  );

  return (
    <div
      style={{
        position: "relative",
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        width: size * 1.8,
        height: size * 1.8,
        transform: `translateY(${floatOffset}px) scale(${Math.max(0, scaleSpring)})`,
      }}
    >
      {/* 1. Neon Radial Glow Aura */}
      <div
        style={{
          position: "absolute",
          width: size * 1.5 * pulseGlow,
          height: size * 1.5 * pulseGlow,
          borderRadius: "50%",
          background: `radial-gradient(circle, ${primaryColor}40 0%, ${accentColor}15 50%, transparent 70%)`,
          filter: "blur(18px)",
          pointerEvents: "none",
        }}
      />

      {/* 2. Outer Orbital Ring 1 */}
      {showOrbitalRings && (
        <div
          style={{
            position: "absolute",
            width: size * 1.55,
            height: size * 1.55,
            borderRadius: "50%",
            border: `1.5px dashed ${primaryColor}55`,
            transform: `rotate(${orbitRotation1}deg)`,
            pointerEvents: "none",
          }}
        >
          {/* Orbital Dot */}
          <div
            style={{
              position: "absolute",
              top: -4,
              left: "50%",
              width: 7,
              height: 7,
              borderRadius: "50%",
              backgroundColor: primaryColor,
              boxShadow: `0 0 10px ${primaryColor}`,
            }}
          />
        </div>
      )}

      {/* 3. Inner Orbital Ring 2 */}
      {showOrbitalRings && (
        <div
          style={{
            position: "absolute",
            width: size * 1.3,
            height: size * 1.3,
            borderRadius: "50%",
            border: `1px solid ${accentColor}44`,
            transform: `rotate(${orbitRotation2}deg)`,
            pointerEvents: "none",
          }}
        >
          {/* Second Orbital Dot */}
          <div
            style={{
              position: "absolute",
              bottom: -3,
              left: "50%",
              width: 6,
              height: 6,
              borderRadius: "50%",
              backgroundColor: accentColor,
              boxShadow: `0 0 8px ${accentColor}`,
            }}
          />
        </div>
      )}

      {/* 4. Center Glassmorphic Circle Badge */}
      <div
        style={{
          position: "relative",
          display: "flex",
          alignItems: "center",
          justifyContent: "center",
          width: size * 1.15,
          height: size * 1.15,
          borderRadius: "50%",
          background: "linear-gradient(135deg, rgba(30, 41, 59, 0.85) 0%, rgba(15, 23, 42, 0.95) 100%)",
          border: `1.5px solid ${primaryColor}77`,
          boxShadow: `0 8px 30px rgba(0, 0, 0, 0.6), 0 0 25px ${primaryColor}44`,
          backdropFilter: "blur(12px)",
        }}
      >
        <IconComponent
          size={size * 0.6}
          color="#ffffff"
          strokeWidth={2.2}
          style={{
            filter: `drop-shadow(0 0 12px ${primaryColor})`,
          }}
        />
      </div>
    </div>
  );
};
