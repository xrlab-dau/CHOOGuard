"""Public-reference architectural form study, built by the shared Blender driver.

R02/R03: Busan upper concourse columns, tubular roof, clerestory and tactile strip.
R04/R06: wall finish and square recessed lighting. Manufacturer details are category
proxies, not identification of installed Busan products. See the reference audit.

All dimensions below are authored metres. No surveyed geometry, image textures,
downloads, scene mutation, export or rendering is performed by this module itself.
WallModule/FloorModule intentionally remain single unit meshes for Unity mesh-copy
compatibility. Detail belongs in the separate WallPanel/RoofPanel/TactileTile assets.
"""
from math import cos, pi, sin


ASSET_IDS = (
    "Pillar", "CeilingLight", "RecessedLight", "WallModule", "FloorModule",
    "DoorFrame", "RoofTruss", "ClerestoryBay", "NavigationArrow", "AssemblyRing",
    "WallPanel", "RoofPanel", "TactileTile", "Purlin", "CeilingPanel",
)

# Named construction families before the driver merges material batches.
# The two unit adapters are deliberately the one-part exception, not pretend kits.
COMPONENTS = {
    "Pillar": ("WhiteShaft", "CladdingSeam", "BaseCladding", "BaseGasket", "UpperCollar"),
    "CeilingLight": ("RoundHousing", "LensRim", "Diffuser", "HangingStem", "MountingCap"),
    "RecessedLight": ("PanelHousing", "Diffuser", "PerimeterTrim", "RearPan", "MountingTab"),
    "WallModule": ("UnitPanel",),
    "FloorModule": ("UnitTile",),
    "DoorFrame": ("JambExtrusion", "ProfileRebate", "ContinuousSeal", "Header", "FlushFastener"),
    "RoofTruss": ("TubularChord", "DiagonalWeb", "DepthTie", "EndBearingPlate", "JointFastener"),
    "ClerestoryBay": ("HorizontalFrame", "Mullion", "Transom", "VerticalGlazingSeal", "ClearGlassPane"),
    "NavigationArrow": ("ArrowShaft", "LeftArrowHead", "RightArrowHead"),
    "AssemblyRing": ("NorthEastArc", "NorthWestArc", "SouthWestArc", "SouthEastArc"),
    "WallPanel": ("BackingPanel", "FrontFace", "RevealChannel", "RearClip"),
    "RoofPanel": ("RoofSheet", "ParallelRib", "EdgeSeam", "UndersideSupport"),
    "TactileTile": ("TileBase", "ParallelGuidanceBar", "LongEdgeJoint", "EndEdgeJoint"),
    "Purlin": ("TopFlange", "BottomFlange", "Web", "EndSeam"),
    "CeilingPanel": ("CeilingTile", "GridFlange", "GridWeb", "PerimeterFinish"),
}


