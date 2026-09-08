"""Individually referenced station equipment, authored in Unity metre coordinates.

Each device has its own silhouette. Public product photographs guide visible form;
the pedestal adaptations and simulation functions are not surveyed KORAIL devices.
References: foundation/art/object-references.json (equipment family).
The root driver owns export, material mapping, budgets and collision integration.
"""
import math

import bpy
import bmesh


def _mesh(c, name, vertices, faces, material, bevel=0):
    data = bpy.data.meshes.new(name)
    data.from_pydata([c.v(p) for p in vertices], [], [tuple(reversed(face)) for face in faces])
    data.update()
    bm = bmesh.new()
    bm.from_mesh(data)
    bmesh.ops.recalc_face_normals(bm, faces=list(bm.faces))
    bm.to_mesh(data)
    bm.free()
    obj = bpy.data.objects.new(name, data)
    bpy.context.collection.objects.link(obj)
    if bevel:
        bpy.ops.object.select_all(action='DESELECT')
        obj.select_set(True)
        bpy.context.view_layer.objects.active = obj
        modifier = obj.modifiers.new('Folded edge radius', 'BEVEL')
        modifier.width = bevel
        modifier.segments = 2
        bpy.ops.object.modifier_apply(modifier=modifier.name)
    return c.finish(obj, name, material)


def _side_profile(c, name, width, yz, material, bevel=.004):
    """Extrude an actual cabinet side profile rather than stack box decorations."""
    n = len(yz)
    vertices = [(x, y, z) for x in (-width / 2, width / 2) for y, z in yz]
    faces = [tuple(range(n - 1, -1, -1)), tuple(range(n, 2 * n))]
    faces += [(i, (i + 1) % n, (i + 1) % n + n, i + n) for i in range(n)]
    return _mesh(c, name, vertices, faces, material, bevel)


def _plate_shape(c, name, xy, z, thickness, material):
    n = len(xy)
    vertices = [(x, y, zz) for zz in (z - thickness / 2, z + thickness / 2) for x, y in xy]
    faces = [tuple(range(n - 1, -1, -1)), tuple(range(n, 2 * n))]
    faces += [(i, (i + 1) % n, (i + 1) % n + n, i + n) for i in range(n)]
    return _mesh(c, name, vertices, faces, material)


def _arrow(c, name, center, length, angle=0, material='White'):
    # Distinct arrow geometry remains separate from the changeable sign backing.
    shape = [(-.5, -.11), (.12, -.11), (.12, -.30), (.5, 0),
             (.12, .30), (.12, .11), (-.5, .11)]
    co, si = math.cos(angle), math.sin(angle)
    xy = [(center[0] + length * (x * co - y * si),
           center[1] + length * (x * si + y * co)) for x, y in shape]
    return _plate_shape(c, name, xy, center[2], .003, material)


def _frame(c, name, center, width, height, border, depth, material):
    x, y, z = center
    for sign in (-1, 1):
        c.box(name, (x, y + sign * (height - border) / 2, z),
              (width, border, depth), material, min(.005, border / 4))
        c.box(name, (x + sign * (width - border) / 2, y, z),
              (border, height - 2 * border, depth), material, min(.005, border / 4))


def _fastener(c, name, p, radius=.004):
    x, y, z = p
    c.pipe(name, (x, y, z), (x, y, z - .003), radius, 'Stainless', 8)
    c.box(name + 'Slot', (x, y, z - .0034), (radius * 1.1, .001, .0005), 'Metal', 0)


def _synthetic_post(c, name, top_y, z=.03, radius=.026, base=.36):
    # Action-root devices use floor y=-1.1. These mounts are explicitly synthetic.
    c.pipe(name, (0, -1.07, z), (0, top_y, z), radius, 'Stainless', 24)
    c.pipe(name + 'Collar', (0, -1.067, z), (0, -1.015, z), radius * 1.5, 'Metal', 24)
    c.pipe(name + 'Foot', (0, -1.1, z), (0, -1.067, z), base / 2, 'Metal', 32)


