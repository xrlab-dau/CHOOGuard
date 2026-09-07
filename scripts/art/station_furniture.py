"""Furniture and anonymous people, authored from the local per-object reference audit.

Only the driver exports. Public photos supply form observations; all authored meshes
are synthetic. No image import, boolean, subdivision, bake, or render is performed.
"""
import math

import bmesh
import bpy


FEATURE_COMPONENTS = {
    'Bench': ['TimberSeatSlat', 'StainlessPedestal', 'SeatDivider', 'FloorFootPad'],
    'InformationKiosk': ['UprightCabinet', 'RecessedDisplay', 'CardReader', 'TicketSlot', 'ServiceDoor'],
    'InformationIsland': ['CurvedCounterShell', 'CurvedCounterTop', 'PerforatedToeBand', 'CurvedAccentPanel'],
    'Luggage': ['HardshellFront', 'HardshellBack', 'PerimeterZipper', 'DoubleCasterWheel', 'TelescopicHandle'],
    'Glove': ['ContinuousGloveHand', 'KnittedSleeve', 'DorsalKnitPanel', 'CuffBinding'],
    'Evacuee': ['TailoredJacket', 'BlankHeadAndNeck', 'ContinuousTrouserLeg', 'ContinuousSleeve', 'AnatomicalHand'],
}


def _save(c, name):
    # Six independent assets below 7,500 triangles each keep this lane below 45k.
    triangles = sum(len(face.vertices) - 2 for obj in c.parts() for face in obj.data.polygons)
    if triangles >= 7500:
        raise RuntimeError('Furniture asset triangle budget exceeded: ' + name + ' = ' + str(triangles))
    c.save(name)


def _mesh(c, name, vertices, faces, material, smooth=True):
    """Build in Unity coordinates; preserve outward normals after mirrored parts."""
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata([c.v(point) for point in vertices], [], [tuple(reversed(face)) for face in faces])
    mesh.update()
    bm = bmesh.new()
    bm.from_mesh(mesh)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    if all(edge.is_manifold for edge in bm.edges) and bm.calc_volume(signed=True) < 0:
        bmesh.ops.reverse_faces(bm, faces=list(bm.faces))
    bm.to_mesh(mesh)
    bm.free()
    for polygon in mesh.polygons:
        polygon.use_smooth = smooth and len(polygon.vertices) == 4
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    return c.finish(obj, name, material)


def _loft(c, name, sections, material, segments=16, exponent=2.5):
    """Closed horizontal sections: (y, center_x, center_z, width, depth)."""
    vertices = []
    faces = []
    power = 2 / exponent
    for y, x, z, width, depth in sections:
        for i in range(segments):
            angle = 2 * math.pi * i / segments
            co, si = math.cos(angle), math.sin(angle)
            vertices.append((x + math.copysign(abs(co) ** power, co) * width / 2,
                             y, z + math.copysign(abs(si) ** power, si) * depth / 2))
    for row in range(len(sections) - 1):
        for i in range(segments):
            j = (i + 1) % segments
            faces.append((row * segments + i, row * segments + j,
                          (row + 1) * segments + j, (row + 1) * segments + i))
    faces.append(tuple(range(segments - 1, -1, -1)))
    faces.append(tuple((len(sections) - 1) * segments + i for i in range(segments)))
    return _mesh(c, name, vertices, faces, material)


def _arc_band(c, name, radius_outer, radius_inner, bottom, top, material,
              start=-.54, end=.54, center_z=1.30, steps=28, angles=None):
    vertices = []
    faces = []
    if angles is not None:
        steps = len(angles) - 1
    for i in range(steps + 1):
        angle = angles[i] if angles is not None else start + (end - start) * i / steps
        for radius, y in [(radius_outer, bottom), (radius_outer, top),
                          (radius_inner, top), (radius_inner, bottom)]:
            vertices.append((radius * math.sin(angle), y, center_z - radius * math.cos(angle)))
    for i in range(steps):
        for side in range(4):
            following = (side + 1) % 4
            faces.append((i * 4 + side, (i + 1) * 4 + side,
                          (i + 1) * 4 + following, i * 4 + following))
    faces.extend([(3, 2, 1, 0), tuple(steps * 4 + i for i in range(4))])
    obj = _mesh(c, name, vertices, faces, material)
    _radial_skin_normals(c, obj, center_z, radius_outer, radius_inner)
    return obj


