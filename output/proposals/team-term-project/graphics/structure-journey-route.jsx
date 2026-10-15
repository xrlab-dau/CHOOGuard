/** @jsxRuntime classic */
/** @jsx h */
// AntV custom structure `choo-journey-route` (slide 3): wayfinding route through
// Learn -> Apply (four response stops nested in a dark field) -> Reflect.
import * as K from './antv-kit.jsx';
const { A, h, C, P, disc, rrect, tri, icon, L, frameAnchor, Group } = K;
const { ItemsGroup, registerStructure, registerTemplate } = A;

const ROUTE_Y = 330;
const ZONES = [
  { x: 0, w: 300, y: 112, h: 384, rx: 56 },
  { x: 320, w: 640, y: 32, h: 544, rx: 72 },
  { x: 980, w: 300, y: 112, h: 384, rx: 56 },
];
const STOPS = [
  { xs: [84, 232], d: 72, labelW: 132 },
  { xs: [409, 563, 717, 871], d: 104, labelW: 148 },
  { xs: [1052, 1196], d: 72, labelW: 132 },
];

const JourneyRoute = (props) => {
  const { data } = props;
  const phases = data.items || [];
  const decor = [];
  const items = [];

  // 1. zone fields (figure/ground: light open platforms, one dominant dark field)
  ZONES.forEach((z, p) => {
    decor.push(rrect(z.x, z.y, z.w, z.h, z.rx, { fill: p === 1 ? C.night : C.paper }));
  });
  // nested track bed inside the Apply field
  const bed = ZONES[1];
  decor.push(rrect(bed.x + 16, ROUTE_Y - 72, bed.w - 32, 144, 72, { fill: C.nightRaised }));

  // 2. route (continuous line; colour changes with the field it crosses)
  decor.push(P(`M 24 ${ROUTE_Y} H 330`, { stroke: C.ink, sw: 10 }));
  decor.push(P(`M 330 ${ROUTE_Y} H 950`, { stroke: C.accent, sw: 14 }));
  decor.push(P(`M 950 ${ROUTE_Y} H 1262`, { stroke: C.ink, sw: 10 }));
  // termini: origin ring + end bar
  decor.push(disc(24, ROUTE_Y, 30, { fill: C.mist, stroke: C.ink, sw: 8 }));
  decor.push(P(`M 1262 ${ROUTE_Y - 26} V ${ROUTE_Y + 26}`, { stroke: C.ink, sw: 10 }));
  // direction heads at the genuine order boundaries
  [[158, C.ink], [310, C.ink], [486, C.accent], [640, C.accent], [794, C.accent], [970, C.ink], [1124, C.ink]].forEach(
    ([x, col]) => decor.push(tri(x, ROUTE_Y, 22, 26, 0, col)),
  );

  phases.forEach((phase, p) => {
    const z = ZONES[p];
    const dark = p === 1;
    const inkOn = dark ? C.paper : C.ink;
    // header: line-number badge + phase title
    const hy = z.y + 36 + 28;
    const bx = z.x + 36 + 28;
    decor.push(disc(bx, hy, 56, { fill: dark ? C.accent : C.ink }));
    items.push(
      K.T(bx - 28, hy - 28, 56, 56, `0${p + 1}`, {
        size: 28, weight: 'bold', family: 'Inter', align: 'center', valign: 'middle', lh: 1,
        color: dark ? C.ink : C.paper, role: 'badge-numeral',
      }),
    );
    items.push(
      L([p], bx + 28 + 16, hy - 28, dark ? 300 : 180, 56, phase.label, {
        size: 32, weight: 'bold', valign: 'middle', lh: 1.2, color: inkOn, role: 'phase-title',
      }),
    );

    // stops
    const s = STOPS[p];
    (phase.children || []).forEach((stop, c) => {
      const cx = s.xs[c];
      const idx = [p, c];
      if (dark) {
        decor.push(disc(cx, ROUTE_Y, s.d, { fill: C.paper, stroke: C.accent, sw: 8 }));
      } else {
        decor.push(disc(cx, ROUTE_Y, s.d, { fill: C.paper, stroke: C.ink, sw: 8 }));
      }
      items.push(icon(idx, cx, ROUTE_Y, Math.round(s.d * 0.5), dark ? C.night : C.ink));
      const ly = ROUTE_Y + s.d / 2 + 16;
      items.push(
        L(idx, cx - s.labelW / 2, ly, s.labelW, 74, stop.label, {
          size: 28, weight: dark ? 'bold' : 'normal', align: 'center', valign: 'top', lh: 1.3,
          color: dark ? C.paper : C.ink, role: dark ? 'stop-label-apply' : 'stop-label',
        }),
      );
    });
  });

  return (
    <Group id="infographic-container">
      {frameAnchor()}
      <Group>{decor}</Group>
      <ItemsGroup>{items}</ItemsGroup>
    </Group>
  );
};

registerStructure('choo-journey-route', { component: JourneyRoute, composites: ['item'] });
registerTemplate('choo-journey-route', {
  design: { title: 'default', structure: { type: 'choo-journey-route' }, items: [{ type: 'simple' }] },
});

export const name = 'choo-journey-route';
