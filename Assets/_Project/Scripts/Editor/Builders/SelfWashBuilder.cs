using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using CarWash.Core;
using CarWash.Vehicles;
using UnityEditor;
using UnityEngine;

namespace CarWash.Editor
{
    /// <summary>
    /// M4.9 G9: the lot as a Korean self-service car wash (layout: docs/layout/G9_layout_proposal.png). The three bays keep
    /// their places (x −8 / 0 / 8, so the car routes barely move) but share ONE big canopy on pillars (the per-bay roofs are
    /// gone, the bay walls became 2.2 m dividers); every bay gets a pay terminal with mode buttons, a ceiling boom arm, a
    /// foam-brush holder, a hose-reel drum and a big drain grating. A separate drying / vacuum area sits south-east under its
    /// own canopy (the exit ramp moved east to x = 33.5 to make room). Entrance price board + info board, painted arrows,
    /// '서행 / 입구 / 출구' markings, a no-entry sign at the ramp, and the small stuff (coin changer, vending machines,
    /// bench, parasol, CCTV, extinguishers, water tank, drums, pipes, AC units, planters, a bicycle…).
    /// Every prop is a prefab built from primitives under Prefabs/Props/SelfWash (decor only: no NetworkObject; colliders
    /// only on the big ones) and listed with its size in docs/ART_REQUESTS.md so real models can replace them 1:1.
    /// </summary>
    public static class SelfWashBuilder
    {
        public const string PropDir = "Assets/_Project/Prefabs/Props/SelfWash";
        public const float CanopyY = 4.3f, RampX = 33.5f;
        /// <summary>Drying / vacuum area (x, z) — kept free of cars and random set dressing.</summary>
        public static readonly Rect VacuumZone = new Rect(20f, -11.6f, 10.5f, 4.4f);
        /// <summary>Footprints of everything placed here (EnvDressingBuilder keeps its random dressing away).</summary>
        public static readonly List<(Vector3 c, float r)> Footprints = new List<(Vector3, float)>();

        static Material M(string n, Color c, float s = 0.3f) => GrayboxMaterials.Get("SW_" + n, c, s);
        static GameObject B(Transform t, string n, Vector3 c, Vector3 s, Material m) => GrayboxMaterials.Box(t, n, c, s, m, collider: false);
        static GameObject C(Transform t, string n, Vector3 c, Vector3 s, Material m, Vector3 euler = default)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.DestroyImmediate(g.GetComponent<Collider>());
            g.name = n; g.transform.SetParent(t, false); g.transform.localPosition = c; g.transform.localScale = s; g.transform.localEulerAngles = euler;
            g.GetComponent<Renderer>().sharedMaterial = m;
            return g;
        }
        static GameObject S(Transform t, string n, Vector3 c, Vector3 s, Material m)
        {
            var g = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.DestroyImmediate(g.GetComponent<Collider>());
            g.name = n; g.transform.SetParent(t, false); g.transform.localPosition = c; g.transform.localScale = s;
            g.GetComponent<Renderer>().sharedMaterial = m;
            return g;
        }
        /// <summary>World text on a board (faces local −Z, depth-tested like the billboards).</summary>
        /// <summary>M4.10: board text = a baked, mipmapped text decal projected onto the board face (+Z of <paramref name="euler"/> into
        /// the surface; the old TextMesh shimmered and its depth offset let it show through meshes). <paramref name="size"/> is
        /// the old TextMesh character size (line height ≈ size × 6.4 m).</summary>
        static void Txt(Transform t, string s, Vector3 c, float size, Color col, Vector3 euler = default, float depth = 0.1f)
            => TextDecals.Board(t, s, c, euler, size * 6.4f, col, depth);

        /// <summary>A ground decal (EnvDressing texture kind) projected down, centred on the prop, <paramref name="size"/> = x × z.</summary>
        static void Decal(Transform t, string kind, Vector2 size)
        {
            var go = new GameObject("Decal_" + kind); go.transform.SetParent(t, false);
            go.transform.localPosition = new Vector3(0, 0.3f, 0); go.transform.localEulerAngles = new Vector3(90f, 0f, 0f);
            var d = go.AddComponent<UnityEngine.Rendering.Universal.DecalProjector>();
            d.material = EnvDressingBuilder.DecalMat(kind);
            d.size = new Vector3(size.x, size.y, 0.7f); d.pivot = new Vector3(0, 0, 0.35f);
            d.drawDistance = 90f; d.fadeScale = 0.9f;
        }

        // ------------------------------------------------------------------ prop catalog
        public class PropDef
        {
            public string id, nameKo, note; public Vector3 size; public bool solid; public System.Action<Transform> build;
            public PropDef(string id, string nameKo, Vector3 size, bool solid, string note, System.Action<Transform> build) { this.id = id; this.nameKo = nameKo; this.size = size; this.solid = solid; this.note = note; this.build = build; }
        }

        static Material Steel => M("Steel", new Color(0.62f, 0.64f, 0.67f), 0.55f);
        static Material Dark => M("Dark", new Color(0.12f, 0.13f, 0.15f), 0.4f);
        static Material White => M("White", new Color(0.92f, 0.93f, 0.94f), 0.3f);
        static Material Blue => M("Blue", new Color(0.12f, 0.36f, 0.78f), 0.35f);
        static Material Yellow => M("Yellow", new Color(0.98f, 0.78f, 0.12f), 0.35f);
        static Material Red => M("Red", new Color(0.85f, 0.14f, 0.12f), 0.4f);
        static Material Green => M("Green", new Color(0.2f, 0.6f, 0.3f), 0.25f);
        static Material Rubber => M("Rubber", new Color(0.08f, 0.08f, 0.09f), 0.2f);
        static Material Glass => M("Screen", new Color(0.15f, 0.3f, 0.45f), 0.9f);

