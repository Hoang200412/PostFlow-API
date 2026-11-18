import React from "react";
import { useStyle } from "../themeContext";

export const GlassCard: React.FC<{ children: React.ReactNode; accent?: boolean; style?: React.CSSProperties }> = ({
  children,
  accent = false,
  style,
}) => {
  const theme = useStyle();
  const cardStyles: Record<typeof theme.cardStyle, React.CSSProperties> = {
    glass: { backgroundColor: `${theme.palette.surface}cc`, border: `1px solid ${theme.palette.primary}55`, boxShadow: `0 18px 50px ${theme.palette.bg}55` },
    flat: { backgroundColor: theme.palette.surface, border: `1px solid ${theme.palette.muted}33`, boxShadow: "0 8px 18px rgba(0,0,0,0.12)" },
    outline: { backgroundColor: "transparent", border: `2px solid ${accent ? theme.palette.primary : theme.palette.muted}` },
    neo: { backgroundColor: theme.palette.surface, border: `1px solid ${theme.palette.primary}88`, boxShadow: `8px 8px 0 ${theme.palette.primary}35, -4px -4px 0 ${theme.palette.accent}25` },
  };

  return (
    <div
      style={{
        ...cardStyles[theme.cardStyle],
        borderRadius: 18,
        padding: 24,
        fontFamily: theme.fonts.body,
        ...style,
      }}
    >
      {children}
    </div>
  );
};
