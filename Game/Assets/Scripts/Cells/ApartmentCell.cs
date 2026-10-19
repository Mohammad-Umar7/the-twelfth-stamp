using System.Collections.Generic;
using UnityEngine;

namespace TwelfthStamp
{
    /// <summary>
    /// Saeed's Abu Dhabi apartment. One builder, two cells with identical layout and coordinates:
    /// ApartmentNormal (homecoming, real home ending) and ApartmentCorrupt (the Margin copy: UAE encounter).
    /// The corrupted copy keeps every furniture position; its changes are the horror. Set dressing and the night view are
    /// in Detail_Apartment.cs and Detail_ApartmentView.cs.
    /// </summary>
    public partial class ApartmentCell : Cell
    {
        public bool Corrupted;
        public Transform TableTop, Suitcase, BathroomDoorPivot, PhoneRoot, FridgeRoot, WindowSeamPoint;
        public Socket CameraSpot, PassportSpot, CaseSpot, GlassesSpot;
        public Grabbable Camera, PassportProp, CaseProp, Glasses;
        public Material PhoneScreen;
        public GameObject ReflectionCorridor, ReflectionFigure;
        public Material ReflectionMat, ReflectionFigureMat;
        public readonly List<Transform> SkylineLayers = new List<Transform>();
        public TraceMark Trace;
        public GameObject GenuineNote;
        public GhostWhack Ghosts;   // the Margin copy only: the whack-a-ghost game (Cells/GhostWhack.cs)
        public Transform BathroomInside;
        public AudioSource FridgeHum;

