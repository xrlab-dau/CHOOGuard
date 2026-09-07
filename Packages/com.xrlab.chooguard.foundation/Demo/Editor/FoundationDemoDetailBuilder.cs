using System.Linq;
using UnityEngine;

namespace ChooGuard.Foundation.Demo.Editor
{
    // Detail geometry is decoration only. Physics bodies stay in FoundationDemoSceneBuilder.
    public static class FoundationDemoDetailBuilder
    {
        public static void Build(Transform root, Material[] m)
        {
            foreach (var target in root.GetComponentsInChildren<DemoInteractable>()) DetailTarget(target, m);
            var props = root.Find("Props");
            foreach (var side in new[] { -1, 1 })
            {
                Bench(props.Find("Bench_" + side), m);
                foreach (var z in new[] { -2, 6 }) Pillar(props.Find("Pillar_" + side + "_" + z), m);
            }
            Kiosk(props.Find("InformationKiosk"), m);
            RoomSign(props.Find("AssemblyDirectionSign"), m);
            Hazard(props.Find("SyntheticHazardIndicator"), m);
            Interior(root.Find("Environment"), m);
            Hands(root.GetComponentInChildren<DemoPlayerController>(), m);
        }

        private static void DetailTarget(DemoInteractable target, Material[] m)
        {
            var d = Group("Details", target.transform);
            var id = target.AnchorId;
            var code = id.Substring(id.Length - 2);
            var status = Box("StatusIndicator", d, new Vector3(.29f, .24f, -.235f), new Vector3(.07f, .04f, .025f), m[7]);
            target.ConfigureFeedback(new[] { status.GetComponent<Renderer>() });
            foreach (var x in new[] { -.34f, .34f })
                foreach (var y in new[] { -.27f, .27f }) Screw(d, new Vector3(x, y, -.181f), m[8]);
            Box("BottomMount", d, new Vector3(0, -.39f, .03f), new Vector3(.54f, .05f, .32f), m[2]);
            Text("UnitNumber", d, code, new Vector3(-.30f, .25f, -.225f), .07f, Color.white);
            if (id == "anchor-01")
            {
                Frame(d, new Vector3(0, .015f, -.19f), .66f, .46f, m[2]);
                Box("RecessedScreen", d, new Vector3(0, .04f, -.219f), new Vector3(.51f, .30f, .015f), m[7]);
                Box("MapHorizontal", d, new Vector3(0, .04f, -.231f), new Vector3(.41f, .018f, .005f), m[8]);
                Box("MapVertical", d, new Vector3(-.10f, .04f, -.232f), new Vector3(.018f, .22f, .005f), m[8]);
                Box("MapZone", d, new Vector3(.11f, .11f, -.233f), new Vector3(.09f, .04f, .005f), m[6]);
                for (var i = -1; i <= 1; i++) Box("SoftKey_" + i, d, new Vector3(i * .18f, -.22f, -.207f), new Vector3(.10f, .055f, .035f), m[8]);
                Vents(d, new Vector3(.407f, 0, 0), m[2], true);
            }
            else if (id == "anchor-02")
            {
                Disc("ButtonBezel", d, new Vector3(0, 0, -.185f), .38f, .018f, m[8]);
                Box("IdentificationPlate", d, new Vector3(0, -.22f, -.18f), new Vector3(.36f, .06f, .02f), m[8]);
                Text("ButtonLabel", d, "ALERT", new Vector3(-.13f, -.19f, -.2f), .043f, Color.black);
                Box("TopLensFrame", d, new Vector3(.29f, .24f, -.20f), new Vector3(.18f, .075f, .04f), m[2]);
                Vents(d, new Vector3(-.407f, -.03f, 0), m[2], true);
                Box("SideCap", d, new Vector3(.405f, 0, .04f), new Vector3(.035f, .54f, .16f), m[4]);
            }
            else if (id == "anchor-03")
            {
                Box("DisplayBezel", d, new Vector3(.14f, .13f, -.175f), new Vector3(.35f, .20f, .025f), m[2]);
                Box("Display", d, new Vector3(.14f, .13f, -.195f), new Vector3(.27f, .12f, .015f), m[7]);
                for (var row = 0; row < 3; row++)
                    for (var col = 0; col < 2; col++) Box("Key_" + row + "_" + col, d,
                        new Vector3(.08f + col * .12f, -.04f - row * .085f, -.18f), new Vector3(.075f, .052f, .035f), m[8]);
                Box("Earpiece", d, new Vector3(-.18f, .18f, -.265f), new Vector3(.2f, .13f, .10f), m[2]);
                Box("Mouthpiece", d, new Vector3(-.18f, -.18f, -.265f), new Vector3(.2f, .13f, .10f), m[2]);
                for (var i = 0; i < 3; i++) Box("SpeakerSlot_" + i, d, new Vector3(-.18f, .145f + .032f * i, -.323f), new Vector3(.13f, .01f, .008f), m[8]);
                Pipe("CableDown", d, new Vector3(-.18f, -.24f, -.23f), new Vector3(-.18f, -.48f, -.22f), .012f, m[2]);
                Pipe("CableAcross", d, new Vector3(-.18f, -.48f, -.22f), new Vector3(.04f, -.48f, -.12f), .012f, m[2]);
                Pipe("CableReturn", d, new Vector3(.04f, -.48f, -.12f), new Vector3(.04f, -.31f, -.12f), .012f, m[2]);
                Disc("VolumeKnob", d, new Vector3(.32f, -.22f, -.19f), .055f, .035f, m[2]);
            }
            else if (id == "anchor-04")
            {
                Frame(d, new Vector3(0, 0, -.17f), .76f, .63f, m[2]);
                Box("BackBracket", d, new Vector3(0, -.20f, .20f), new Vector3(.50f, .04f, .12f), m[2]);
                Box("LeftStandoff", d, new Vector3(-.23f, -.3f, .08f), new Vector3(.07f, .17f, .10f), m[2]);
                Box("RightStandoff", d, new Vector3(.23f, -.3f, .08f), new Vector3(.07f, .17f, .10f), m[2]);
                Text("RouteLabel", d, "ROUTE", new Vector3(-.13f, -.18f, -.19f), .05f, Color.black);
            }
            else
            {
                Box("ReaderFrame", d, new Vector3(0, .08f, -.18f), new Vector3(.4f, .35f, .035f), m[2]);
                Box("ReaderFace", d, new Vector3(0, .08f, -.206f), new Vector3(.29f, .25f, .018f), m[7]);
                Frame(d, new Vector3(0, 0, -.17f), .78f, .66f, m[2]);
                Disc("HingeHub", d, new Vector3(-.09f, .04f, -.19f), .12f, .04f, m[8]);
                var arm = target.transform.Find("MovingPart");
                for (var i = 0; i < 4; i++) Box("ArmStripe_" + i, arm,
                    new Vector3(-.08f + .18f * i, .062f, 0), new Vector3(.075f, .006f, .15f), m[2]);
                Box("MaintenanceDoor", d, new Vector3(0, -.55f, .05f), new Vector3(.15f, .42f, .24f), m[2]);
            }
        }

