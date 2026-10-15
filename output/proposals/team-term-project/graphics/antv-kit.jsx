/** @jsxRuntime classic */
/** @jsx h */
// Shared helpers for the CHOOGuard AntV Infographic structures.
// Resolves the isolated, already-installed @antv/infographic@0.2.20 (no new installs).
import path from 'node:path';
import fs from 'node:fs';
import { createRequire } from 'node:module';
import { fileURLToPath } from 'node:url';
const require = createRequire(import.meta.url);
const __dirname = path.dirname(fileURLToPath(import.meta.url));

const TOOL_ROOT = '/Users/um-yunsang/.omp/agent/tools/design-graphics/node_modules';
const A = require(path.join(TOOL_ROOT, '@antv/infographic'));
const { renderToString } = require(path.join(TOOL_ROOT, '@antv/infographic/lib/ssr'));
const chroma = require(path.join(TOOL_ROOT, 'chroma-js'));

const { jsx, Fragment } = A;

function h(type, props, ...children) {
  const p = { ...(props || {}) };
  const flat = children.flat(Infinity).filter((c) => c !== null && c !== undefined && c !== false);
  if (flat.length === 1) p.children = flat[0];
  else if (flat.length > 1) p.children = flat;
  return jsx(type, p);
}

// Contract palette (redesign-contract.json direction.provisionalColors) + tonal derivatives.
const C = {
  ink: '#142025',
  night: '#0C1D26',
  paper: '#F4F2EA',
  mist: '#E1E8E6',
  accent: '#D0F266',
  accentInk: '#3D550A',
  line: '#809390',
};
const hex = (c) => c.hex().toUpperCase();
C.nightRaised = hex(chroma.mix(C.night, C.mist, 0.1, 'oklab')); // tonal lift on night
C.nightLine = hex(chroma.mix(C.night, C.mist, 0.32, 'oklab')); // quiet rule/track on night
C.nightText = hex(chroma.mix(C.night, C.paper, 0.86, 'oklab')); // secondary text on night
C.paperDeep = hex(chroma.mix(C.paper, C.line, 0.14, 'oklab')); // tonal field on paper
C.mistDeep = hex(chroma.mix(C.mist, C.line, 0.28, 'oklab')); // tonal field on mist
C.inkSoft = hex(chroma.mix(C.ink, C.paper, 0.18, 'oklab')); // secondary text on light

const FRAME = { width: 1280, height: 608 };
const ICON_DIR = path.join(__dirname, 'icons');

// Custom icon loader: `icon lucide/<name>` -> local vetted Lucide SVG (offline, deterministic).
A.registerResourceLoader(async (cfg) => {
  const m = /^lucide\/([a-z0-9-]+)$/.exec(String(cfg.data || ''));
  if (!m) return null;
  const file = path.join(ICON_DIR, `lucide-${m[1]}.svg`);
  if (!fs.existsSync(file)) throw new Error(`Missing icon file: ${file}`);
  return A.loadSVGResource(fs.readFileSync(file, 'utf8'));
});

const nl = (s) => String(s ?? '').replace(/\\n/g, '\n');

// ---- shape helpers (absolute coordinates inside the 1280x608 frame) ----
const { Group, Rect, Path, Ellipse, Polygon, Text } = A;
const { ItemIcon, ItemLabel, ItemDesc } = A;

const P = (d, o = {}) => (
  <Path
    d={d}
    width={FRAME.width}
    height={FRAME.height}
    fill={o.fill || 'none'}
    stroke={o.stroke}
    strokeWidth={o.sw}
    strokeLinecap={o.cap || 'round'}
    strokeLinejoin={o.join || 'round'}
    strokeDasharray={o.dash}
    opacity={o.opacity}
    fillOpacity={o.fillOpacity}
  />
);

const disc = (cx, cy, d, o = {}) => (
  <Ellipse
    x={cx - d / 2}
    y={cy - d / 2}
    width={d}
    height={d}
    fill={o.fill || 'none'}
    stroke={o.stroke}
    strokeWidth={o.sw}
    strokeDasharray={o.dash}
    opacity={o.opacity}
    fillOpacity={o.fillOpacity}
  />
);

const rrect = (x, y, w, hgt, r, o = {}) => (
  <Rect
    x={x}
    y={y}
    width={w}
    height={hgt}
    rx={r}
    ry={r}
    fill={o.fill || 'none'}
    stroke={o.stroke}
    strokeWidth={o.sw}
    strokeDasharray={o.dash}
    opacity={o.opacity}
    fillOpacity={o.fillOpacity}
  />
);

// Filled triangle (direction chevron / arrow head). dir in degrees: 0 = pointing right.
const tri = (cx, cy, len, wid, dir, fill) => {
  const a = (dir * Math.PI) / 180;
  const pt = (px, py) => ({
    x: +(cx + px * Math.cos(a) - py * Math.sin(a)).toFixed(2),
    y: +(cy + px * Math.sin(a) + py * Math.cos(a)).toFixed(2),
  });
  return <Polygon points={[pt(len / 2, 0), pt(-len / 2, -wid / 2), pt(-len / 2, wid / 2)]} fill={fill} />;
};

const icon = (indexes, cx, cy, size, color) => (
  <ItemIcon indexes={indexes} x={cx - size / 2} y={cy - size / 2} size={size} fill={color} />
);

// Static (non-datum) text.
const T = (x, y, w, hgt, str, o = {}) => (
  <Text
    x={x}
    y={y}
    width={w}
    height={hgt}
    fontSize={o.size || 28}
    fontWeight={o.weight || 'normal'}
    fontFamily={o.family}
    fill={o.color || C.ink}
    alignHorizontal={o.align || 'left'}
    alignVertical={o.valign || 'top'}
    lineHeight={o.lh || 1.3}
    letterSpacing={o.ls}
    data-role={o.role}
  >
    {nl(str)}
  </Text>
);

// Datum-bound label / description (text is read from the DSL datum).
const L = (indexes, x, y, w, hgt, str, o = {}) => (
  <ItemLabel
    indexes={indexes}
    x={x}
    y={y}
    width={w}
    height={hgt}
    fontSize={o.size || 28}
    fontWeight={o.weight || 'normal'}
    fontFamily={o.family}
    fill={o.color || C.ink}
    alignHorizontal={o.align || 'left'}
    alignVertical={o.valign || 'top'}
    lineHeight={o.lh || 1.3}
    data-role={o.role}
  >
    {nl(str)}
  </ItemLabel>
);

const D = (indexes, x, y, w, hgt, str, o = {}) => (
  <ItemDesc
    indexes={indexes}
    x={x}
    y={y}
    width={w}
    height={hgt}
    fontSize={o.size || 28}
    fontWeight={o.weight || 'normal'}
    fontFamily={o.family}
    fill={o.color || C.ink}
    alignHorizontal={o.align || 'left'}
    alignVertical={o.valign || 'top'}
    lineHeight={o.lh || 1.3}
    wordWrap={true}
    data-role={o.role}
  >
    {nl(str)}
  </ItemDesc>
);

const frameAnchor = () => <Rect x={0} y={0} width={FRAME.width} height={FRAME.height} fill="none" />;

export {
  A, h, Fragment, C, FRAME, chroma, renderToString,
  P, disc, rrect, tri, icon, T, L, D, nl, frameAnchor, Group,
};