        protected override void Build()
        {
            Lighting = Corrupted ? new LightingPreset
            {
                // The Margin copy: the same room gone cold and too quiet, but readable (playtest: it was near black). A cool
                // moon through the window, a pale ambient, the lamps drained of warmth rather than switched off.
                SunColor = new Color(0.5f, 0.6f, 0.76f), SunIntensity = 0.45f, SunEuler = new Vector3(32f, 230f, 0f), ShadowStrength = 0.55f,
                // The fog is thin so the dead city beyond the window still reads as silhouettes against the pale horizon.
                AmbientSky = new Color(0.25f, 0.29f, 0.35f), AmbientEquator = new Color(0.2f, 0.21f, 0.23f), AmbientGround = new Color(0.08f, 0.08f, 0.09f),
                FogColor = new Color(0.14f, 0.17f, 0.21f), FogDensity = 0.006f,
                SkyZenith = new Color(0.04f, 0.05f, 0.09f), SkyHorizon = new Color(0.3f, 0.35f, 0.42f), SkyGround = new Color(0.07f, 0.08f, 0.1f),
                SunDiscColor = new Color(0, 0, 0, 0), Stars = 0.15f,
                PostExposure = 0.34f, Saturation = -18f, Contrast = 10f, ColorFilter = new Color(0.88f, 0.95f, 0.98f), Bloom = 0.55f, Vignette = 0.26f
            } : new LightingPreset
            {
                // Dusk: a low sun across the floor and up the feature wall, the lamps on, the city lighting up outside.
                SunColor = new Color(1f, 0.64f, 0.4f), SunIntensity = 0.6f, SunEuler = new Vector3(12f, 250f, 0f), ShadowStrength = 0.7f,
                AmbientSky = new Color(0.34f, 0.32f, 0.38f), AmbientEquator = new Color(0.31f, 0.26f, 0.22f), AmbientGround = new Color(0.12f, 0.09f, 0.07f),
                FogColor = new Color(0.32f, 0.25f, 0.24f), FogDensity = 0.0035f,
                SkyZenith = new Color(0.12f, 0.16f, 0.32f), SkyHorizon = new Color(0.95f, 0.55f, 0.38f), SkyGround = new Color(0.18f, 0.14f, 0.13f),
                SunDiscColor = new Color(1f, 0.6f, 0.35f, 1f), SunSize = 0.006f, CloudColor = new Color(0.9f, 0.55f, 0.45f, 0.25f),
                PostExposure = 0.16f, Saturation = 5f, Contrast = 8f, ColorFilter = new Color(1f, 0.96f, 0.9f), Bloom = 0.55f, Vignette = 0.22f
            };
            Ambience = Corrupted ? "amb_apartment_corrupt" : "amb_apartment";
            AddSun();

            var floor = Mats.Lit(new Color(0.55f, 0.42f, 0.3f), 0.5f, 0f, "wood_oak_floor");
            var wall = Mats.Lit(Corrupted ? new Color(0.66f, 0.66f, 0.64f) : new Color(0.84f, 0.78f, 0.7f), 0.18f, 0f, "plaster_warm");
            var tile = Mats.Lit(new Color(0.78f, 0.76f, 0.72f), 0.55f, 0f, "tile_terrazzo");
            var wood = Mats.Lit(new Color(1f, 0.9f, 0.8f), 0.5f, 0f, "wood_dark");
            var fabric = Mats.Lit(Corrupted ? new Color(0.36f, 0.38f, 0.4f) : new Color(0.62f, 0.55f, 0.46f), 0.15f, 0f, "fabric_linen");
            var rug = Mats.Lit(new Color(0.75f, 0.68f, 0.62f), 0.1f, 0f, "carpet_rug");
            var metal = Mats.Lit(new Color(0.7f, 0.7f, 0.72f), 0.7f, 0.9f, "metal_brushed");
            var white = Mats.Lit(new Color(0.88f, 0.88f, 0.86f), 0.55f);
            var black = Mats.Lit(new Color(0.05f, 0.05f, 0.055f), 0.6f);
            var fabricDark = Mats.Lit(Corrupted ? new Color(0.22f, 0.23f, 0.25f) : new Color(0.34f, 0.27f, 0.21f), 0.15f, 0f, "fabric_linen");

            var g = Group("Apartment");
            // Shell: 9 x 8 m room, 2.9 m ceiling; window wall to the east (+X).
            MeshFactory.Floor("Floor", g, new Vector3(0, 0, 0), new Vector2(9f, 8f), floor, "wood", 0.2f, 1f);
            MeshFactory.Box("Ceiling", g, new Vector3(0, 2.95f, 0), new Vector3(9f, 0.1f, 8f), Mats.Lit(new Color(0.85f, 0.84f, 0.82f), 0.1f), false, 2f);
            MeshFactory.Box("WallW", g, new Vector3(-4.6f, 1.45f, 0), new Vector3(0.2f, 2.9f, 8f), wall, true, 1.5f);
            MeshFactory.Box("WallN", g, new Vector3(0, 1.45f, 4.1f), new Vector3(9.2f, 2.9f, 0.2f), wall, true, 1.5f);
            MeshFactory.Box("WallS_A", g, new Vector3(-1.4f, 1.45f, -4.1f), new Vector3(6.4f, 2.9f, 0.2f), wall, true, 1.5f);
            MeshFactory.Box("WallS_B", g, new Vector3(4.2f, 1.45f, -4.1f), new Vector3(0.8f, 2.9f, 0.2f), wall, true, 1.5f);
            MeshFactory.Box("WallS_Top", g, new Vector3(2.75f, 2.5f, -4.1f), new Vector3(2.1f, 0.8f, 0.2f), wall, true, 1.5f);
            // Window wall (east): low sill, mullions, glass.
            MeshFactory.Box("Sill", g, new Vector3(4.55f, 0.2f, 0), new Vector3(0.3f, 0.4f, 8f), wall, true, 1.5f);
            for (int i = 0; i < 5; i++) MeshFactory.Box("Mullion", g, new Vector3(4.55f, 1.6f, -3.9f + i * 1.95f), new Vector3(0.08f, 2.8f, 0.08f), black, true);
            var glass = MeshFactory.Box("Window", g, new Vector3(4.6f, 1.65f, 0), new Vector3(0.02f, 2.5f, 8f), Mats.Glass(new Color(0.3f, 0.35f, 0.4f, 0.14f)), false);
            glass.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            var glassBlock = new GameObject("WindowCollider");
            glassBlock.transform.SetParent(g, false);
            glassBlock.transform.localPosition = new Vector3(4.6f, 1.65f, 0);
            glassBlock.AddComponent<BoxCollider>().size = new Vector3(0.1f, 2.6f, 8f);

            // The night view: the Corniche below, a marina channel, a skyline across the water (its three layers drift apart
            // in the corrupted copy, where every window has gone dark).
            BuildCityView(g);

            // Kitchen (north wall): counter, fridge, upper cabinets. The Blender models (Art/Models/MODELS.md) replace the
            // procedural meshes only; every collider below stays where it was.
            var counter = MeshFactory.Box("Counter", g, new Vector3(-1.5f, 0.45f, 3.65f), new Vector3(4f, 0.9f, 0.7f), wood, true);
            // The run's back is 0.31 m behind its origin: turned to face the room, that back sits on the wall (z = 4.0).
            var ovenGlass = Mats.Lit(new Color(0.03f, 0.035f, 0.04f), 0.95f, 0.2f);
            if (Models.Spawn("KitchenRun", g, Models.Slots(("TS_Wood", wood), ("TS_Stone", tile), ("TS_Metal", metal), ("TS_Glass", ovenGlass)), new Vector3(-1.5f, 0f, 3.69f)) != null)
                Models.StripVisual(counter);
            else MeshFactory.Box("CounterTop", g, new Vector3(-1.5f, 0.92f, 3.62f), new Vector3(4.05f, 0.05f, 0.75f), tile, false);
            MeshFactory.Box("Upper", g, new Vector3(-1.5f, 2.2f, 3.82f), new Vector3(4f, 0.8f, 0.36f), wood, false);
            FridgeRoot = MeshFactory.Box("Fridge", g, new Vector3(1.1f, 0.95f, 3.62f), new Vector3(0.85f, 1.9f, 0.72f), white, true).transform;
            bool fridgeModel = Models.Spawn("Fridge", FridgeRoot, Models.Slots(("TS_PlasticWhite", white), ("TS_Metal", metal)), new Vector3(0f, -0.95f, 0f)) != null;
            if (fridgeModel) Models.StripVisual(FridgeRoot.gameObject);
            else MeshFactory.Box("FridgeHandle", FridgeRoot, new Vector3(-0.32f, 0.2f, -0.38f), new Vector3(0.03f, 0.5f, 0.04f), metal, false);
            // Notes on the fridge: in the copy, every note is blank except the genuine one. The modelled fridge has French
            // doors with their handles on the centre seam, so there the notes all go on the left door, the genuine one
            // (i = 1) right under the UAE trace, which stays where it is.
            var noteRng = new System.Random(5);
            for (int i = 0; i < 4; i++)
            {
                var notePos = fridgeModel
                    ? new Vector3(0.79f + (i % 2) * 0.16f, 1.25f + (i / 2) * 0.2f, 3.255f)
                    : new Vector3(1.0f + (i % 2) * 0.22f - 0.1f, 1.25f + (i / 2) * 0.2f, 3.255f);
                var n = MeshFactory.Quad("Note", g, 0.14f, 0.12f, Mats.Lit(new Color(0.95f, 0.92f, 0.78f), 0.1f), notePos, Quaternion.Euler(0, 0, (float)(noteRng.NextDouble() - 0.5) * 10f));
                if (i == 1) GenuineNote = n;
            }
            var notesTex = Tex.Art("optician_notes");
            if (GenuineNote != null && notesTex != null)
                GenuineNote.GetComponent<Renderer>().sharedMaterial = Mats.Printed(notesTex, new Color(1f, 0.97f, 0.9f), 0.1f);

            // Living area: sofa, coffee table, rug, dining table (the unpacking table), chairs, shelf with a photo.
            MeshFactory.Box("Rug", g, new Vector3(1.2f, 0.005f, -0.6f), new Vector3(2.6f, 0.01f, 1.8f), rug, false);
            var sofaBase = MeshFactory.Box("SofaBase", g, new Vector3(1.2f, 0.22f, -2.1f), new Vector3(2.3f, 0.44f, 0.9f), fabric, true);
            var sofaBack = MeshFactory.Box("SofaBack", g, new Vector3(1.2f, 0.6f, -2.5f), new Vector3(2.3f, 0.6f, 0.2f), fabric, true);
            if (Models.Spawn("Sofa", g, Models.Slots(("TS_Fabric", fabric), ("TS_FabricDark", fabricDark), ("TS_WoodDark", wood)), new Vector3(1.2f, 0f, -2.1f), 0f) != null)
            {
                Models.StripVisual(sofaBase);
                Models.StripVisual(sofaBack);
            }
            // The coffee table keeps this collider; its visual (oak top, black legs, the coffee set) is in BuildHomeDetail.
            Models.StripVisual(MeshFactory.Box("CoffeeTable", g, new Vector3(1.2f, 0.2f, -0.7f), new Vector3(1.1f, 0.4f, 0.6f), wood, true));
            var table = MeshFactory.Box("Table", g, new Vector3(-1.6f, 0.74f, 0.4f), new Vector3(1.6f, 0.05f, 0.9f), wood, true);
            var tableModel = Models.Spawn("DiningTable", g, Models.Slots(("TS_Wood", wood)), new Vector3(-1.6f, 0f, 0.4f), 0f);
            if (tableModel != null)
            {
                // Its 0.75 m top is raised 1.5 cm to the procedural top, where the phone and the table spots rest.
                tableModel.transform.localScale = new Vector3(1f, 0.765f / 0.75f, 1f);
                Models.StripVisual(table);
            }
            else foreach (var x in new[] { -2.3f, -0.9f }) foreach (var z in new[] { 0.05f, 0.75f })
                MeshFactory.Box("TableLeg", g, new Vector3(x, 0.36f, z), new Vector3(0.05f, 0.72f, 0.05f), wood, false);
            for (int i = 0; i < 4; i++)
            {
                float x = -2.2f + (i % 2) * 1.2f;
                float z = i < 2 ? -0.35f : 1.15f;
                if (Corrupted && i == 3) z += 0.4f; // one chair has moved
                var seat = MeshFactory.Box("ChairSeat", g, new Vector3(x, 0.45f, z), new Vector3(0.45f, 0.05f, 0.45f), wood, true);
                // Every chair faces the table (the model's seat front is +Z).
                if (Models.Spawn("DiningChair", g, Models.Slots(("TS_Wood", wood), ("TS_Fabric", fabric)), new Vector3(x, 0f, z), i < 2 ? 0f : 180f) != null)
                    Models.StripVisual(seat);
                else MeshFactory.Box("ChairBack", g, new Vector3(x, 0.75f, z + (i < 2 ? -0.2f : 0.2f)), new Vector3(0.45f, 0.55f, 0.04f), wood, false);
            }
            TableTop = new GameObject("TableTop").transform;
            TableTop.SetParent(g, false);
            TableTop.localPosition = new Vector3(-1.6f, 0.77f, 0.4f);
            // (The old shelf's photo now stands on the credenza: BuildHomeDetail.)

            // Lighting: pendant over the table, floor lamp, kitchen strip.
            var warm = Corrupted ? new Color(0.75f, 0.85f, 0.95f) : new Color(1f, 0.78f, 0.52f);
            AddPointLight(g, new Vector3(-1.6f, 2.2f, 0.4f), warm, Corrupted ? 1.8f : 2.6f, 5f, true);
            MeshFactory.Frustum("Pendant", g, new Vector3(-1.6f, 2.25f, 0.4f), 0.25f, 0.08f, 0.22f, Mats.Emissive(new Color(0.2f, 0.2f, 0.2f), warm * (Corrupted ? 0.6f : 1.4f)), 18);
            AddPointLight(g, new Vector3(2.8f, 1.6f, -2.4f), warm, Corrupted ? 1.0f : 1.4f, 4f);
            AddPointLight(g, new Vector3(-1.5f, 1.9f, 3.4f), warm, Corrupted ? 0.9f : 1.1f, 3.5f);

            // Bathroom (south-east): door in the south wall leads to a small tiled room.
            var bath = Group("Bathroom");
            MeshFactory.Floor("BathFloor", bath, new Vector3(2.75f, 0.01f, -5.4f), new Vector2(2.2f, 2.4f), tile, "tile", 0.2f, 0.6f);
            MeshFactory.Box("BathW", bath, new Vector3(1.6f, 1.45f, -5.4f), new Vector3(0.15f, 2.9f, 2.6f), tile, true, 0.6f);
            MeshFactory.Box("BathE", bath, new Vector3(3.9f, 1.45f, -5.4f), new Vector3(0.15f, 2.9f, 2.6f), tile, true, 0.6f);
            MeshFactory.Box("BathS", bath, new Vector3(2.75f, 1.45f, -6.65f), new Vector3(2.4f, 2.9f, 0.15f), tile, true, 0.6f);
            MeshFactory.Box("BathCeil", bath, new Vector3(2.75f, 2.95f, -5.4f), new Vector3(2.4f, 0.1f, 2.6f), white, false);
            var sink = MeshFactory.Box("Sink", bath, new Vector3(2.75f, 0.85f, -6.4f), new Vector3(0.6f, 0.15f, 0.4f), white, true);
            // The vanity's origin is on the wall plane (the bathroom's south wall face, z = -6.575); it faces into the room.
            var vanity = Models.Spawn("BathroomSink", bath, Models.Slots(("TS_Wood", wood), ("TS_Metal", metal)), new Vector3(2.75f, 0f, -6.575f), 0f);
            var mirrorGlass = Models.Child(vanity, "MirrorGlass")?.GetComponent<Renderer>();
            // A real mirror (PlanarMirror): the room and the player's own body, which is seen only in reflections.
            // The Margin's copy runs a little cold and wrong.
            if (vanity != null)
            {
                Models.StripVisual(sink);
                if (mirrorGlass != null) PlanarMirror.Attach(mirrorGlass, vanity.transform.forward, Corrupted ? 1f : 0f);
            }
            else
            {
                var mirrorBox = MeshFactory.Box("Mirror", bath, new Vector3(2.75f, 1.6f, -6.56f), new Vector3(0.7f, 0.9f, 0.02f), white, false);
                PlanarMirror.Attach(mirrorBox.GetComponent<Renderer>(), bath.forward, Corrupted ? 1f : 0f);
            }
            AddPointLight(bath, new Vector3(2.75f, 2.5f, -5.4f), Corrupted ? new Color(0.7f, 0.8f, 0.9f) : new Color(1f, 0.9f, 0.75f), 1.6f, 3.5f);
            // A light bar over the mirror, as over any basin: the ceiling light sits behind your head there, so without it
            // your face in the glass was in shadow (nearly black in the reflection). The Margin's copy burns cold.
            var vanityColor = Corrupted ? new Color(0.72f, 0.84f, 0.92f) : new Color(1f, 0.93f, 0.82f);
            MeshFactory.Box("VanityLightBar", bath, new Vector3(2.75f, 2.2f, -6.5f), new Vector3(0.62f, 0.06f, 0.09f),
                Mats.Emissive(new Color(0.9f, 0.9f, 0.88f), vanityColor * 1.8f), false).GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            MeshFactory.Box("VanityLightMount", bath, new Vector3(2.75f, 2.2f, -6.56f), new Vector3(0.68f, 0.08f, 0.03f), metal, false);
            AddPointLight(bath, new Vector3(2.75f, 2.08f, -6.2f), vanityColor, Corrupted ? 0.9f : 1.3f, 2.8f);
            BathroomInside = new GameObject("BathroomInside").transform;
            BathroomInside.SetParent(bath, false);
            BathroomInside.localPosition = new Vector3(2.75f, 0f, -5.6f);
            BathroomDoorPivot = new GameObject("BathroomDoorPivot").transform;
            BathroomDoorPivot.SetParent(g, false);
            BathroomDoorPivot.localPosition = new Vector3(3.75f, 0f, -4.1f);
            // Procedural on purpose: this leaf and its collider are 1.9 m wide and it closes in the ending; Door.fbx is 0.9 m.
            MeshFactory.Box("BathroomDoor", BathroomDoorPivot, new Vector3(-0.95f, 1.05f, 0), new Vector3(1.9f, 2.1f, 0.05f), wood, true);
            BathroomDoorPivot.localRotation = Quaternion.Euler(0, 95f, 0); // open

            // Entrance door (west): the modelled leaf hangs on a hinge at its south edge, knuckle side facing the room.
            if (Models.Spawn("Door", g, Models.Slots(("TS_Wood", wood), ("TS_Metal", metal)), new Vector3(-4.48f, 0f, -3.05f), 90f) == null)
                MeshFactory.Box("FrontDoor", g, new Vector3(-4.48f, 1.05f, -2.6f), new Vector3(0.06f, 2.1f, 1f), wood, false);
            // The suitcase: the story's unpacking props rest at 0.2 m on CaseBottom (a gameplay collider and the "bag"
            // interactable's target), deeper than the model's 13 cm shell, so the open model is packed up to that height.
            Suitcase = new GameObject("Suitcase").transform;
            Suitcase.SetParent(g, false);
            Suitcase.localPosition = new Vector3(-3.4f, 0f, -1.6f);
            var luggage = Mats.Lit(new Color(0.12f, 0.18f, 0.24f), 0.45f);
            var caseBottom = MeshFactory.Box("CaseBottom", Suitcase, new Vector3(0, 0.1f, 0), new Vector3(0.75f, 0.2f, 0.5f), luggage, true);
            var lining = Mats.Lit(Corrupted ? new Color(0.62f, 0.63f, 0.64f) : new Color(0.8f, 0.77f, 0.7f), 0.15f, 0f, "fabric_linen");
            if (Models.Spawn("Suitcase", Suitcase, Models.Slots(("TS_Plastic", luggage), ("TS_Metal", metal), ("TS_Fabric", lining), ("TS_FabricDark", fabricDark))) != null)
            {
                Models.StripVisual(caseBottom);
                var shirt = Mats.Lit(Corrupted ? new Color(0.7f, 0.71f, 0.72f) : new Color(0.86f, 0.84f, 0.8f), 0.15f, 0f, "fabric_linen");
                MeshFactory.Box("Clothes", Suitcase, new Vector3(0f, 0.055f, 0f), new Vector3(0.66f, 0.09f, 0.42f), fabricDark, false);
                MeshFactory.Box("Clothes", Suitcase, new Vector3(0.015f, 0.125f, -0.01f), new Vector3(0.62f, 0.05f, 0.4f), fabric, false, 1f, Quaternion.Euler(0f, 2f, 0f));
                MeshFactory.Box("Clothes", Suitcase, new Vector3(-0.01f, 0.175f, 0.005f), new Vector3(0.6f, 0.05f, 0.38f), shirt, false, 1f, Quaternion.Euler(0f, -1.5f, 0f));
            }
            else MeshFactory.Box("CaseLid", Suitcase, new Vector3(0, 0.38f, 0.27f), new Vector3(0.75f, 0.5f, 0.06f), luggage, false, 1f, Quaternion.Euler(-15, 0, 0));

            // Phone on the table.
            PhoneRoot = PropLibrary.Phone(g, out PhoneScreen).transform;
            PhoneRoot.localPosition = new Vector3(-1.05f, 0.77f, 0.75f);
            PhoneRoot.localRotation = Quaternion.Euler(0, 20, 0);

            // Reflection: a corridor that exists only in the window glass (ghost geometry beyond the glass).
            ReflectionMat = Mats.UnlitInstance(new Color(0.75f, 0.75f, 0.7f, 0f), Tex.Albedo("plaster_warm"), true, 1f);
            ReflectionCorridor = new GameObject("ReflectionCorridor");
            ReflectionCorridor.transform.SetParent(g, false);
            for (int i = 0; i < 6; i++)
            {
                float d = 4.8f + i * 1.2f;
                MeshFactory.Quad("CorridorL", ReflectionCorridor.transform, 1.2f, 2.6f, ReflectionMat, new Vector3(d, 1.3f, -1.1f), Quaternion.Euler(0, 90, 0));
                MeshFactory.Quad("CorridorR", ReflectionCorridor.transform, 1.2f, 2.6f, ReflectionMat, new Vector3(d, 1.3f, 0.1f), Quaternion.Euler(0, -90, 0));
            }
            ReflectionFigureMat = Mats.UnlitInstance(new Color(0.03f, 0.035f, 0.04f, 0f), Tex.SoftDot, true, 1f);
            ReflectionFigure = MeshFactory.Quad("ReflectionFigure", ReflectionCorridor.transform, 0.5f, 1.9f, ReflectionFigureMat, new Vector3(10.5f, 1.25f, -0.5f), Quaternion.Euler(0, 90, 0));
            MeshFactory.NoShadows(ReflectionCorridor);

            // Set dressing: feature wall, media wall, kitchen, living room, entry, curtains, ceiling, balcony.
            BuildHomeDetail(g, wall, wood, fabric, metal, black, rug, tile);

            // Seams (only meaningful in the Margin copy; harmless in the real one since tears are disabled there).
            AddSeam("window", new Vector3(4.2f, 0f, -0.5f), -90f);
            AddSeam("kitchen", new Vector3(-0.2f, 0f, 1.9f), 30f, 1.35f);
            AddSeam("hall", new Vector3(-3.9f, 0f, 1.2f), 90f);
            if (!Corrupted) foreach (var s in Seams) s.Revealed = false;

            // UAE trace: the genuine handwritten note (the copy could not forge handwriting). It stays hidden now: in the
            // Margin copy the seal is won by the whack-a-ghost game, ghosts popping out of the furniture (Cells/GhostWhack.cs).
            Trace = TraceMark.Create(transform, CountryId.UAE, new Vector3(1.0f, 1.25f, 3.24f), Quaternion.identity, 0.24f, false);
            Trace.transform.localRotation = Quaternion.Euler(0, 180, 0);
            if (Corrupted) Ghosts = GhostWhack.Build(this);

            SetArrival(new Vector3(-2.8f, 0f, -2.4f), 60f);
            AddBounds(new Vector3(0, 0, -0.5f), new Vector3(8.8f, 3f, 9f));

            Expand_Interiors.UaePostcards(this); // the Margin copy is the UAE: three postcards (Cells/Expand_Interiors.cs)
            if (Corrupted) SetEncounter(gameObject.AddComponent<UaeEncounter>());
            else SetEncounter(gameObject.AddComponent<HomeEncounter>());
        }

        public static ApartmentCell Make(GameObject go, bool corrupted)
        {
            var c = go.AddComponent<ApartmentCell>();
            c.Corrupted = corrupted;
            c.Id = corrupted ? CellId.ApartmentCorrupt : CellId.ApartmentNormal;
            return c;
        }

        void Update()
        {
            if (Mode != CellMode.Active || !Corrupted) return;
            // The skyline separates into layers (scenery only; the horizon under the player never moves).
            for (int i = 0; i < SkylineLayers.Count; i++)
            {
                float t = Mathf.Sin(Time.time * 0.05f + i) * 0.5f + 0.5f;
                SkylineLayers[i].localPosition = new Vector3(90f + i * 70f, -30f + t * (i + 1) * 6f, Mathf.Sin(Time.time * 0.03f * (i + 1)) * 12f * i);
            }
        }
    }

    /// <summary>The real apartment: used for the ending (the homecoming chapter drives it directly).</summary>
    public class HomeEncounter : Encounter
    {
        public override string Objective => "";
    }
}