        private static void Bench(Transform original, Material[] m)
        {
            var d = Group("Details", original);
            for (var i = -1; i <= 1; i++)
            {
                Box("SeatPad_" + i, d, new Vector3(.77f * i, .66f, -.02f), new Vector3(.68f, .08f, .60f), m[2]);
                Box("BackPad_" + i, d, new Vector3(.77f * i, 1, .18f), new Vector3(.68f, .47f, .04f), m[2]);
            }
            foreach (var x in new[] { -1.14f, 0f, 1.14f })
            {
                Pipe("ArmSupport_" + x, d, new Vector3(x, .61f, -.13f), new Vector3(x, .90f, -.13f), .024f, m[8]);
                Box("ArmRest_" + x, d, new Vector3(x, .91f, -.01f), new Vector3(.075f, .05f, .53f), m[8]);
            }
            Box("LegBrace", d, new Vector3(0, .30f, .03f), new Vector3(1.9f, .06f, .08f), m[2]);
        }

        private static void Pillar(Transform original, Material[] m)
        {
            var d = Group("Details", original);
            // Child scale compensates for the original cylinder's nonuniform scale.
            d.localScale = new Vector3(1 / original.localScale.x, 1 / original.localScale.y, 1 / original.localScale.z);
            Cylinder("FootRing", d, new Vector3(0, -1.43f, 0), .70f, .28f, m[2]);
            Cylinder("Cap", d, new Vector3(0, 1.50f, 0), .69f, .12f, m[8]);
            Cylinder("NumberBand", d, new Vector3(0, .08f, 0), .66f, .14f, m[3]);
        }

        private static void Kiosk(Transform original, Material[] m)
        {
            var d = Group("Details", original);
            d.localRotation = Quaternion.Euler(0, 180, 0);
            Frame(d, new Vector3(0, 1.55f, -.12f), 1.13f, .80f, m[2]);
            Box("Glass", d, new Vector3(0, 1.57f, -.15f), new Vector3(.94f, .61f, .02f), m[7]);
            for (var i = 0; i < 4; i++) Box("DisplayRow_" + i, d, new Vector3(-.12f, 1.76f - .13f * i, -.166f), new Vector3(.55f, .018f, .005f), m[8]);
            Box("KeyboardShelf", d, new Vector3(0, 1.05f, -.245f), new Vector3(1.0f, .06f, .20f), m[2]);
            Box("ServiceDoor", d, new Vector3(0, .57f, -.361f), new Vector3(1.15f, .83f, .025f), m[3]);
            Box("DoorHandle", d, new Vector3(.41f, .67f, -.39f), new Vector3(.035f, .17f, .03f), m[8]);
            Vents(d, new Vector3(.715f, .60f, 0), m[8], true);
            Text("Header", d, "INFORMATION", new Vector3(-.49f, 2.17f, -.413f), .11f, Color.white);
        }

