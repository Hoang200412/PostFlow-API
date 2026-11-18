import React, { createContext, useContext } from "react";
import { DEFAULT_STYLE_PACK, StylePack } from "./themes";

export const StyleContext = createContext<StylePack>(DEFAULT_STYLE_PACK);

export const StyleProvider: React.FC<{ pack: StylePack; children: React.ReactNode }> = ({
  pack,
  children,
}) => <StyleContext.Provider value={pack}>{children}</StyleContext.Provider>;

export function useStyle(): StylePack {
  return useContext(StyleContext);
}