        public static readonly PropDef[] Catalog =
        {
            new PropDef("PayTerminal", "결제기 + 모드 버튼", new Vector3(0.5f, 1.5f, 0.35f), true, "front = -Z; screen, coin/card slot, 4 mode buttons (물·거품·왁스·헹굼)", t =>
            {
                B(t, "Body", new Vector3(0, 0.75f, 0), new Vector3(0.5f, 1.5f, 0.35f), Blue);
                B(t, "Screen", new Vector3(0, 1.22f, -0.18f), new Vector3(0.36f, 0.22f, 0.02f), Glass);
                B(t, "Slot", new Vector3(0.12f, 0.98f, -0.18f), new Vector3(0.12f, 0.03f, 0.02f), Dark);
                var cols = new[] { Blue, White, Yellow, Green };
                for (int i = 0; i < 4; i++) B(t, "Mode" + i, new Vector3(-0.15f + i * 0.1f, 0.82f, -0.18f), new Vector3(0.07f, 0.07f, 0.03f), cols[i]);
                B(t, "Top", new Vector3(0, 1.52f, -0.04f), new Vector3(0.56f, 0.05f, 0.45f), White);
            }),
            new PropDef("BoomArm", "회전 붐 암 (천장)", new Vector3(0.3f, 0.9f, 2.8f), false, "hangs from the canopy; arm along +Z; the arm slowly sways (BoomSway)", t =>
            {
                C(t, "Pivot", new Vector3(0, -0.35f, 0), new Vector3(0.18f, 0.35f, 0.18f), Steel);
                var arm = new GameObject("Arm").transform; arm.SetParent(t, false); arm.localPosition = new Vector3(0, -0.72f, 0);
                B(arm, "Beam", new Vector3(0, 0, 1.3f), new Vector3(0.12f, 0.12f, 2.6f), Steel);
                C(arm, "HoseDrop", new Vector3(0, -0.45f, 2.55f), new Vector3(0.05f, 0.45f, 0.05f), Rubber);
                arm.gameObject.AddComponent<Visual.BoomSway>();
            }),
            new PropDef("FoamBrushHolder", "폼브러시 거치대", new Vector3(0.4f, 1.3f, 0.25f), false, "wall-mounted; front = -Z", t =>
            {
                B(t, "Plate", new Vector3(0, 1.0f, 0.1f), new Vector3(0.4f, 0.5f, 0.04f), Steel);
                B(t, "Hook", new Vector3(0, 1.15f, -0.02f), new Vector3(0.18f, 0.05f, 0.2f), Dark);
                C(t, "Handle", new Vector3(0, 0.8f, -0.06f), new Vector3(0.05f, 0.4f, 0.05f), Blue);
                B(t, "Brush", new Vector3(0, 0.36f, -0.06f), new Vector3(0.22f, 0.14f, 0.1f), Yellow);
            }),
            new PropDef("HoseReelDrum", "호스 릴", new Vector3(0.6f, 0.6f, 0.3f), false, "wall-mounted drum; axis along Z", t =>
            {
                C(t, "Drum", Vector3.zero, new Vector3(0.6f, 0.12f, 0.6f), Yellow, new Vector3(90, 0, 0));
                C(t, "Hose", new Vector3(0, 0, -0.02f), new Vector3(0.48f, 0.1f, 0.48f), Rubber, new Vector3(90, 0, 0));
                C(t, "Hub", new Vector3(0, 0, -0.1f), new Vector3(0.12f, 0.06f, 0.12f), Steel, new Vector3(90, 0, 0));
            }),
            new PropDef("DrainGrate", "배수구 (바닥 중앙)", new Vector3(1.6f, 0.03f, 0.6f), false, "flush with the floor", t =>
            {
                Decal(t, "drain", new Vector2(1.6f, 0.6f)); // M4.10: a decal (the 2 cm boxes z-fought the bay floor)
            }),
            new PropDef("VacuumPillar", "진공 청소기 기둥", new Vector3(0.5f, 1.9f, 0.5f), true, "hose + nozzle on the side; coin box", t =>
            {
                C(t, "Pillar", new Vector3(0, 0.85f, 0), new Vector3(0.42f, 0.85f, 0.42f), Yellow);
                B(t, "Head", new Vector3(0, 1.8f, 0), new Vector3(0.5f, 0.22f, 0.5f), Blue);
                B(t, "CoinBox", new Vector3(0, 1.25f, -0.22f), new Vector3(0.2f, 0.25f, 0.06f), Dark);
                for (int i = 0; i < 4; i++) C(t, "Hose" + i, new Vector3(0.3f + i * 0.05f, 1.55f - i * 0.38f, 0), new Vector3(0.07f, 0.2f, 0.07f), Rubber, new Vector3(0, 0, 25f - i * 12f));
                B(t, "Nozzle", new Vector3(0.48f, 0.2f, 0), new Vector3(0.06f, 0.2f, 0.12f), Dark);
            }),
            new PropDef("MatRack", "매트 거치대", new Vector3(1.6f, 1.4f, 0.5f), true, "3 car mats hanging on a bar", t =>
            {
                B(t, "PostL", new Vector3(-0.78f, 0.7f, 0), new Vector3(0.06f, 1.4f, 0.06f), Steel);
                B(t, "PostR", new Vector3(0.78f, 0.7f, 0), new Vector3(0.06f, 1.4f, 0.06f), Steel);
                B(t, "Bar", new Vector3(0, 1.36f, 0), new Vector3(1.6f, 0.05f, 0.05f), Steel);
                B(t, "FootL", new Vector3(-0.78f, 0.02f, 0), new Vector3(0.1f, 0.04f, 0.5f), Steel);
                B(t, "FootR", new Vector3(0.78f, 0.02f, 0), new Vector3(0.1f, 0.04f, 0.5f), Steel);
                for (int i = 0; i < 3; i++) B(t, "Mat" + i, new Vector3(-0.48f + i * 0.48f, 0.98f, 0.02f * i), new Vector3(0.42f, 0.7f, 0.02f), Rubber);
            }),
            new PropDef("MatWasher", "매트 세척기", new Vector3(0.9f, 1.1f, 0.7f), true, "box with a mat slot + rollers; front = -Z", t =>
            {
                B(t, "Body", new Vector3(0, 0.55f, 0), new Vector3(0.9f, 1.1f, 0.7f), White);
                B(t, "Slot", new Vector3(0, 0.95f, -0.2f), new Vector3(0.7f, 0.04f, 0.32f), Dark);
                C(t, "Roller", new Vector3(0, 0.88f, -0.36f), new Vector3(0.08f, 0.36f, 0.08f), Blue, new Vector3(0, 0, 90));
                B(t, "Panel", new Vector3(0.3f, 0.6f, -0.36f), new Vector3(0.2f, 0.25f, 0.02f), Glass);
            }),
            new PropDef("AirGun", "에어건", new Vector3(0.4f, 1.5f, 0.4f), false, "post + coiled hose + gun", t =>
            {
                B(t, "Post", new Vector3(0, 0.75f, 0), new Vector3(0.12f, 1.5f, 0.12f), Steel);
                B(t, "Box", new Vector3(0, 1.3f, -0.1f), new Vector3(0.28f, 0.3f, 0.12f), Blue);
                for (int i = 0; i < 4; i++) C(t, "Coil" + i, new Vector3(0, 0.95f - i * 0.1f, -0.2f), new Vector3(0.22f, 0.012f, 0.22f), Yellow);
                B(t, "Gun", new Vector3(0.18f, 1.12f, -0.2f), new Vector3(0.05f, 0.14f, 0.12f), Dark);
            }),
            new PropDef("RecycleBins", "분리수거함 (3칸)", new Vector3(1.5f, 1.0f, 0.5f), true, "캔 / 플라스틱 / 일반", t =>
            {
                var cols = new[] { Blue, Yellow, Green };
                for (int i = 0; i < 3; i++)
                {
                    B(t, "Bin" + i, new Vector3(-0.5f + i * 0.5f, 0.45f, 0), new Vector3(0.46f, 0.9f, 0.46f), cols[i]);
                    B(t, "Lid" + i, new Vector3(-0.5f + i * 0.5f, 0.93f, 0), new Vector3(0.48f, 0.06f, 0.48f), Dark);
                }
            }),
            new PropDef("PriceBoard", "가격표 간판 (입구)", new Vector3(2.4f, 3.2f, 0.2f), true, "two posts + board; text faces -Z", t =>
            {
                B(t, "PostL", new Vector3(-1.05f, 1.2f, 0.05f), new Vector3(0.12f, 2.4f, 0.12f), Steel);
                B(t, "PostR", new Vector3(1.05f, 1.2f, 0.05f), new Vector3(0.12f, 2.4f, 0.12f), Steel);
                B(t, "Board", new Vector3(0, 2.25f, 0), new Vector3(2.4f, 1.9f, 0.1f), Blue);
                B(t, "Header", new Vector3(0, 3.05f, -0.06f), new Vector3(2.3f, 0.3f, 0.02f), Yellow);
                Txt(t, "셀프 세차 요금표", new Vector3(0, 3.05f, -0.08f), 0.028f, new Color(0.1f, 0.12f, 0.2f));
                Txt(t, "기본 세차   $30\n고압수 · 거품 · 헹굼\n왁스 추가   +$5\n진공 청소   $5\n24시간 영업", new Vector3(0, 2.1f, -0.07f), 0.022f, Color.white);
            }),
            new PropDef("InfoBoard", "이용 안내판", new Vector3(1.6f, 2.2f, 0.15f), true, "text faces -Z", t =>
            {
                B(t, "Post", new Vector3(0, 0.7f, 0.04f), new Vector3(0.1f, 1.4f, 0.1f), Steel);
                B(t, "Board", new Vector3(0, 1.65f, 0), new Vector3(1.6f, 1.1f, 0.08f), White);
                Txt(t, "이용 안내\n1. 결제기에서 모드 선택\n2. 고압수 → 거품 → 헹굼\n3. 건조 · 진공은 출구 쪽", new Vector3(0, 1.65f, -0.05f), 0.018f, new Color(0.1f, 0.15f, 0.3f));
            }),
            new PropDef("NoEntrySign", "진입금지 표지", new Vector3(0.7f, 2.2f, 0.1f), false, "red disc + white bar; faces -Z", t =>
            {
                B(t, "Post", new Vector3(0, 0.9f, 0.03f), new Vector3(0.07f, 1.8f, 0.07f), Steel);
                C(t, "Disc", new Vector3(0, 1.85f, 0), new Vector3(0.66f, 0.02f, 0.66f), Red, new Vector3(90, 0, 0));
                B(t, "Bar", new Vector3(0, 1.85f, -0.025f), new Vector3(0.46f, 0.11f, 0.01f), White);
            }),
            new PropDef("RoadArrow", "노면 화살표", new Vector3(0.9f, 0.01f, 3f), false, "painted, points +Z", t =>
            {
                Decal(t, "arrow", new Vector2(0.9f, 3f)); // M4.10: painted = a decal (no slab z-fighting)
            }),
            new PropDef("CoinChanger", "동전교환기", new Vector3(0.6f, 1.6f, 0.5f), true, "front = -Z", t =>
            {
                B(t, "Body", new Vector3(0, 0.8f, 0), new Vector3(0.6f, 1.6f, 0.5f), Yellow);
                B(t, "Note", new Vector3(0, 1.15f, -0.26f), new Vector3(0.3f, 0.05f, 0.02f), Dark);
                B(t, "Tray", new Vector3(0, 0.45f, -0.27f), new Vector3(0.3f, 0.12f, 0.06f), Steel);
                Txt(t, "동전교환", new Vector3(0, 1.42f, -0.26f), 0.012f, new Color(0.1f, 0.1f, 0.15f));
            }),
            new PropDef("VendingMachine", "자판기", new Vector3(1.0f, 1.9f, 0.8f), true, "glass front with drinks; front = -Z", t =>
            {
                B(t, "Body", new Vector3(0, 0.95f, 0), new Vector3(1.0f, 1.9f, 0.8f), Red);
                B(t, "Window", new Vector3(-0.12f, 1.15f, -0.42f), new Vector3(0.65f, 1.2f, 0.02f), Glass); // 3 cm proud (2 cm flickered at a distance)
                var cans = new[] { Yellow, Blue, Green, White };
                for (int r = 0; r < 4; r++) for (int i = 0; i < 4; i++) C(t, $"Can{r}{i}", new Vector3(-0.36f + i * 0.16f, 0.7f + r * 0.27f, -0.38f), new Vector3(0.08f, 0.07f, 0.08f), cans[(r + i) % 4]);
                for (int i = 0; i < 4; i++) B(t, "Btn" + i, new Vector3(0.36f, 1.35f - i * 0.12f, -0.41f), new Vector3(0.1f, 0.06f, 0.02f), White);
                B(t, "Out", new Vector3(-0.12f, 0.3f, -0.41f), new Vector3(0.5f, 0.16f, 0.03f), Dark);
            }),
            new PropDef("Bench", "벤치", new Vector3(1.6f, 0.85f, 0.5f), true, "front = -Z", t =>
            {
                for (int i = 0; i < 3; i++) B(t, "Slat" + i, new Vector3(0, 0.45f, -0.15f + i * 0.13f), new Vector3(1.6f, 0.04f, 0.1f), M("Wood", new Color(0.6f, 0.42f, 0.25f)));
                B(t, "Back", new Vector3(0, 0.72f, 0.22f), new Vector3(1.6f, 0.25f, 0.04f), M("Wood", new Color(0.6f, 0.42f, 0.25f)));
                B(t, "LegL", new Vector3(-0.7f, 0.22f, 0), new Vector3(0.06f, 0.44f, 0.45f), Dark);
                B(t, "LegR", new Vector3(0.7f, 0.22f, 0), new Vector3(0.06f, 0.44f, 0.45f), Dark);
            }),
            new PropDef("Parasol", "파라솔 + 테이블", new Vector3(2.2f, 2.4f, 2.2f), false, "round table + umbrella", t =>
            {
                C(t, "Pole", new Vector3(0, 1.15f, 0), new Vector3(0.05f, 1.15f, 0.05f), Steel);
                C(t, "Shade", new Vector3(0, 2.3f, 0), new Vector3(2.2f, 0.06f, 2.2f), Red);
                C(t, "ShadeTop", new Vector3(0, 2.38f, 0), new Vector3(1.2f, 0.05f, 1.2f), White);
                C(t, "Table", new Vector3(0, 0.72f, 0), new Vector3(0.8f, 0.02f, 0.8f), White);
                C(t, "TableLeg", new Vector3(0, 0.36f, 0), new Vector3(0.06f, 0.36f, 0.06f), Steel);
            }),
            new PropDef("Cctv", "CCTV 카메라", new Vector3(0.3f, 0.25f, 0.45f), false, "wall / pillar mount; looks along -Z", t =>
            {
                B(t, "Bracket", new Vector3(0, 0, 0.12f), new Vector3(0.06f, 0.06f, 0.24f), White);
                B(t, "Body", new Vector3(0, -0.06f, -0.08f), new Vector3(0.14f, 0.12f, 0.3f), White);
                C(t, "Lens", new Vector3(0, -0.06f, -0.24f), new Vector3(0.08f, 0.02f, 0.08f), Dark, new Vector3(90, 0, 0));
            }),
            new PropDef("Extinguisher", "소화기", new Vector3(0.2f, 0.62f, 0.2f), false, "", t =>
            {
                C(t, "Tank", new Vector3(0, 0.27f, 0), new Vector3(0.16f, 0.27f, 0.16f), Red);
                B(t, "Head", new Vector3(0, 0.58f, 0), new Vector3(0.06f, 0.06f, 0.1f), Dark);
                C(t, "Hose", new Vector3(0.07f, 0.4f, -0.04f), new Vector3(0.025f, 0.15f, 0.025f), Rubber, new Vector3(0, 0, 15));
            }),
            new PropDef("WaterTank", "물탱크", new Vector3(1.8f, 2.4f, 1.8f), true, "big plastic tank on a stand + ladder", t =>
            {
                B(t, "Stand", new Vector3(0, 0.15f, 0), new Vector3(1.8f, 0.3f, 1.8f), Steel);
                C(t, "Tank", new Vector3(0, 1.3f, 0), new Vector3(1.7f, 1.0f, 1.7f), M("TankBlue", new Color(0.35f, 0.62f, 0.85f), 0.3f));
                for (int i = 0; i < 5; i++) B(t, "Rung" + i, new Vector3(0, 0.5f + i * 0.4f, -0.92f), new Vector3(0.4f, 0.04f, 0.04f), Steel);
            }),
            new PropDef("ChemDrum", "약품 드럼통", new Vector3(0.6f, 0.9f, 0.6f), true, "blue drum with a label", t =>
            {
                C(t, "Drum", new Vector3(0, 0.45f, 0), new Vector3(0.58f, 0.45f, 0.58f), Blue);
                C(t, "Label", new Vector3(0, 0.5f, 0), new Vector3(0.59f, 0.12f, 0.59f), White);
                C(t, "Cap", new Vector3(0.15f, 0.91f, 0), new Vector3(0.08f, 0.02f, 0.08f), Dark);
            }),
            new PropDef("BucketStack", "버킷 (쌓아 둔 것)", new Vector3(0.4f, 0.6f, 0.4f), false, "", t =>
            {
                for (int i = 0; i < 3; i++) C(t, "Bucket" + i, new Vector3(0, 0.18f + i * 0.1f, 0), new Vector3(0.36f, 0.17f, 0.36f), i == 2 ? Yellow : Blue);
            }),
            new PropDef("TowelRack", "수건 걸이", new Vector3(1.2f, 1.3f, 0.4f), false, "frame + 4 towels", t =>
            {
                B(t, "PostL", new Vector3(-0.58f, 0.65f, 0), new Vector3(0.05f, 1.3f, 0.05f), Steel);
                B(t, "PostR", new Vector3(0.58f, 0.65f, 0), new Vector3(0.05f, 1.3f, 0.05f), Steel);
                B(t, "Bar", new Vector3(0, 1.25f, 0), new Vector3(1.2f, 0.04f, 0.04f), Steel);
                var cols = new[] { White, Blue, Yellow, White };
                for (int i = 0; i < 4; i++) B(t, "Towel" + i, new Vector3(-0.42f + i * 0.28f, 0.98f, 0.04f), new Vector3(0.24f, 0.5f, 0.02f), cols[i]); // hang in front of the bar
            }),
            new PropDef("SlipperySign", "미끄럼 주의 표지", new Vector3(0.4f, 0.7f, 0.5f), false, "yellow A-frame", t =>
            {
                var a = B(t, "SideA", new Vector3(0, 0.33f, -0.1f), new Vector3(0.38f, 0.66f, 0.02f), Yellow); a.transform.localEulerAngles = new Vector3(-16, 0, 0);
                var b = B(t, "SideB", new Vector3(0, 0.33f, 0.1f), new Vector3(0.38f, 0.66f, 0.02f), Yellow); b.transform.localEulerAngles = new Vector3(16, 0, 0);
                Txt(t, "미끄럼\n주의", new Vector3(0, 0.38f, -0.125f), 0.009f, new Color(0.1f, 0.1f, 0.1f), new Vector3(-16, 0, 0));
            }),
            new PropDef("Pipes", "배관 (벽 따라)", new Vector3(6f, 0.3f, 0.25f), false, "two pipes along local X with brackets", t =>
            {
                C(t, "PipeA", new Vector3(0, 1.3f, 0), new Vector3(0.12f, 3f, 0.12f), Steel, new Vector3(0, 0, 90));
                C(t, "PipeB", new Vector3(0, 1.55f, 0), new Vector3(0.08f, 3f, 0.08f), M("Copper", new Color(0.72f, 0.45f, 0.25f), 0.6f), new Vector3(0, 0, 90));
                for (int i = 0; i < 4; i++) B(t, "Bracket" + i, new Vector3(-2.7f + i * 1.8f, 1.42f, 0.08f), new Vector3(0.05f, 0.4f, 0.06f), Dark);
            }),
            new PropDef("ElectricBox", "전기 박스", new Vector3(0.7f, 1.0f, 0.3f), false, "wall box; front = -Z", t =>
            {
                B(t, "Box", new Vector3(0, 0.5f, 0), new Vector3(0.7f, 1.0f, 0.3f), M("Grey", new Color(0.55f, 0.57f, 0.55f), 0.3f));
                B(t, "Door", new Vector3(0, 0.5f, -0.16f), new Vector3(0.62f, 0.92f, 0.02f), M("Grey", new Color(0.55f, 0.57f, 0.55f), 0.3f));
                B(t, "Warn", new Vector3(0, 0.75f, -0.18f), new Vector3(0.16f, 0.14f, 0.01f), Yellow);
            }),
            new PropDef("AcUnit", "에어컨 실외기", new Vector3(0.9f, 0.7f, 0.35f), true, "fan grille on the front (-Z)", t =>
            {
                B(t, "Body", new Vector3(0, 0.35f, 0), new Vector3(0.9f, 0.7f, 0.35f), White);
                C(t, "Fan", new Vector3(-0.15f, 0.35f, -0.18f), new Vector3(0.5f, 0.02f, 0.5f), Dark, new Vector3(90, 0, 0));
                B(t, "Feet", new Vector3(0, 0.035f, 0), new Vector3(0.95f, 0.07f, 0.4f), Steel); // top clear of the 2.5–5 cm ground slabs
            }),
            new PropDef("Planter", "화분 (플랜터)", new Vector3(0.8f, 0.9f, 0.8f), true, "", t =>
            {
                B(t, "Pot", new Vector3(0, 0.25f, 0), new Vector3(0.8f, 0.5f, 0.8f), M("Terracotta", new Color(0.62f, 0.36f, 0.24f)));
                S(t, "Bush", new Vector3(0, 0.68f, 0), new Vector3(0.8f, 0.55f, 0.8f), Green);
            }),
            new PropDef("Bicycle", "자전거", new Vector3(1.7f, 1.0f, 0.5f), false, "along local X", t =>
            {
                C(t, "WheelF", new Vector3(0.6f, 0.34f, 0), new Vector3(0.66f, 0.02f, 0.66f), Rubber, new Vector3(90, 0, 0));
                C(t, "WheelB", new Vector3(-0.6f, 0.34f, 0), new Vector3(0.66f, 0.02f, 0.66f), Rubber, new Vector3(90, 0, 0));
                var f1 = B(t, "Down", new Vector3(0.1f, 0.5f, 0), new Vector3(0.8f, 0.04f, 0.04f), Red); f1.transform.localEulerAngles = new Vector3(0, 0, 25);
                B(t, "Top", new Vector3(0.05f, 0.72f, 0), new Vector3(0.9f, 0.04f, 0.04f), Red);
                B(t, "Seat", new Vector3(-0.32f, 0.85f, 0), new Vector3(0.22f, 0.05f, 0.1f), Dark);
                B(t, "Bars", new Vector3(0.55f, 0.92f, 0), new Vector3(0.04f, 0.04f, 0.5f), Dark);
            }),
        };