        private static void Hazard(Transform original, Material[] m)
        {
            var d = Group("Details", original);
            Cylinder("BeaconBase", d, new Vector3(0, .35f, 0), .72f, .12f, m[2]);
            foreach (var x in new[] { -.32f, .32f })
                Pipe("Guard_" + x, d, new Vector3(x, .36f, -.20f), new Vector3(x, 1.35f, -.20f), .023f, m[8]);
            Pipe("GuardTop", d, new Vector3(-.32f, 1.35f, -.20f), new Vector3(.32f, 1.35f, -.20f), .023f, m[8]);
            for (var i = -2; i <= 2; i++) Box("CautionStripe_" + i, d, new Vector3(.18f * i, .303f, -.28f), new Vector3(.08f, .006f, .36f), m[5]);
        }

        private static void Interior(Transform environment, Material[] m)
        {
            var d = Group("InteriorDetails", environment);
            Box("CeilingConcourse", d, new Vector3(0, 3.30f, .5f), new Vector3(16, .12f, 15), m[1]);
            Box("CeilingCorridor", d, new Vector3(0, 3.30f, 11), new Vector3(6, .12f, 6), m[1]);
            Box("CeilingAssembly", d, new Vector3(0, 3.30f, 18), new Vector3(12, .12f, 8), m[1]);
            foreach (Transform wall in environment)
            {
                if (!wall.name.Contains("Wall") && !wall.name.Contains("Wing")) continue;
                var at = wall.localPosition;
                var size = wall.localScale;
                var alongX = size.x > size.z;
                var length = alongX ? size.x : size.z;
                if (alongX) at.z += (wall.name.Contains("AssemblyWing") || at.z < 0 ? 1 : -1) * (size.z / 2 + .012f);
                else at.x -= Mathf.Sign(at.x) * (size.x / 2 + .012f);
                at.y = .17f;
                Box(wall.name+"_Skirting", d, at, alongX ? new Vector3(length,.30f,.02f) : new Vector3(.02f,.30f,length), m[2]);
                at.y = 1.4f;
                Box(wall.name+"_Band", d, at, alongX ? new Vector3(length,.12f,.02f) : new Vector3(.02f,.12f,length), m[3]);
                for (var offset = -length / 2 + 1.5f; offset < length / 2; offset += 3)
                {
                    var point = at + (alongX ? Vector3.right : Vector3.forward) * offset;
                    point.y = 1.6f;
                    Box(wall.name+"_Joint_"+offset, d, point, new Vector3(.023f,3,.023f), m[2]);
                }
            }
            for (var x = -6; x <= 6; x += 2) Box("FloorJointX_" + x, d, new Vector3(x, .004f, .5f), new Vector3(.015f, .004f, 15), m[2]);
            for (var z = -5; z <= 21; z += 2)
            {
                var width = z < 8 ? 16 : z < 14 ? 6 : 12;
                Box("FloorJointZ_" + z, d, new Vector3(0, .004f, z), new Vector3(width, .004f, .015f), m[2]);
            }
            foreach (var z in new[] { -4f, 1f, 6f, 11f, 16f, 20f })
            {
                var fixture = Group("CeilingLight_" + z, d);
                fixture.localPosition = new Vector3(0, 3.16f, z);
                Box("Housing", fixture, Vector3.zero, new Vector3(2.6f, .11f, .32f), m[2]);
                Box("Diffuser", fixture, new Vector3(0, -.062f, 0), new Vector3(2.35f, .016f, .23f), m[8]);
                Box("Beam", fixture, new Vector3(0, .06f, 0), new Vector3(z < 8 ? 16 : z < 14 ? 6 : 12, .09f, .12f), m[8]);
            }
            var hall = Group("HallSign", d);
            hall.localPosition = new Vector3(0, 2.68f, 7.76f);
            Box("Board", hall, Vector3.zero, new Vector3(2.8f, .42f, .08f), m[2]);
            Text("Title", hall, "TRAINING HALL", new Vector3(-1.16f, .13f, -.052f), .18f, Color.white);
        }