def _lathe(c, name, radius_y, center, material, segments=32):
    # Profiles use positive radii; caps are ngons so pole triangles cannot degenerate.
    vertices = [(center[0] + radius * math.cos(2 * math.pi * i / segments),
                 center[1] + y,
                 center[2] + radius * math.sin(2 * math.pi * i / segments))
                for radius, y in radius_y for i in range(segments)]
    rings = len(radius_y)
    faces = [tuple(range(segments - 1, -1, -1))]
    for row in range(rings - 1):
        for i in range(segments):
            j = (i + 1) % segments
            faces.append((row * segments + i, row * segments + j,
                          (row + 1) * segments + j, (row + 1) * segments + i))
    faces.append(tuple((rings - 1) * segments + i for i in range(segments)))
    obj = _mesh(c, name, vertices, faces, material)
    for face in obj.data.polygons:
        face.use_smooth = len(face.vertices) == 4
    return obj


def _perforated_grille(c, center, width, height, columns=7, rows=11):
    """A punched sheet with octagonal holes and real inner walls, no texture input."""
    vertices, faces = [], []
    cell_w, cell_h, thickness = width / columns, height / rows, .003
    # Eight perimeter points correspond to a circular aperture's eight directions.
    outer = [(-.5, -.5), (0, -.5), (.5, -.5), (.5, 0),
             (.5, .5), (0, .5), (-.5, .5), (-.5, 0)]
    for row in range(rows):
        for column in range(columns):
            cx = center[0] - width / 2 + (column + .5) * cell_w
            cy = center[1] - height / 2 + (row + .5) * cell_h
            radius = min(cell_w, cell_h) * .26
            inner = [(radius * math.cos(math.radians(-135 + 45 * i)),
                      radius * math.sin(math.radians(-135 + 45 * i))) for i in range(8)]
            first = len(vertices)
            for z in (center[2] - thickness / 2, center[2] + thickness / 2):
                vertices += [(cx + x * cell_w, cy + y * cell_h, z) for x, y in outer]
                vertices += [(cx + x, cy + y, z) for x, y in inner]
            for i in range(8):
                j = (i + 1) % 8
                faces += [(first+i, first+j, first+8+j, first+8+i),
                          (first+16+i, first+24+i, first+24+j, first+16+j),
                          (first+8+i, first+8+j, first+24+j, first+24+i),
                          (first+i, first+16+i, first+16+j, first+j)]
    return _mesh(c, 'PerforatedSpeakerArea', vertices, faces, 'Metal')


def _situation_panel(c):
    c.start('SituationPanel')
    _synthetic_post(c, 'SyntheticVesaPedestal', -.065, z=.010, base=.38)
    c.box('ShallowRearHousing', (0, 0, -.105), (.494, .294, .055), 'Metal', .008)
    c.box('VesaMountBlock', (0, -.035, -.043), (.115, .105, .065), 'Metal', .004)
    _frame(c, 'AluminiumSideRail', (0, 0, -.137), .518, .314, .007, .060, 'Stainless')
    _frame(c, 'DisplayBezel', (0, 0, -.170), .508, .304, .019, .008, 'Metal')
    c.set_group('Status')
    c.box('InsetStatusGlass', (0, 0, -.171), (.472, .268, .004), 'Screen', .001)
    c.set_group('Static')
    for x in (-.248, .248):
        c.box('SideExpansionGroove', (x, 0, -.107), (.002, .253, .003), 'Rubber', 0)
    c.save('SituationPanel')