def _radial_skin_normals(c, obj, center_z, radius_outer, radius_inner):
    """Cylindrical skins use radial normals; hole walls and slab edges stay flat.

    Explicit loop normals prevent thin aperture walls from rounding the broad skin
    shading. The object is authored at identity; c.v supplies the driver handedness.
    """
    mesh = obj.data
    center = c.v((0, 0, center_z))
    normals = [(0, 0, 0)] * len(mesh.loops)
    for face in mesh.polygons:
        radii = [math.hypot(mesh.vertices[i].co.x - center[0],
                            mesh.vertices[i].co.y - center[1]) for i in face.vertices]
        side = 1 if all(abs(r - radius_outer) < .00001 for r in radii) else (
            -1 if all(abs(r - radius_inner) < .00001 for r in radii) else 0)
        face.use_smooth = side != 0
        for loop in face.loop_indices:
            if side:
                point = mesh.vertices[mesh.loops[loop].vertex_index].co
                x, y = point.x - center[0], point.y - center[1]
                length = math.hypot(x, y)
                normals[loop] = (side * x / length, side * y / length, 0)
            else:
                normals[loop] = tuple(face.normal)
    mesh.normals_split_custom_set(normals)


def _toe_angles():
    """Shared grid edges for twenty slots and their solid intervening webs."""
    angles = []
    for col in range(20):
        left = -.54 + col * 1.08 / 20
        center = left + 1.08 / 40
        angles.extend((left, center - .010, center + .010))
    return angles + [.54]


def _perforated_arc(c):
    """A shared angular/height grid with forty omitted cells as real through-holes.

    Every skin quad has only two angles and two heights, so it is planar. The
    previous outer/inner trapezoids used four angles and produced twisted quads.
    Only boundary edges receive thickness walls; no coplanar cell interior walls.
    """
    angles = _toe_angles()
    heights = [.035, .0685, .0765, .110, .1435, .1515, .185]
    columns = len(angles)
    layer = columns * len(heights)
    vertices = []
    for radius in (1.622, 1.613):
        for y in heights:
            vertices.extend((radius * math.sin(angle), y, 1.30 - radius * math.cos(angle)) for angle in angles)
    skin = []
    boundary = {}
    for row in range(len(heights) - 1):
        for col in range(columns - 1):
            if row % 3 == 1 and col % 3 == 1:
                continue
            a = row * columns + col
            face = (a, a + 1, a + columns + 1, a + columns)
            skin.append(face)
            for i, start in enumerate(face):
                end = face[(i + 1) % 4]
                key = tuple(sorted((start, end)))
                if key in boundary:
                    del boundary[key]
                else:
                    boundary[key] = (start, end)
    faces = skin + [tuple(i + layer for i in reversed(face)) for face in skin]
    faces.extend((end, start, start + layer, end + layer) for start, end in boundary.values())
    obj = _mesh(c, 'PerforatedToeBand', vertices, faces, 'Metal')
    _radial_skin_normals(c, obj, 1.30, 1.622, 1.613)
    return obj