        private static void RoomSign(Transform sign, Material[] m)
        {
            var d = Group("Details", sign);
            Frame(d, new Vector3(0,0,-.06f), 1.68f,.44f,m[2]);
            Text("Title",d,"ASSEMBLY",new Vector3(-.70f,.12f,-.09f),.12f,Color.white);
            sign.Find("Arrow").localPosition = new Vector3(.57f,0,-.065f);
        }

        private static void Hands(DemoPlayerController player, Material[] m)
        {
            var root = Group("FirstPersonHands", player.ViewCamera.transform);
            var visual = Group("Visual", root);
            visual.localPosition = new Vector3(.25f, -.29f, .48f);
            Box("Sleeve", visual, new Vector3(0, -.12f, 0), new Vector3(.115f, .20f, .12f), m[3]);
            Box("GlovePalm", visual, new Vector3(0, .025f, .02f), new Vector3(.11f, .14f, .095f), m[2]);
            Box("WristBand", visual, new Vector3(0, -.042f, 0), new Vector3(.12f, .023f, .13f), m[5]);
            for (var i = 0; i < 4; i++)
            {
                Box("Finger_" + i, visual, new Vector3(-.04f + i * .027f, .105f, .052f), new Vector3(.024f, .064f, .05f), m[2]);
                Box("Knuckle_" + i, visual, new Vector3(-.04f + i * .027f, .07f, .075f), new Vector3(.025f, .03f, .018f), m[8]);
            }
            var thumb = Box("Thumb", visual, new Vector3(-.065f, .02f, .045f), new Vector3(.035f, .073f, .05f), m[2]);
            thumb.transform.localRotation = Quaternion.Euler(0, 0, -25);
            root.gameObject.AddComponent<DemoHands>().Configure(player, visual);
        }

        private static Transform Group(string name, Transform parent)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            return t;
        }
        private static GameObject Shape(string name, Transform parent, PrimitiveType type, Vector3 position, Vector3 scale, Material m)
        {
            var g = GameObject.CreatePrimitive(type); g.name = name;
            g.transform.SetParent(parent, false); g.transform.localPosition = position; g.transform.localScale = scale;
            g.GetComponent<Renderer>().sharedMaterial = m;
            Object.DestroyImmediate(g.GetComponent<Collider>());
            return g;
        }
        private static GameObject Box(string name, Transform p, Vector3 at, Vector3 size, Material m)
        { return Shape(name, p, PrimitiveType.Cube, at, size, m); }
        private static void Cylinder(string name, Transform p, Vector3 at, float diameter, float height, Material m)
        { Shape(name, p, PrimitiveType.Cylinder, at, new Vector3(diameter, height / 2, diameter), m); }
        private static void Disc(string name, Transform p, Vector3 at, float diameter, float depth, Material m)
        {
            var g = Shape(name, p, PrimitiveType.Cylinder, at, new Vector3(diameter, depth / 2, diameter), m);
            g.transform.localRotation = Quaternion.Euler(90, 0, 0);
        }
        private static void Pipe(string name, Transform p, Vector3 a, Vector3 b, float radius, Material m)
        {
            var g = Shape(name, p, PrimitiveType.Cylinder, (a+b)/2, new Vector3(radius*2, (b-a).magnitude/2, radius*2), m);
            g.transform.localRotation = Quaternion.FromToRotation(Vector3.up, b-a);
        }
        private static void Frame(Transform p, Vector3 at, float width, float height, Material m)
        {
            Box("FrameTop", p, at+Vector3.up*height/2, new Vector3(width, .035f, .04f), m);
            Box("FrameBottom", p, at-Vector3.up*height/2, new Vector3(width, .035f, .04f), m);
            Box("FrameLeft", p, at-Vector3.right*width/2, new Vector3(.035f, height, .04f), m);
            Box("FrameRight", p, at+Vector3.right*width/2, new Vector3(.035f, height, .04f), m);
        }
        private static void Screw(Transform p, Vector3 at, Material m)
        { Disc("Fastener", p, at, .034f, .012f, m); }
        private static void Vents(Transform p, Vector3 at, Material m, bool side)
        {
            for (var i=0;i<4;i++) Box("Vent_"+i, p, at+Vector3.up*(i*.07f-.10f), side ? new Vector3(.012f,.018f,.18f) : new Vector3(.18f,.018f,.012f), m);
        }
        private static void Text(string name, Transform p, string text, Vector3 at, float size, Color color)
        {
            var t=Group(name,p);t.localPosition=at;
            var label=t.gameObject.AddComponent<TextMesh>();label.text=text;label.characterSize=size*10f/64f;label.fontSize=64;label.color=color;
            label.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.GetComponent<MeshRenderer>().sharedMaterial=label.font.material;
        }
    }
}