        public static string PathOf(string id) => $"{PropDir}/SW_{id}.prefab";
        /// <summary>True when the Blender model for this prop is in place (it replaces the procedural mesh).</summary>
        public static bool HasModel(string id) => GlbKit.Find("SelfWash", "SW_" + id) != null;

        /// <summary>(Re)builds every prop prefab.</summary>
        public static void BuildPrefabs()
        {
            Directory.CreateDirectory(PropDir);
            foreach (var d in Catalog)
            {
                var root = new GameObject("SW_" + d.id) { layer = Layers.Scenery };
                // Blender model delivered (ART_REQUESTS "Art delivery status"): same pivot / front −Z as the graybox → swap 1:1
                string glb = GlbKit.Find("SelfWash", "SW_" + d.id);
                if (glb != null)
                {
                    // keep the graybox build's decals (board text, painted marks): the Blender boards leave blank faces for them
                    d.build(root.transform);
                    foreach (Transform ch in root.transform.Cast<Transform>().ToArray())
                        if (ch.GetComponent<UnityEngine.Rendering.Universal.DecalProjector>() == null) Object.DestroyImmediate(ch.gameObject);
                    var model = GlbKit.Model(glb, root.transform, Layers.Scenery, Vector3.zero);
                    var arm = VehicleInteriorBuilder.Deep(model.transform, "Arm");
                    if (d.id == "BoomArm" && arm != null) arm.gameObject.AddComponent<Visual.BoomSway>(); // the arm swings, the mount stays
                }
                else d.build(root.transform);
                foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = Layers.Scenery;
                foreach (var r in root.GetComponentsInChildren<Renderer>(true)) r.shadowCastingMode = d.size.y < 0.3f ? UnityEngine.Rendering.ShadowCastingMode.Off : UnityEngine.Rendering.ShadowCastingMode.On;
                if (d.solid)
                {
                    // one simple collider for the whole prop (F7: no per-part decoration colliders); Default layer = blocks players / props
                    root.layer = Layers.Default;
                    var bc = root.AddComponent<BoxCollider>(); bc.center = new Vector3(0, d.size.y * 0.5f, 0); bc.size = d.size;
                }
                PrefabUtility.SaveAsPrefabAsset(root, PathOf(d.id));
                Object.DestroyImmediate(root);
            }
        }

