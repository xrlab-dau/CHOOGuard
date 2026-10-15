/** @jsxRuntime classic */
/** @jsx h */
// Custom items use AntV's installed sequence-circular and relation-dagre-flow structures.
// No stock renderer geometry is replaced by a hand-drawn card diagram.
import * as K from './antv-kit.jsx';
const { A, h, C, Group, L, D, disc, icon, rrect } = K;

const CircularLabel = ({ datum, indexes, ...rest }) => (
  <Group {...rest} width={260} height={104}>
    {L(indexes, 0, 0, 260, 40, datum.label, { size: 32, weight: 'bold', color: C.paper, align: rest.positionH === 'flipped' ? 'right' : 'left', lh: 1.2 })}
    {D(indexes, 0, 44, 260, 60, datum.desc, { size: 28, color: C.mist, align: rest.positionH === 'flipped' ? 'right' : 'left', lh: 1.25 })}
  </Group>
);
A.registerItem('choo-circular-label', { component: CircularLabel, composites: [] });
A.registerTemplate('choo-state-circular', {
  design: { title: 'default', structure: { type: 'sequence-circular', outerRadius: 176, innerRadius: 116, itemDistance: 304, gapAngle: 6, iconRadius: 34, iconBgRadius: 40, iconSize: 42 }, items: [{ type: 'choo-circular-label' }] },
});

const ObservationGlyph = ({ datum, indexes, themeColors, ...rest }) => {
  const knowledge = datum.id === 'knowledge';
  const source = ['danger', 'broadcast', 'position'].includes(datum.id);
  return (
    <Group {...rest} width={248} height={152}>
      {knowledge ? rrect(20, 0, 208, 100, 50, { fill: C.accent }) : disc(124, 44, 84, { fill: source ? C.mist : C.paper, stroke: C.ink, sw: source ? 2 : 4 })}
      {icon(indexes, 124, knowledge ? 50 : 44, 48, C.ink)}
      {L(indexes, 0, 104, 248, 48, datum.label, { size: 28, weight: knowledge ? 'bold' : 'normal', align: 'center', color: C.ink, lh: 1.2 })}
    </Group>
  );
};
A.registerItem('choo-observation-glyph', { component: ObservationGlyph, composites: [] });
A.registerTemplate('choo-observation-dagre', {
  design: { title: 'default', structure: { type: 'relation-dagre-flow', rankdir: 'LR', nodesep: 24, ranksep: 60, edgesep: 16, padding: 16, edgeWidth: 3, edgeCornerRadius: 8, edgeRouting: 'orth', edgeColorMode: 'solid', showArrow: true, arrowType: 'triangle' }, items: [{ type: 'choo-observation-glyph' }] },
});