def _hand(c, name, material, offset=(0, 0, 0), scale=(1, 1, 1)):
    """A connected palm with four finger sockets and a six-edge thumb socket.

    Shared socket vertices form the webbing. Fingers taper through swept sections,
    rather than overlapping spheres. The thumb replaces two palm side faces.
    """
    vertices = []
    faces = []
    sections = [(-.050, .075, .052, .006), (-.012, .090, .055, .012),
                (.032, .106, .052, .018), (.071, .096, .044, .020)]
    for y, width, depth, z in sections:
        front = [(width * (i / 8 - .5), y, z + depth / 2 * (1 - .06 * (i / 4 - 1) ** 2))
                 for i in range(9)]
        back = [(width * (i / 8 - .5), y, z - depth / 2 * (1 - .06 * (i / 4 - 1) ** 2))
                for i in range(9)]
        vertices.extend(front + [(width / 2, y, z)] + list(reversed(back)) + [(-width / 2, y, z)])
    for row in range(len(sections) - 1):
        for i in range(20):
            if row == 1 and i in (18, 19):
                continue  # Shared side opening for the thumb root.
            j = (i + 1) % 20
            faces.append((row * 20 + i, row * 20 + j, (row + 1) * 20 + j, (row + 1) * 20 + i))
    faces.append(tuple(range(19, -1, -1)))
    top = 60
    side_midpoints = [top + 19]
    for boundary in range(1, 4):
        side_midpoints.append(len(vertices))
        vertices.append((-.048 + boundary * .024, .071, .020))
    side_midpoints.append(top + 9)
    outline = [(-.78, 1), (0, 1), (.78, 1), (1, 0), (.78, -1), (0, -1), (-.78, -1), (-1, 0)]
    # Index, middle, ring, little finger. Distinct lengths and modest natural flexion.
    for finger, tip in enumerate((.160, .176, .168, .145)):
        left = finger * 2
        ring = [top + left, top + left + 1, top + left + 2, side_midpoints[finger + 1],
                top + 18 - left - 2, top + 18 - left - 1, top + 18 - left, side_midpoints[finger]]
        center_x = -.036 + finger * .024
        width = (.020, .021, .020, .017)[finger]
        for fraction, fullness in ((.18, 1), (.52, .93), (.79, .82), (.94, .57), (1, .12)):
            y = .071 + (tip - .071) * fraction
            center_z = .020 + .012 * fraction ** 2
            next_ring = []
            for u, w in outline:
                next_ring.append(len(vertices))
                vertices.append((center_x + u * width * fullness / 2,
                                 y, center_z + w * .032 * fullness / 2))
            for i in range(8):
                j = (i + 1) % 8
                faces.append((ring[i], ring[j], next_ring[j], next_ring[i]))
            ring = next_ring
        faces.append(tuple(reversed(ring)))
    thumb = [20, 40, 59, 58, 38, 39]
    thumb_outline = [(-.8, .85), (.8, .85), (1, 0), (.8, -.85), (-.8, -.85), (-1, 0)]
    for x, y, z, ry, rz in [(-.060, .025, .020, .019, .022), (-.068, .040, .027, .017, .020),
                             (-.076, .056, .034, .014, .017), (-.081, .069, .036, .009, .012),
                             (-.083, .075, .036, .002, .003)]:
        next_ring = []
        for dy, dz in thumb_outline:
            next_ring.append(len(vertices))
            vertices.append((x, y + dy * ry, z + dz * rz))
        for i in range(6):
            j = (i + 1) % 6
            faces.append((thumb[i], thumb[j], next_ring[j], next_ring[i]))
        thumb = next_ring
    faces.append(tuple(reversed(thumb)))
    vertices = [tuple(offset[i] + point[i] * scale[i] for i in range(3)) for point in vertices]
    return _mesh(c, name, vertices, faces, material)


def _bench(c):
    c.start('Bench')
    for i in range(5):
        c.box('TimberSeatSlat', (0, .495, -.266 + i * .133), (2.38, .050, .124), 'Wood', .009)
    for z in (-.325, .325):
        c.box('TimberApron', (0, .470, z), (2.38, .080, .030), 'Wood', .008)
    for z in (-.19, .19):
        c.box('UnderSeatRail', (0, .425, z), (2.20, .050, .065), 'Stainless', .006)
    for x in (-.88, .88):
        c.box('StainlessPedestal', (x, .237, 0), (.135, .434, .315), 'Stainless', .010)
        c.box('PedestalFoot', (x, .031, 0), (.31, .035, .47), 'Stainless', .008)
        c.box('FloorFootPad', (x, .008, 0), (.28, .016, .44), 'Rubber', .004)
    for x in (-.397, .397):
        c.curve('SeatDivider', [(x, .52, -.245), (x, .690, -.245), (x, .715, -.210),
                                (x, .715, .190), (x, .690, .225), (x, .52, .225)], .016, 'Stainless')
    _save(c, 'Bench')