def _alarm(c):
    c.start('AlarmSimulator')
    _synthetic_post(c, 'SyntheticCallpointPost', -.11, z=-.024, radius=.019, base=.28)
    c.box('MountBackplate', (0, 0, -.043), (.185, .245, .026), 'Stainless', .006)
    c.box('RedWeatherproofBackbox', (0, 0, -.108), (.140, .170, .080), 'Red', .006)
    _frame(c, 'RaisedFaceFrame', (0, -.013, -.154), .132, .117, .018, .022, 'Red')
    c.box('RecessedPressureElement', (0, -.013, -.156), (.096, .078, .007), 'White', .001)
    # A simple bespoke pressed-area icon, not the manufacturer's logo or procedure.
    c.pipe('PressureTarget', (0, -.013, -.161), (0, -.013, -.163), .009, 'Metal', 16)
    _arrow(c, 'PressureArrow', (-.027, -.013, -.163), .024, 0, 'Metal')
    _arrow(c, 'PressureArrow', (.027, -.013, -.163), .024, math.pi, 'Metal')
    for x in (-.034, .034):
        c.pipe('TopConduitCaps', (x, .084, -.108), (x, .095, -.108), .023, 'White', 24)
    for x in (-.052, .052):
        _fastener(c, 'FaceFixing', (x, .060, -.151), .004)
    c.pipe('ResetKeyRecess', (0, -.070, -.151), (0, -.070, -.155), .006, 'Metal', 12)
    c.set_group('Status')
    c.box('CallpointIndicator', (-.048, .057, -.158), (.010, .008, .003), 'Screen', .001)
    c.set_group('Static')
    c.save('AlarmSimulator')


def _radio(c):
    c.start('RadioConsole')
    _synthetic_post(c, 'PagingDeskPedestal', -.172, z=.03, radius=.03, base=.46)
    _side_profile(c, 'WedgeHousing', .560,
                  [(-.180, -.172), (-.180, .172), (.102, .172), (-.096, -.172)], 'Metal', .006)
    first = len(c.parts())
    c.box('InclinedDeck', (0, 0, -.009), (.544, .296, .009), 'Metal', .004)
    _frame(c, 'RecessedScreen', (.069, 0, -.017), .298, .247, .010, .006, 'Rubber')
    c.set_group('Status')
    c.box('PagingDisplay', (.069, 0, -.018), (.277, .227, .003), 'Screen', .001)
    c.set_group('Static')
    for x in (-.104, .241):
        for i in range(6):
            y = .105 - i * .042
            c.box('TwoSixKeyBanks', (x, y, -.026), (.038, .029, .017), 'Rubber', .004)
            c.box('KeyIndicator', (x+.010, y+.006, -.035), (.008, .006, .002), 'Yellow', .001)
    c.box('SpeakerDarkCavity', (-.205, 0, -.003), (.109, .261, .012), 'Rubber', 0)
    _perforated_grille(c, (-.205, 0, -.017), .109, .261)
    c.pipe('RotaryControl', (-.206, -.087, -.020), (-.206, -.087, -.035), .022, 'Rubber', 24)
    c.pipe('RotaryControlInset', (-.206, -.087, -.035), (-.206, -.087, -.038), .017, 'Metal', 24)
    c.tilt(c.parts()[first:], 60, (0, 0, 0))
    # Separately inspected AXIS TC6901 accessory, dimensions adapted synthetically.
    c.pipe('GooseneckSocket', (-.214, .039, .102), (-.214, .078, .102), .016, 'Metal', 16)
    neck = [(-.214,.078,.102), (-.214,.132,.102), (-.214,.177,.089),
            (-.214,.213,.055), (-.214,.305,-.027)]
    c.curve('FlexibleGooseneck', neck, .006, 'Metal')
    for i in range(8):
        y = .083 + .005 * i
        c.pipe('GooseneckRib', (-.214, y, .102), (-.214, y+.002, .102), .0075, 'Rubber', 12)
    c.ellipsoid('MicrophoneWindscreen', (-.214,.321,-.040), (.038,.061,.043), 'Fabric')
    c.save('RadioConsole')