        static GameObject Place(Transform parent, string id, Vector3 pos, float yaw, float footprint = -1f)
        {
            var p = AssetDatabase.LoadAssetAtPath<GameObject>(PathOf(id));
            var g = (GameObject)PrefabUtility.InstantiatePrefab(p, parent);
            g.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
            var d = Catalog.First(c => c.id == id);
            Footprints.Add((pos, footprint >= 0f ? footprint : Mathf.Max(d.size.x, d.size.z) * 0.5f + 0.3f));
            return g;
        }

        // ------------------------------------------------------------------ the lot
        public static void Build(Transform env, Bay[] bays)
        {
            Footprints.Clear();
            BuildPrefabs();
            var root = new GameObject("SelfWash").transform; root.SetParent(env, false);
            Canopy(root);
            foreach (var b in bays) BayProps(root, b);
            VacuumArea(root);
            Entrance(root);
            OfficeSide(root);
            BackYard(root);
            WriteArtRequests();
        }

        /// <summary>M4.10 follow-up: light metal ceiling panels (linear strips every 0.3 m with dark seams, joints every 1.2 m),
        /// generated once (Art/Generated/Textures/ceiling_panels.png, mipmapped + anisotropic), tiled per 1.2 m.</summary>
        static Material CeilingMat(string name, Vector2 size)
        {
            const string texPath = "Assets/_Project/Art/Generated/Textures/ceiling_panels.png";
            if (!File.Exists(texPath))
            {
                Directory.CreateDirectory(Path.GetDirectoryName(texPath));
                const int S = 512; var px = new Color[S * S];
                for (int y = 0; y < S; y++)
                    for (int x = 0; x < S; x++)
                    {
                        float v = 0.9f;
                        int sy = y % 128, sx = x % 512;
                        if (sy < 3) v = 0.6f; else if (sy < 5) v = 0.97f;              // strip seam + highlight lip
                        if (sx < 2) v = Mathf.Min(v, 0.7f);                            // panel joint every 1.2 m
                        v *= 0.985f + 0.015f * Mathf.PerlinNoise(x * 0.05f, y * 0.05f);
                        px[y * S + x] = new Color(v, v, v * 1.01f, 1f);
                    }
                var t = new Texture2D(S, S, TextureFormat.RGBA32, true); t.SetPixels(px); t.Apply();
                File.WriteAllBytes(texPath, t.EncodeToPNG()); Object.DestroyImmediate(t);
                AssetDatabase.ImportAsset(texPath, ImportAssetOptions.ForceUpdate);
                if (AssetImporter.GetAtPath(texPath) is TextureImporter ti) { ti.mipmapEnabled = true; ti.anisoLevel = 8; ti.filterMode = FilterMode.Trilinear; ti.wrapMode = TextureWrapMode.Repeat; ti.SaveAndReimport(); }
            }
            var m = M(name, Color.white, 0.55f);
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texPath));
            m.SetTextureScale("_BaseMap", new Vector2(size.x / 1.2f, size.y / 1.2f));
            m.SetFloat("_Metallic", 0.12f);
            EditorUtility.SetDirty(m);
            return m;
        }

        static void Canopy(Transform root)
        {
            var c = new GameObject("Canopy") { layer = Layers.Default }.transform; c.SetParent(root, false);
            var roof = M("CanopyRoof", new Color(0.86f, 0.88f, 0.9f), 0.3f);
            var fascia = M("CanopyFascia", new Color(0.1f, 0.32f, 0.72f), 0.4f);
            const float x0 = -12.4f, x1 = 12.4f, z0 = -1.6f, z1 = 9.6f;
            float cx = (x0 + x1) / 2f, cz = (z0 + z1) / 2f, w = x1 - x0, d = z1 - z0;
            GrayboxMaterials.Box(c, "Roof", new Vector3(cx, CanopyY + 0.15f, cz), new Vector3(w, 0.3f, d), roof);
            // the underside people look up at: bright metal ceiling panels (the roof box underside read as a dark brown slab)
            B(c, "Ceiling", new Vector3(cx, CanopyY - 0.03f, cz), new Vector3(w - 0.24f, 0.02f, d - 0.24f), CeilingMat("CanopyCeiling", new Vector2(w - 0.24f, d - 0.24f)));
            B(c, "FasciaN", new Vector3(cx, CanopyY - 0.05f, z1), new Vector3(w + 0.1f, 0.75f, 0.12f), fascia);
            B(c, "FasciaS", new Vector3(cx, CanopyY - 0.05f, z0), new Vector3(w + 0.1f, 0.75f, 0.12f), fascia);
            B(c, "StripeN", new Vector3(cx, CanopyY - 0.32f, z1 + 0.08f), new Vector3(w + 0.14f, 0.08f, 0.04f), Yellow);   // M4.10: 4 cm proud of the fascia
            B(c, "StripeS", new Vector3(cx, CanopyY - 0.32f, z0 - 0.08f), new Vector3(w + 0.14f, 0.08f, 0.04f), Yellow);
            foreach (float x in new[] { -12.2f, -4f, 4f, 12.2f })
                foreach (float z in new[] { z0 + 0.3f, z1 - 0.3f })
                    GrayboxMaterials.Box(c, $"Pillar_{x}_{z}", new Vector3(x, CanopyY / 2f, z), new Vector3(0.35f, CanopyY, 0.35f), Steel);
            // the big name on the north fascia (readable from the feed lane / queue)
            // M4.10: the name on its own roof sign (on the fascia it collided with the bay plates 1·2·3)
            var sign = GrayboxMaterials.Box(c, "RoofSign", new Vector3(cx, CanopyY + 0.75f, z1 - 0.4f), new Vector3(7.2f, 0.9f, 0.16f), fascia, collider: false).transform;
            B(c, "RoofSignPostL", new Vector3(cx - 3f, CanopyY + 0.4f, z1 - 0.4f), new Vector3(0.1f, 0.5f, 0.1f), Steel);
            B(c, "RoofSignPostR", new Vector3(cx + 3f, CanopyY + 0.4f, z1 - 0.4f), new Vector3(0.1f, 0.5f, 0.1f), Steel);
            TextDecals.Board(c, "셀프 세차장 · SUDSY", new Vector3(cx, CanopyY + 0.75f, z1 - 0.4f + 0.08f), new Vector3(0, 180f, 0), 0.52f, Color.white, 0.06f);
            ModelOver(c, "SW_Canopy", new Vector3(0f, 0f, 4f), "셀프 세차장 · SUDSY");
        }

        /// <summary>
        /// Blender canopy / vacuum canopy (visual only, ART_REQUESTS): our boxes keep their colliders, their looks go; the model
        /// sits at <paramref name="origin"/>. Its `Lights` fittings are hidden (our switchable LED bars are the working lights)
        /// and the roof-sign text decal moves onto its blank `RoofSign` board (front = +Z, towards the feed lane).
        /// </summary>
        static void ModelOver(Transform group, string id, Vector3 origin, string signText = null)
        {
            string glb = GlbKit.Find("SelfWash", id);
            if (glb == null) return;
            foreach (var r in group.GetComponentsInChildren<MeshRenderer>(true).ToArray())
            {
                var mf = r.GetComponent<MeshFilter>();
                Object.DestroyImmediate(r); if (mf != null) Object.DestroyImmediate(mf);
            }
            foreach (var d in group.GetComponentsInChildren<UnityEngine.Rendering.Universal.DecalProjector>(true).ToArray()) Object.DestroyImmediate(d.gameObject);
            var holder = new GameObject("Model_" + id) { layer = group.gameObject.layer }.transform;
            holder.SetParent(group, true); holder.position = origin; holder.rotation = Quaternion.identity;
            var model = GlbKit.Model(glb, holder, Layers.Default, Vector3.zero);
            var lights = VehicleInteriorBuilder.Deep(model.transform, "Lights");
            if (lights != null) lights.gameObject.SetActive(false);
            var sign = signText != null ? VehicleInteriorBuilder.Deep(model.transform, "RoofSign") : null;
            var sr = sign != null ? sign.GetComponentInChildren<Renderer>() : null;
            if (sr != null)
            {
                var b = sr.bounds;
                // the blank face is the model's front (−Z, towards the bays): project from there along +Z
                TextDecals.Board(group, signText, new Vector3(b.center.x, b.center.y, b.min.z - 0.03f), Vector3.zero, Mathf.Min(0.52f, b.size.y * 0.6f), Color.white, b.size.z + 0.08f);
            }
        }

        static void BayProps(Transform root, Bay b)
        {
            var c = b.transform.position; int n = b.Index + 1;
            var g = new GameObject("BayProps" + n).transform; g.SetParent(root, false);
            Place(g, "PayTerminal", c + new Vector3(2.82f, 0, 3.6f), 90f);              // east divider, north end, facing into the bay
            Place(g, "BoomArm", new Vector3(c.x, CanopyY, c.z), 0f, 0f);
            Place(g, "FoamBrushHolder", c + new Vector3(-2.88f, 0, 2.6f), -90f, 0f);   // west divider
            Place(g, "HoseReelDrum", c + new Vector3(-2.88f, 1.65f, 0f), -90f, 0f);    // above the hose socket
            Place(g, "DrainGrate", c + new Vector3(0, 0.005f, 0f), 0f, 0f);
            // big bay number painted on the floor at the entrance (read by drivers coming in from the north)
            TextDecals.Floor(g, n.ToString(), c + new Vector3(0, 0, 3.2f), 180f, 1.3f, new Color(1f, 1f, 1f, 0.9f), 1.8f);
            Footprints.Add((c, 3.2f)); // the whole bay stays clear of random dressing
        }

        static void VacuumArea(Transform root)
        {
            var g = new GameObject("DryVacuumArea").transform; g.SetParent(root, false);
            var z = VacuumZone;
            float cx = z.center.x, cz = z.center.y;
            // light canopy on four posts
            GrayboxMaterials.Box(g, "VacRoof", new Vector3(cx, 3.35f, cz), new Vector3(z.width + 0.8f, 0.18f, z.height + 0.8f), M("VacRoof", new Color(0.2f, 0.55f, 0.35f), 0.3f));
            B(g, "VacCeiling", new Vector3(cx, 3.21f, cz), new Vector3(z.width + 0.6f, 0.02f, z.height + 0.6f), CeilingMat("VacCeiling", new Vector2(z.width + 0.6f, z.height + 0.6f)));
            foreach (float x in new[] { z.xMin + 0.2f, z.xMax - 0.2f })
                foreach (float zz in new[] { z.yMin + 0.2f, z.yMax - 0.2f })
                    GrayboxMaterials.Box(g, $"VacPost_{x}_{zz}", new Vector3(x, 1.65f, zz), new Vector3(0.22f, 3.3f, 0.22f), Steel);
            for (int i = 0; i < 4; i++) Place(g, "VacuumPillar", new Vector3(z.xMin + 1.4f + i * 2.6f, 0, z.yMax - 0.7f), 0f);
            Place(g, "MatRack", new Vector3(z.xMin + 1.6f, 0, z.yMin + 0.6f), 180f);
            Place(g, "MatWasher", new Vector3(z.xMin + 3.9f, 0, z.yMin + 0.6f), 180f);
            Place(g, "AirGun", new Vector3(z.xMin + 5.8f, 0, z.yMin + 0.5f), 180f);
            Place(g, "RecycleBins", new Vector3(z.xMin + 7.8f, 0, z.yMin + 0.55f), 180f);
            Place(g, "TowelRack", new Vector3(z.xMax - 0.9f, 0, z.yMin + 0.6f), 180f);
            EnvironmentBuilder.NightSpot(g, "VacLight", new Vector3(cx, 3.12f, cz), new Vector3(cx, 0f, cz), 9f, 12f, new Color(1f, 0.95f, 0.85f), 0.38f, 130f);
            Footprints.Add((new Vector3(cx, 0, cz), 4.5f));
            // Blender vacuum canopy (visual only; origin = the zone centre): our roof / posts / ceiling keep only their colliders
            string vglb = GlbKit.Find("SelfWash", "SW_VacCanopy");
            if (vglb != null)
            {
                foreach (Transform ch in g.Cast<Transform>().ToArray())
                    if (ch.name.StartsWith("VacRoof") || ch.name.StartsWith("VacPost") || ch.name.StartsWith("VacCeiling"))
                    {
                        var r = ch.GetComponent<MeshRenderer>(); var mf = ch.GetComponent<MeshFilter>();
                        if (r != null) Object.DestroyImmediate(r); if (mf != null) Object.DestroyImmediate(mf);
                    }
                var holder = new GameObject("Model_SW_VacCanopy").transform; holder.SetParent(g, true); holder.position = new Vector3(cx, 0f, cz);
                GlbKit.Model(vglb, holder, Layers.Default, Vector3.zero);
            }
        }

        static void Entrance(Transform root)
        {
            var g = new GameObject("Entrance").transform; g.SetParent(root, false);
            // west of the leave lane (angry cars drive out at x = queue − 4.5): boards face the queue lane (east)
            Place(g, "PriceBoard", new Vector3(-27.0f, 0, -8.8f), -90f);
            Place(g, "InfoBoard", new Vector3(-27.0f, 0, -4.2f), -90f);
            Place(g, "Planter", new Vector3(-26.9f, 0, -1.6f), 0f);
            Place(g, "Planter", new Vector3(-26.9f, 0, 0.9f), 0f);
            // painted: 서행 + 입구 on the queue lane, arrows along the route, 출구 at the ramp
            Place(g, "RoadArrow", new Vector3(LotBuilder.QueueX, 0.004f, -3f), 0f, 0f);
            Place(g, "RoadArrow", new Vector3(14f, 0.004f, -5f), 90f, 0f);
            Place(g, "RoadArrow", new Vector3(RampX, 0.004f, -9.2f), 180f, 0f);
            Floor(g, "서행", new Vector3(LotBuilder.QueueX, 0f, -7.75f), 0f, 1.9f); // between the queue marks at z -5 and -10.5
            Floor(g, "입구", new Vector3(LotBuilder.QueueX, 0f, -12.0f), 0f, 2.2f);
            Floor(g, "출구", new Vector3(RampX, 0f, -11.8f), 180f, 2.2f);
            Place(g, "NoEntrySign", new Vector3(RampX + 2.1f, 0, -12.6f), 0f, 0.5f);  // faces the road: no driving in through the exit
        }

        /// <summary>Painted text on the asphalt, read by a driver heading along <paramref name="yaw"/>.</summary>
        static void Floor(Transform g, string s, Vector3 p, float yaw, float width) => TextDecals.Floor(g, s, p, yaw, width, new Color(1f, 1f, 1f, 0.92f));

        static void OfficeSide(Transform root)
        {
            var g = new GameObject("OfficeSide").transform; g.SetParent(root, false);
            var o = LotBuilder.OfficeCenter; // office box: x -16…-8, z -13…-7
            // M4.10: kept clear of the day board (x -14.7…-12.3) and the shop terminal (x -9.9…-9.1)
            Place(g, "VendingMachine", new Vector3(-15.45f, 0, -6.55f), 180f);
            Place(g, "VendingMachine", new Vector3(-11.6f, 0, -6.55f), 180f);
            Place(g, "CoinChanger", new Vector3(-10.6f, 0, -6.7f), 180f);
            Place(g, "Bench", new Vector3(-17.6f, 0, -12.6f), 90f);
            Place(g, "Parasol", new Vector3(-17.6f, 0, -10.6f), 0f, 1.0f); // the Blender parasol is wider: clear of the bench
            Place(g, "Bicycle", new Vector3(-14.5f, 0, -13.6f), 0f);
            Place(g, "AcUnit", new Vector3(-11.4f, 0, -13.35f), 0f);
            Place(g, "ElectricBox", new Vector3(-9.6f, 0.3f, -13.18f), 0f, 0.3f);
            Place(g, "Extinguisher", new Vector3(-8.8f, 0, -13.25f), 0f, 0.2f);
            Place(g, "Cctv", new Vector3(-8.1f, 2.7f, -13.1f), 135f, 0f);
            // under the canopy: extinguishers on the middle pillars, CCTV on the corner pillars, slippery signs at the exits
            Place(g, "Extinguisher", new Vector3(-4f, 0, -1.6f), 0f, 0.2f);
            Place(g, "Extinguisher", new Vector3(4f, 0, -1.6f), 0f, 0.2f);
            Place(g, "Cctv", new Vector3(12.2f, 3.6f, 9.05f), 225f, 0f);
            Place(g, "Cctv", new Vector3(-12.2f, 3.6f, -1.05f), 45f, 0f);
            Place(g, "SlipperySign", new Vector3(-4f, 0, -2.4f), 20f, 0.4f);
            Place(g, "SlipperySign", new Vector3(4f, 0, -2.4f), -20f, 0.4f);
            Place(g, "BucketStack", new Vector3(-12.9f, 0, 6.2f), 0f, 0.4f);
            Place(g, "TowelRack", new Vector3(12.95f, 0, 2.2f), 90f);
        }

        static void BackYard(Transform root)
        {
            var g = new GameObject("BackYard").transform; g.SetParent(root, false);
            Place(g, "WaterTank", new Vector3(-14.2f, 0, 16.1f), 0f);
            Place(g, "ChemDrum", new Vector3(-10.2f, 0, 16.9f), 0f);
            Place(g, "ChemDrum", new Vector3(-9.5f, 0, 16.9f), 0f);
            Place(g, "ChemDrum", new Vector3(-9.85f, 0, 16.25f), 0f);
            Place(g, "Pipes", new Vector3(-4f, 0, 18.2f), 0f, 0f);
            Place(g, "Pipes", new Vector3(2f, 0, 18.2f), 0f, 0f);
            Place(g, "ElectricBox", new Vector3(6.2f, 0.4f, 18.25f), 0f, 0.3f);
            Place(g, "AcUnit", new Vector3(8.4f, 0, 18.0f), 0f);
            Place(g, "AcUnit", new Vector3(9.5f, 0, 18.0f), 0f);
            Place(g, "HoseReelDrum", new Vector3(-7f, 1.3f, 18.3f), 180f, 0f);
            Place(g, "Planter", new Vector3(11.5f, 0, 17.6f), 0f);
        }

        // ------------------------------------------------------------------ ART_REQUESTS.md
        const string ArtPath = "docs/ART_REQUESTS.md";
        const string Begin = "<!-- G9 self-wash props: generated by SelfWashBuilder -->", End = "<!-- /G9 self-wash props -->";

        public static void WriteArtRequests()
        {
            var sb = new StringBuilder();
            sb.AppendLine(Begin);
            sb.AppendLine("## M4.9 G9 — self-service car wash props (procedural placeholders, replace 1:1)");
            sb.AppendLine();
            sb.AppendLine("Each one is a prefab under `Assets/_Project/Prefabs/Props/SelfWash/SW_<id>.prefab`. Pivot: bottom centre. Front: −Z. Sizes in metres (W × H × D). Low poly, flat colours, readable at 10 m. GLB is fine. Keep the footprint, because routes and colliders use it.");
            sb.AppendLine();
            sb.AppendLine("| id | 이름 | size W×H×D (m) | collider | notes |");
            sb.AppendLine("|---|---|---|---|---|");
            foreach (var d in Catalog)
                sb.AppendLine($"| SW_{d.id} | {d.nameKo} | {d.size.x:0.##} × {d.size.y:0.##} × {d.size.z:0.##} | {(d.solid ? "box" : "none")} | {(HasModel(d.id) ? "**Blender model in use** — " : "")}{d.note} |");
            sb.AppendLine();
            sb.AppendLine("Also wanted: the canopy (one roof over 3 bays, 24.8 × 11.2 m, height 4.3 m, blue fascia with the name), the drying / vacuum area canopy (11.3 × 5.2 m, height 3.35 m), and the angry-face icon and ring / arrow sprites for the patience timers (G6, currently drawn in code by `UiSprites`).");
            sb.AppendLine(End);
            string text = File.Exists(ArtPath) ? File.ReadAllText(ArtPath) : "# Art requests\n";
            int a = text.IndexOf(Begin), b = text.IndexOf(End);
            if (a >= 0 && b > a) text = text.Substring(0, a) + sb.ToString().TrimEnd('\n', '\r') + text.Substring(b + End.Length);
            else text = text.TrimEnd() + "\n\n" + sb;
            File.WriteAllText(ArtPath, text, new UTF8Encoding(false));
        }
    }
}