def _kiosk(c):
    c.start('InformationKiosk')
    c.box('FootPad', (0, .015, 0), (.71, .030, .58), 'Rubber', .006)
    c.box('Plinth', (0, .075, 0), (.74, .090, .60), 'Stainless', .018)
    c.box('UprightCabinet', (0, 1.015, 0), (.70, 1.79, .55), 'Roof', .026)
    c.box('HeaderPanel', (0, 2.0, 0), (.70, .20, .55), 'Blue', .015)
    c.box('BlueFacePanel', (0, 1.41, -.284), (.61, .88, .022), 'Blue', .016)
    c.box('DisplayBezel', (0, 1.49, -.305), (.55, .49, .029), 'Rubber', .020)
    c.box('RecessedDisplay', (0, 1.505, -.323), (.475, .385, .011), 'Screen', .008)
    c.frame((0, 1.49, -.303), .54, .47, 'Stainless')
    c.box('DisplayGlazing', (0, 1.505, -.331), (.471, .381, .002), 'ClearGlass', 0)
    for i in range(3):
        c.box('DisplayRow', (-.030, 1.61 - i * .10, -.334), (.33, .015, .002), 'White', 0)
    c.box('CardReader', (.16, 1.05, -.316), (.17, .09, .040), 'Rubber', .010)
    c.box('CardSlot', (.16, 1.054, -.339), (.12, .008, .007), 'Stainless', .002)
    c.box('TicketSlot', (-.07, .72, -.292), (.34, .021, .030), 'Rubber', .005)
    c.box('TicketTray', (-.07, .693, -.32), (.35, .025, .10), 'Stainless', .005)
    c.box('ServiceDoor', (0, .39, -.283), (.57, .46, .013), 'Stainless', .007)
    c.box('DoorLock', (.22, .43, -.294), (.018, .042, .009), 'Metal', .003)
    for i in range(5):
        c.box('SideVent', (.353, .45 + i * .07, .02), (.008, .012, .23), 'Rubber', .001)
    _save(c, 'InformationKiosk')


def _island(c):
    c.start('InformationIsland')
    # The three body bands meet edge-to-edge. Previously the shared rear radius
    # overlapped vertically by 15 mm, producing a ragged white/dark z-fight.
    _arc_band(c, 'InnerToeSupport', 1.602, 1.14, .018, .185, 'Rubber', angles=_toe_angles())
    _arc_band(c, 'ToeTransitionCollar', 1.622, 1.14, .185, .21, 'Metal', angles=_toe_angles())
    _arc_band(c, 'CurvedCounterShell', 1.614, 1.14, .21, 1.015, 'Roof', angles=_toe_angles())
    _perforated_arc(c)
    _arc_band(c, 'CurvedCounterTop', 1.65, 1.11, 1.015, 1.10, 'Stone')
    _arc_band(c, 'CounterLip', 1.621, 1.614, .985, 1.010, 'Stainless')
    _arc_band(c, 'CurvedAccentPanel', 1.619, 1.615, .21, .49, 'Metal', start=.12, end=.535)
    _save(c, 'InformationIsland')


def _luggage(c):
    c.start('Luggage')
    profile = [(.090, .315, .090), (.13, .38, .103), (.43, .38, .103),
               (.49, .35, .095), (.525, .29, .075)]
    for name, z in [('HardshellFront', -.046), ('HardshellBack', .046)]:
        _loft(c, name, [(y, 0, z, w, d) for y, w, d in profile], 'Blue', 20, 4)
    c.curve('PerimeterZipper', [(-.152, .10, 0), (-.185, .14, 0), (-.185, .43, 0),
                               (-.16, .50, 0), (-.10, .529, 0), (.10, .529, 0),
                               (.16, .50, 0), (.185, .43, 0), (.185, .14, 0),
                               (.152, .10, 0), (-.152, .10, 0)], .006, 'Rubber')
    for i in range(6):
        y = .16 + i * .049
        c.curve('ShellHorizontalRib', [(-.157, y + .01, -.079), (-.105, y, -.099),
                                      (.105, y, -.099), (.157, y + .01, -.079)], .003, 'Blue')
    for x in (-.151, .151):
        for z in (-.064, .064):
            c.box('CasterHousing', (x, .077, z), (.075, .033, .065), 'Rubber', .008)
            for offset in (-.023, .023):
                c.pipe('DoubleCasterWheel', (x + offset - .006, .035, z),
                       (x + offset + .006, .035, z), .035, 'Rubber', 20)
                c.pipe('WheelHub', (x + offset - .0065, .035, z),
                       (x + offset + .0065, .035, z), .015, 'Stainless', 12)
    for x in (-.082, .082):
        c.pipe('TelescopicHandle', (x, .48, .040), (x, .955, .040), .0065, 'Stainless', 12)
    c.box('PullGrip', (0, .964, .04), (.212, .027, .026), 'Rubber', .008)
    c.box('CarryGrip', (0, .544, 0), (.145, .020, .038), 'Rubber', .006)
    c.box('SideHandle', (.193, .31, 0), (.018, .15, .036), 'Rubber', .006)
    c.box('LockBody', (-.190, .36, 0), (.017, .09, .043), 'Metal', .004)
    _save(c, 'Luggage')