def _route_console(c):
    c.start('RouteConsole')
    c.box('RoundedBasePlate', (0, -1.073, .04), (.580, .054, .440), 'Metal', .010)
    _side_profile(c, 'FoldedCabinet', .400,
                  [(-1.046,-.100), (-1.046,.200), (-.350,.200), (-.075,.092),
                   (-.075,-.030), (-.350,-.168)], 'Metal', .005)
    c.box('ServiceSeam', (0, -.365, -.171), (.386, .009, .003), 'Rubber', 0)
    c.box('RearServiceDoor', (0, -.660, .204), (.324, .594, .009), 'Metal', .005)
    _fastener(c, 'ServiceLock', (.135,-.498,.211), .008)
    c.box('InclinedUpperNeck', (0, -.12, .013), (.185, .250, .098), 'Metal', .006)
    c.box('DisplayMountArm', (0,-.09,-.075), (.135,.12,.10), 'Metal', .005)
    first = len(c.parts())
    c.box('ScreenRearCase', (0, 0, -.108), (.583, .353, .044), 'Metal', .008)
    _frame(c, 'OverhangingMapDisplay', (0, 0, -.141), .594, .367, .024, .025, 'Metal')
    c.set_group('Status')
    c.box('RouteMapGlass', (0, 0, -.144), (.547, .320, .004), 'Screen', .001)
    c.set_group('Static')
    # Broad schematic strokes are authored game graphics, not copied map textures.
    c.box('MapDiagramRoute', (-.03, -.03, -.147), (.290, .008, .0015), 'White', 0)
    for x, y in [(-.18, .045), (-.075,-.030), (.075,-.030), (.175,.075)]:
        c.pipe('MapDiagramNode', (x,y,-.148), (x,y,-.150), .011, 'White', 16)
    c.tilt(c.parts()[first:], 18, (0,0,-.115))
    c.save('RouteConsole')


def _register(c):
    c.start('AssemblyRegister')
    # Manufacturer allows portrait orientation; selected main photo shows landscape.
    c.pipe('WeightedDiscBase', (0,-1.100,.05), (0,-1.068,.05), .200, 'White', 48)
    c.pipe('SlenderPost', (0,-1.068,.05), (0,-.075,.05), .021, 'White', 32)
    c.pipe('PostFootCollar', (0,-1.067,.05), (0,-1.024,.05), .032, 'White', 24)
    c.pipe('TiltHeadJoint', (-.031,-.075,.027), (.031,-.075,.027), .029, 'Metal', 24)
    c.curve('TabletRearSupport', [(0,-.135,.05),(0,-.065,.05),(0,-.022,-.078)], .022, 'White')
    first = len(c.parts())
    c.box('RoundedTabletEnclosure', (0,0,-.105), (.240,.330,.040), 'White', .010)
    _frame(c, 'TabletFaceRim', (0,0,-.133), .235,.325,.027,.017,'White')
    c.set_group('Status')
    c.box('PortraitStatusScreen', (0,0,-.139), (.183,.269,.004), 'Screen', .001)
    c.set_group('Static')
    c.pipe('EnclosureLock', (.121,-.054,-.110), (.124,-.054,-.110), .008, 'Metal', 12)
    c.tilt(c.parts()[first:], 18, (0,0,-.115))
    c.save('AssemblyRegister')


def _direction_sign(c):
    c.start('DirectionSign')
    _synthetic_post(c, 'RoundPost', -.15, z=.022, radius=.021, base=.36)
    c.pipe('PostMountCollar', (0,-.23,.022), (0,-.16,.022), .036, 'Metal', 24)
    c.box('RearFrameBracket',(0,-.16,-.013),(.058,.045,.14),'Metal',.004)
    c.box('SignRearPlate', (0,0,-.090), (.344,.474,.017), 'Metal', .004)
    _frame(c,'PortraitFrame',(0,0,-.107),.360,.490,.019,.033,'Metal')
    c.set_group('Status')
    c.box('InsetArrowPlate',(0,0,-.109),(.321,.451,.006),'Green',.001)
    c.set_group('Static')
    _arrow(c,'WhiteDirectionArrow',(0,.018,-.115),.245)
    c.box('SignLabelBand',(0,-.145,-.114),(.255,.004,.002),'White',0)
    c.save('DirectionSign')