def build(c):
    """Build fifteen assets through root-owned c.start/save and geometry helpers.

    Unity-local coordinates: Y up, panel fronts toward -Z. RoofPanel/TactileTile
    lie in X/Z with +Y normals; WallPanel lies in X/Y and faces -Z.
    The driver owns material creation, triangle counting, export and collection.
    """
    def begin(name):
        c.start(name)
        c.set_group("Static")

    begin("Pillar")
    c.pipe("WhiteShaft", (0, -1.6, 0), (0, 1.6, 0), .325, "White", 32)
    c.pipe("BaseCladding", (0, -1.586, 0), (0, -1.395, 0), .341, "Stainless", 32)
    c.pipe("BaseGasket", (0, -1.6, 0), (0, -1.586, 0), .340, "Rubber", 24)
    c.pipe("UpperCollar", (0, 1.435, 0), (0, 1.565, 0), .343, "White", 32)
    # Narrow metal seam, not an unsupported black mid-column band.
    c.box("CladdingSeam", (0, -.01, .325), (.003, 2.70, .002), "Stainless", 0)
    c.save("Pillar")

    # Small round fixtures are visible beneath the upper roof in R02/R03.
    # Exact housing, stem length and photometry are not established by the photos.
    begin("CeilingLight")
    c.pipe("RoundHousing", (0, -.036, 0), (0, .042, 0), .20, "White", 32)
    c.pipe("LensRim", (0, -.055, 0), (0, -.035, 0), .198, "Stainless", 32)
    c.pipe("Diffuser", (0, -.060, 0), (0, -.054, 0), .174, "Diffuser", 32)
    c.pipe("HangingStem", (0, .040, 0), (0, .195, 0), .017, "Stainless", 12)
    c.pipe("MountingCap", (0, .188, 0), (0, .215, 0), .058, "White", 20)
    c.save("CeilingLight")

    # Square flush panel form: station R04/R06 plus inspected Signify panel photo.
    begin("RecessedLight")
    c.box("PanelHousing", (0, .004, 0), (.600, .044, .600), "White", .003)
    c.box("Diffuser", (0, -.022, 0), (.553, .009, .553), "Diffuser", .001)
    for x in (-.286, .286):
        c.box("PerimeterTrim", (x, -.020, 0), (.020, .013, .590), "White", .002)
    for z in (-.286, .286):
        c.box("PerimeterTrim", (0, -.020, z), (.552, .013, .020), "White", .002)
    c.box("RearPan", (0, .030, 0), (.545, .012, .545), "Stainless", .003)
    for x in (-.266, .266):
        for z in (-.18, .18):
            c.box("MountingTab", (x, .040, z), (.035, .018, .075), "Stainless", .002)
    c.save("RecessedLight")

    # Bare unit adapters: changing their origin, bounds or part count breaks
    # Environment mesh-copy consumers. They do not supply semantic family detail.
    begin("WallModule")
    c.box("UnitPanel", (0, 0, 0), (1, 1, 1), "Wall", 0)
    c.save("WallModule")
    begin("FloorModule")
    tile = c.box("UnitTile", (0, 0, 0), (1, 1, 1), "Stone", 0)
    c.unit_face_uv(tile)
    c.save("FloorModule")

    begin("DoorFrame")
    # A manufactured framed-opening proxy; it is not an installed product claim.
    for side in (-1, 1):
        x = side * 1.5
        c.box("JambExtrusion", (x, 1.50, 0), (.13, 3.0, .32), "Stainless", .007)
        c.box("ProfileRebate", (x - side * .027, 1.50, -.154), (.035, 2.86, .028), "Stainless", .003)
        c.box("ContinuousSeal", (x - side * .040, 1.50, -.172), (.008, 2.80, .009), "Rubber", .001)
        c.box("JambShoe", (x, .045, -.002), (.141, .09, .327), "Stainless", .004)
        for y in (.20, 1.50, 2.78):
            c.pipe("FlushFastener", (x + side * .035, y, -.164),
                   (x + side * .035, y, -.172), .009, "Stainless", 8)
    c.box("Header", (0, 2.955, 0), (3.13, .150, .32), "Stainless", .007)
    c.box("HeaderRebate", (0, 2.915, -.160), (2.86, .026, .018), "Stainless", .002)
    c.box("HeaderSeal", (0, 2.900, -.172), (2.85, .008, .009), "Rubber", .001)
    c.save("DoorFrame")

    begin("RoofTruss")
    # Existing synthetic envelope: X[-7.5,7.5], Y[0,1.1], Z[-.1,.1].
    for y in (.08, 1.02):
        c.pipe("TubularChord", (-7.42, y, 0), (7.42, y, 0), .08, "White", 24)
    cells = 8
    for cell in range(cells):
        x0 = -7.35 + cell * (14.7 / cells)
        x1 = x0 + 14.7 / cells
        y0, y1 = (.155, .945) if cell % 2 == 0 else (.945, .155)
        for z in (-.048, .048):
            c.pipe("DiagonalWeb", (x0, y0, z), (x1, y1, -z), .026, "White", 12)
        c.pipe("VerticalWeb", (x0, .16, 0), (x0, .94, 0), .028, "White", 12)
        c.pipe("DepthTie", (x0, y0, -.062), (x0, y0, .062), .021, "White", 12)
        # Plates and flush hardware are generic connection detail; exact station
        # connection specifications are not visible in the public photographs.
        for z in (-.090, .090):
            c.box("JointGusset", (x0, y0, z), (.125, .145, .010), "Stainless", .001)
        for dx in (-.035, .035):
            c.pipe("JointFastener", (x0 + dx, y0, -.096),
                   (x0 + dx, y0, -.099), .009, "Stainless", 8)
    for x in (-7.43, 7.43):
        c.box("EndBearingPlate", (x, .55, 0), (.14, 1.1, .190), "Stainless", .003)
        for y in (.18, .92):
            c.pipe("BearingFastener", (x, y, -.096), (x, y, -.099), .015, "Stainless", 8)
    c.save("RoofTruss")

    begin("ClerestoryBay")
    for y in (-.47, .47):
        c.box("HorizontalFrame", (0, y, -.015), (1, .06, .09), "White", .004)
    for x in (-.47, .47):
        c.box("VerticalFrame", (x, 0, -.015), (.06, .88, .09), "White", .004)
    c.box("ClearGlassPane", (0, 0, .015), (.878, .878, .008), "ClearGlass", 0)
    for x in (-.435, .435):
        c.box("VerticalGlazingSeal", (x, 0, -.047), (.010, .88, .006), "Rubber", .001)
    for y in (-.435, .435):
        c.box("HorizontalGlazingSeal", (0, y, -.047), (.88, .010, .006), "Rubber", .001)
    c.box("Mullion", (0, 0, -.0125), (.033, .88, .085), "White", .003)
    c.box("Transom", (0, .12, -.0125), (.88, .024, .085), "White", .003)
    c.save("ClerestoryBay")

    begin("NavigationArrow")
    # Consistent virtual glyph, not a surveyed station marking or ISO claim.
    c.box("ArrowShaft", (0, 0, -.08), (.064, .016, .40), "Green", .002)
    c.pipe("LeftArrowHead", (0, 0, .23), (-.16, 0, .02), .025, "Green", 12)
    c.pipe("RightArrowHead", (0, 0, .23), (.16, 0, .02), .025, "Green", 12)
    c.save("NavigationArrow")

    begin("AssemblyRing")
    for quadrant, name in enumerate(COMPONENTS["AssemblyRing"]):
        points = [(.48 * cos(quadrant * pi / 2 + i * pi / 32), 0,
                   .48 * sin(quadrant * pi / 2 + i * pi / 32)) for i in range(17)]
        c.curve(name, points, .02, "Green")
    c.save("AssemblyRing")

    begin("WallPanel")
    # Vertical one-metre overlay, total depth .05, front -Z. Rear clips are a
    # synthetic mounting proxy; the photograph only supports the front finish.
    c.box("BackingPanel", (0, 0, .018), (1, 1, .014), "Metal", .001)
    for i in range(64):
        x=-.5+(i+.5)/64
        c.box("FrontFace", (x, 0, -.010), (.013, .980, .024), "Wall", 0)
    for i in range(1,64):
        c.box("RevealChannel", (-.5+i/64, 0, -.004), (.002, .98, .004), "Metal", 0)
    for x in (-.455, .455):
        for y in (-.43, .43):
            c.box("RearClip", (x, y, .007), (.052, .09, .010), "Stainless", .001)
    c.save("WallPanel")

    begin("RoofPanel")
    # Horizontal 1x1 sheet, .06 total Y depth, positive-Y raised parallel ribs.
    c.box("RoofSheet", (0, -.015, 0), (1, .016, 1), "Ceiling", .001)
    for x in (-.4, -.2, 0, .2, .4):
        c.box("ParallelRib", (x, .007, 0), (.026, .028, .998), "Ceiling", .002)
    for x in (-.485, .485):
        c.box("EdgeSeam", (x, .0085, 0), (.03, .043, 1), "Ceiling", .002)
    for z in (-.43, .43):
        c.box("UndersideSupport", (0, -.026, z), (.99, .008, .032), "Stainless", .001)
    c.save("RoofPanel")

    begin("TactileTile")
    # Horizontal 1x1 tile, base Y=0, top Y=.014. These are synthetic dimensions;
    # accessibility compliance and the installed route are not established.
    c.box("TileBase", (0, .0045, 0), (1, .009, 1), "Yellow", .0007)
    for x in (-.32, -.16, 0, .16, .32):
        c.box("ParallelGuidanceBar", (x, .0115, 0), (.058, .005, .84), "Yellow", .001)
    for x in (-.4985, .4985):
        c.box("LongEdgeJoint", (x, .004, 0), (.003, .008, 1), "Stone", 0)
    for z in (-.4985, .4985):
        c.box("EndEdgeJoint", (0, .004, z), (.994, .008, .003), "Stone", 0)
    c.save("TactileTile")

    begin("Purlin")
    # R02/R03 show slender secondary roof members. This I-like profile and
    # end-joint strips are authored fabrication detail, not a surveyed section.
    # Exact adapter envelope: X .055, Y .09, Z 1.0; longitudinal axis is +Z.
    c.box("TopFlange", (0, .041, 0), (.055, .008, 1), "White", .0003)
    c.box("BottomFlange", (0, -.041, 0), (.055, .008, 1), "White", .0003)
    c.box("Web", (0, 0, 0), (.006, .074, 1), "White", .0003)
    for z in (-.493, .493):
        for y in (-.0445, .0445):
            c.box("EndSeam", (0, y, z), (.054, .001, .004), "Stainless", 0)
        c.box("EndSeam", (.0035, 0, z), (.001, .072, .004), "Stainless", 0)
    c.save("Purlin")

    begin("CeilingPanel")
    # R06: a low, gridded ceiling with light-toned infill panels. These 36
    # panels are not emissive luminaires; RecessedLight remains a separate asset.
    # Authored 3.6 x 3.6 metre module in X/Z, .05 Y depth, underside toward -Y.
    # The photograph does not establish the actual pitch or hidden T-bar section.
    for row in range(6):
        for column in range(6):
            c.box("CeilingTile", (-1.5 + column * .6, -.003, -1.5 + row * .6),
                  (.584, .014, .584), "Wall", 0)
    for line in (-1.791, -1.2, -.6, 0, .6, 1.2, 1.791):
        c.box("GridFlange", (0, -.012, line), (3.6, .006, .018), "Stainless", 0)
        c.box("GridFlange", (line, -.012, 0), (.018, .006, 3.6), "Stainless", 0)
        c.box("GridWeb", (0, .007, line), (3.568, .032, .004), "Stainless", 0)
        c.box("GridWeb", (line, .007, 0), (.004, .032, 3.568), "Stainless", 0)
    for edge in (-1.794, 1.794):
        c.box("PerimeterFinish", (edge, 0, 0), (.012, .05, 3.6), "Metal", 0)
        c.box("PerimeterFinish", (0, 0, edge), (3.6, .05, .012), "Metal", 0)
    c.save("CeilingPanel")