def _glove(c):
    c.start('Glove')
    _hand(c, 'ContinuousGloveHand', 'Rubber')
    _loft(c, 'KnittedSleeve', [(-.205, 0, 0, .11, .11), (-.145, 0, 0, .113, .113),
                              (-.07, 0, .003, .094, .088), (-.045, 0, .006, .080, .061)], 'Fabric', 16, 3)
    vertices = []
    faces = []
    for y, width, z in [(-.035, .059, -.020), (0, .076, -.016), (.032, .085, -.011), (.056, .071, -.007)]:
        vertices.extend([(width * (i / 4 - .5), y, z) for i in range(5)])
    for row in range(3):
        for col in range(4):
            a = row * 5 + col
            faces.append((a + 5, a + 6, a + 1, a))
    c.set_group('KnitPanel')
    _mesh(c, 'DorsalKnitPanel', vertices, faces, 'Fabric')
    c.set_group('Static')
    for y in (-.07, -.062, -.054):
        points = [(.043 * math.cos(i * 2 * math.pi / 24), y,
                   .006 + .034 * math.sin(i * 2 * math.pi / 24)) for i in range(25)]
        c.curve('CuffBinding', points, .0017, 'Rubber')
    stitch = [(.043 * math.cos(i * 2 * math.pi / 24), -.052,
               .006 + .034 * math.sin(i * 2 * math.pi / 24)) for i in range(25)]
    c.curve('CuffStitch', stitch, .0008, 'White')
    _save(c, 'Glove')


def _evacuee(c):
    c.start('Evacuee')
    c.set_group('Torso')
    _loft(c, 'TrouserWaist', [(.80, 0, 0, .29, .23), (.88, 0, 0, .35, .25),
                             (1.02, 0, 0, .33, .23)], 'Fabric', 16, 2.6)
    _loft(c, 'TailoredJacket', [(.925, 0, -.004, .38, .275), (1.02, 0, -.006, .36, .25),
                               (1.16, 0, -.003, .405, .27), (1.30, 0, 0, .47, .29),
                               (1.375, 0, 0, .47, .25), (1.43, 0, .008, .22, .16)], 'Blue', 20, 2.8)
    c.box('JacketClosure', (0, 1.17, -.149), (.011, .44, .009), 'Rubber', .003)
    for x in (-.12, .12):
        c.box('PocketSeam', (x, 1.035, -.135), (.10, .008, .008), 'Fabric', .002)
    c.set_group('Head')
    _loft(c, 'BlankHeadAndNeck', [(1.405, 0, .009, .10, .11), (1.48, 0, .008, .105, .115),
                                 (1.515, 0, -.010, .145, .170), (1.59, 0, -.002, .211, .217),
                                 (1.665, 0, .005, .224, .23), (1.728, 0, .008, .174, .185),
                                 (1.764, 0, .01, .085, .10), (1.77, 0, .01, .014, .017)], 'Wall', 20, 2)
    for side, sign in [('Left', -1), ('Right', 1)]:
        c.set_group(side + 'Leg')
        x = sign * .13
        _loft(c, 'ContinuousTrouserLeg', [(.14, x, .010, .135, .15), (.29, x, .011, .142, .16),
                                        (.47, x, -.006, .155, .172), (.66, x, .003, .183, .207),
                                        (.84, x, .006, .194, .218), (.935, x, .010, .19, .215)], 'Fabric', 16, 2.7)
        _loft(c, 'WalkingShoe', [(.018, x, -.045, .17, .30), (.042, x, -.045, .17, .30),
                                (.085, x, -.034, .155, .263), (.14, x, .012, .122, .16)], 'Rubber', 16, 3.5)
        c.box('ShoeSole', (x, .011, -.045), (.17, .022, .30), 'Rubber', .006)
        c.set_group(side + 'Arm')
        _loft(c, 'ContinuousSleeve', [(.78, sign * .292, 0, .105, .115),
                                     (.88, sign * .296, 0, .116, .132),
                                     (1.00, sign * .292, .005, .125, .143),
                                     (1.18, sign * .282, .007, .145, .170),
                                     (1.30, sign * .263, 0, .174, .20),
                                     (1.36, sign * .242, .008, .116, .135)], 'Blue', 16, 2.5)
        _hand(c, 'AnatomicalHand', 'Wall', offset=(sign * .292, .760, -.006),
              scale=(sign * .67, -.60, .70))
    c.set_group('Static')
    _save(c, 'Evacuee')


def build(c):
    """Called by the sole export driver; names/pivots remain the runtime contract."""
    _bench(c)
    _kiosk(c)
    _island(c)
    _luggage(c)
    _glove(c)
    _evacuee(c)