def _gate(c):
    c.start('AccessGate')
    c.box('RoundedGateCabinet',(-.069,-.545,.040),(.326,1.080,.490),'Stainless',.022)
    # Reader end rises above the cabinet and is sloped like the inspected gate lid.
    lid=_side_profile(c,'SlopedReaderLid',.334,
                      [(-.038,-.215),(-.038,.282),(.016,.282),(.016,-.148),(-.002,-.215)],'Metal',.006)
    lid.location=c.v((-.069,0,0))
    c.box('CabinetServiceSeam',(-.235,-.60,.040),(.0015,.650,.344),'Rubber',0)
    c.box('ReaderLockSlot',(-.154,-.090,-.209),(.012,.051,.003),'Metal',.001)
    c.set_group('Status')
    c.box('StatusReaderWindow',(-.055,-.009,-.216),(.124,.048,.004),'Screen',.001)
    c.set_group('Static')
    c.pipe('PivotHingeRail',(.10,-.470,0),(.10,.265,0),.024,'Stainless',24)
    for y in (-.40,.19):
        c.pipe('HingeCollar',(.10,y-.015,0),(.10,y+.015,0),.032,'Metal',24)
    c.set_group('MovingPart')
    c.box('TransparentSwingLeaf',(.40,-.120,0),(.600,.700,.016),'ClearGlass',.003)
    c.pipe('SwingTopRail',(.10,.235,0),(.70,.235,0),.018,'Stainless',16)
    c.box('LeafHingeClamp',(.128,-.110,0),(.050,.610,.038),'Metal',.004)
    c.set_group('Static')
    c.save('AccessGate')


def _hazard(c):
    c.start('HazardIndicator')
    # Ground-rooted prop. The nominal 100 mm device is separate from its stand.
    c.box('SyntheticBeaconFoot',(0,.04,0),(.260,.080,.260),'Metal',.010)
    c.pipe('SyntheticBeaconPost',(0,.080,0),(0,.655,0),.022,'Stainless',24)
    c.pipe('BeaconMountPlate',(0,.654,0),(0,.669,0),.075,'Metal',32)
    for i in range(3):
        angle=math.radians(30+120*i)
        x,z=.036*math.cos(angle),.036*math.sin(angle)
        c.pipe('ThreeFixingFeet',(x,.661,z),(x,.678,z),.004,'Stainless',12)
    _lathe(c,'MountGasketRing',[(.050,.677),(.052,.680),(.052,.687),(.050,.690)],(0,0,0),'Rubber')
    _lathe(c,'LowerLensBand',[(.049,.690),(.050,.705),(.047,.726)],(0,0,0),'Metal')
    _lathe(c,'InternalReflector',[(.033,.721),(.021,.742),(.039,.774),(.015,.790)],(0,0,0),'Stainless')
    c.pipe('LightSource',(0,.731,0),(0,.786,0),.010,'Orange',16)
    c.set_group('Static')
    _lathe(c,'DomedBeaconLens',[(.050,.708),(.050,.751),(.047,.786),(.037,.810),(.018,.824),(.002,.827)],
           (0,0,0),'ClearGlass',48)
    c.set_group('Beacon')
    c.pipe('PulsingOpticalCore',(0,.745,0),(0,.785,0),.025,'Orange',24)
    c.set_group('Static')
    c.save('HazardIndicator')


def _assembly_symbol(c, center, size):
    x,y,z=center
    # Bespoke relief of the photographed three-person / four-inward-arrow motif.
    for dx,dy,scale in [(-.18,-.025,.86),(.18,-.025,.86),(0,.095,.84)]:
        xx,yy=x+dx*size,y+dy*size
        c.pipe('WhiteAssemblySymbol',(xx,yy+.135*size,z),(xx,yy+.135*size,z-.002),.055*size*scale,'White',16)
        body=[(xx-.055*size,yy+.055*size),(xx+.055*size,yy+.055*size),
              (xx+.055*size,yy-.16*size),(xx+.017*size,yy-.16*size),
              (xx+.017*size,yy-.045*size),(xx-.017*size,yy-.045*size),
              (xx-.017*size,yy-.16*size),(xx-.055*size,yy-.16*size)]
        _plate_shape(c,'WhiteAssemblySymbol',body,z-.002,.002,'White')
    for sx,sy in [(-1,-1),(-1,1),(1,-1),(1,1)]:
        _arrow(c,'FourInwardArrows',(x+sx*.33*size,y+sy*.28*size,z-.004),.25*size,
               math.atan2(-sy,-sx))


def _rally(c):
    c.start('RallyPoint')
    # Keep the original ground root: Unity applies the existing -1.1 m offset.
    c.pipe('PortablePostAndFoot',(0,.005,.020),(0,.037,.020),.160,'Metal',32)
    c.pipe('PortablePostAndFoot',(0,.037,.020),(0,1.02,.020),.021,'Stainless',24)
    c.pipe('PostSleeve',(0,.41,.020),(0,.47,.020),.025,'Metal',24)
    c.box('PlateBacking',(0,1.10,-.040),(.360,.480,.009),'White',.002)
    c.set_group('Status')
    c.box('GreenPlate',(0,1.10,-.046),(.346,.466,.004),'Green',.001)
    c.set_group('Static')
    _assembly_symbol(c,(0,1.135,-.051),.34)
    c.box('LowerLabelBand',(0,.964,-.051),(.327,.003,.002),'White',0)
    for y in (1.00,1.21):
        c.box('RearPostChannel',(0,y,-.015),(.075,.025,.045),'Metal',.003)
    c.save('RallyPoint')


def _assembly_sign(c):
    c.start('AssemblySign')
    # Center-rooted overhead two-bay adaptation of a thin assembly-point plate.
    c.box('PlateBacking',(0,0,.008),(1.700,.450,.028),'White',.004)
    c.set_group('Status')
    c.box('GreenPlate',(0,0,-.010),(1.670,.420,.007),'Green',.001)
    c.set_group('Static')
    _assembly_symbol(c,(-.54,.025,-.016),.43)
    c.box('LowerLabelBand',(-.54,-.140,-.016),(.43,.003,.002),'White',0)
    c.box('SignBayDivider',(-.275,0,-.017),(.005,.383,.003),'White',0)
    _arrow(c,'WhiteDirectionArrow',(.54,0,-.017),.43,math.pi/2)
    # Space between symbol and arrow is reserved for the runtime's legible label.
    for x in (-.58,.58):
        c.box('RearChannelAndOverheadBrackets',(x,0,.040),(.035,.29,.035),'Stainless',.003)
        c.pipe('RearChannelAndOverheadBrackets',(x,.145,.040),(x,.36,.040),.011,'Metal',16)
    c.save('AssemblySign')


def _departure_board(c):
    c.start('DepartureBoard')
    # Fit the existing synthetic upper-envelope contract exactly.
    c.box('RearEnclosure',(0,0,.046),(3.16,.96,.046),'Metal',.007)
    _frame(c,'OuterDisplayFrame',(0,0,0),3.2,1.0,.080,.140,'Metal')
    c.box('RecessedDisplayPlane',(0,0,-.052),(3.035,.835,.012),'Screen',.001)
    c.box('HeaderAndRowSeparators',(0,.260,-.060),(2.96,.007,.002),'White',0)
    for i in range(6):
        c.box('HeaderAndRowSeparators',(0,.18-i*.098,-.060),(2.96,.003,.002),'Green',0)
    for x in (-1.03,-.29,.59,1.17):
        c.box('ColumnGuide',(x,-.047,-.060),(.003,.581,.002),'Metal',0)
    for x in (-1.1,1.1):
        c.box('RearStandoffRails',(x,0,.061),(.055,.82,.028),'Stainless',.003)
    c.save('DepartureBoard')


def _temporary_barrier(c):
    """Three opaque hoarding modules inside the existing 6 x 1.8 x .32 box.

    Altrad/Heras product form is the reference; the smaller feet, panel dimensions
    and straight three-panel arrangement are synthetic visual adaptations. This
    is not an engineered support system or a change to the incident collider.
    """
    c.start('TemporaryBarrier')
    for center in (-2.0, 0.0, 2.0):
        # A continuous folded sheet, with thickness and capped edges, not stripes
        # painted onto a giant box. Eight trapezoidal corrugations per module.
        profile = []
        for rib in range(8):
            start = -1.0 + rib * .25
            for dx, z in [(0,-.035),(.030,-.035),(.050,-.069),
                          (.100,-.069),(.120,-.035),(.250,-.035)]:
                point = (center + start + dx, z)
                if not profile or point != profile[-1]:
                    profile.append(point)
        n = len(profile)
        vertices = [(x,y,z+offset) for offset in (0,.006)
                    for y in (-.795,.851) for x,z in profile]
        faces = []
        for i in range(n-1):
            j = i+1
            faces += [(i,j,n+j,n+i), (2*n+i,3*n+i,3*n+j,2*n+j),
                      (i,2*n+i,2*n+j,j), (n+i,n+j,3*n+j,3*n+i)]
        faces += [(0,n,3*n,2*n), (n-1,3*n-1,4*n-1,2*n-1)]
        _mesh(c,'CorrugatedOpaqueSheets',vertices,faces,'White')
        # Rails meet the panels at each edge; silhouettes remain within +/-3 m.
        for y in (-.822,.873):
            c.box('PerimeterRails',(center,y,-.029),(2.0,.054,.064),'Stainless',0)
        c.box('GroundSkirt',(center,-.864,-.025),(2.0,.072,.014),'White',0)
        for side in (-1,1):
            post_x = center + side*.974
            _lathe(c,'PerimeterPosts',[(.021,-.836),(.021,.886)],
                   (post_x,0,-.020),'Stainless',12)
            c.box('PostEndCaps',(post_x,.893,-.020),(.044,.014,.044),'Yellow',0)
            # Feet are biased inward at the end posts and never protrude beyond
            # x=+/-3, y=+/-0.9 or z=+/-0.16 of the preserved collider envelope.
            foot_x = center + side*.906
            outline=[(-.094,-.900),(.094,-.900),(.094,-.843),(.075,-.823),
                     (-.075,-.823),(-.094,-.843)]
            _plate_shape(c,'RubberFeet',[(foot_x+x,y) for x,y in outline],0,.320,'Rubber')
            for z in (-.155,.155):
                c.box('YellowFootCaps',(foot_x,-.852,z),(.122,.041,.010),'Yellow',0)
    # Couplers connect pairs of posts at the seams; no new moving-part group.
    for seam in (-1.0,1.0):
        for y in (-.570,.580):
            c.box('PanelJointCouplers',(seam,y,-.087),(.142,.053,.024),'Stainless',0)
            _lathe(c,'CouplerPins',[(.012,y-.035),(.012,y+.035)],
                   (seam,0,-.101),'Stainless',12)
            c.box('CouplerBoltFace',(seam,y,-.115),(.019,.019,.006),'Rubber',0)
    c.save('TemporaryBarrier')


def build(c):
    for builder in (_situation_panel, _alarm, _radio, _route_console, _register,
                    _direction_sign, _gate, _hazard, _rally, _assembly_sign,
                    _departure_board, _temporary_barrier):
        builder(c)
