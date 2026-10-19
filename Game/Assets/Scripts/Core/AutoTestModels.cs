using System.Collections;
using System.Linq;
using UnityEngine;

namespace TwelfthStamp
{
    /// <summary>
    /// --autotest=models: an art-review pass over the Blender models (Art/Models/MODELS.md). It activates each cell without
    /// its encounter, checks that every model replaced its procedural mesh, and frames each one for a screenshot. Landmarks
    /// are shot from closer than the player gets: once as the game shows them (a fogged silhouette) and once with their
    /// backdrop fog switched off for that shot, so the model itself can be judged. The two props that sit in corners the
    /// scenes leave dark (New York's radio, Berlin's stamp) get a temporary lamp for their shots. Desktop only.
    /// </summary>
    public partial class AutoTest
    {
        IEnumerator ModelsFlow()
        {
            Note("--- Models: every Blender model in place, framed for review ---");
            MenuSystem.I.HideAll();
            var rig = Game.Rig as DesktopRig;
            Check(rig != null, "models gallery runs on the desktop rig");
            if (rig == null) yield break;
            rig.enabled = false; // no gravity or mouse look while the camera is placed by hand

            var menu = Game.Cells.Active;
            ExpectModels(menu, ("Passport_Closed", 1));
            yield return Frame(menu, new Vector3(-0.05f, 1.2f, 1.08f), new Vector3(-0.15f, 0.79f, 1.6f), "m01_menu_passport");

            var apt = (ApartmentCell)Game.Cells.Activate(CellId.ApartmentNormal);
            ExpectModels(apt, ("Sofa", 1), ("DiningTable", 1), ("DiningChair", 4), ("KitchenRun", 1), ("Fridge", 1),
                ("BathroomSink", 1), ("Door", 1), ("Suitcase", 1), ("Phone", 1));
            CheckTiled(apt, "KitchenRun", 4f);
            yield return Frame(apt, new Vector3(2.6f, 1.6f, -1.3f), new Vector3(-1f, 0.8f, 2f), "m02_apartment_living");
            yield return Frame(apt, new Vector3(-0.6f, 1.6f, 1.5f), new Vector3(-1.3f, 0.9f, 3.7f), "m03_apartment_kitchen");
            yield return Frame(apt, new Vector3(0.3f, 1.55f, 2.2f), new Vector3(1.1f, 1.15f, 3.3f), "m04_apartment_fridge_notes");
            yield return Frame(apt, new Vector3(1.2f, 1.5f, 0.6f), new Vector3(1.2f, 0.45f, -2.1f), "m05_apartment_sofa");
            // Hand props on the table, next to the phone.
            var props = new GameObject("ModelReviewProps").transform;
            props.SetParent(apt.transform, false);
            var table = apt.TableTop.position;
            PropLibrary.VintageCamera(props).transform.position = table + new Vector3(-0.45f, 0f, -0.15f);
            PropLibrary.PassportClosed(props).transform.position = table + new Vector3(-0.1f, 0f, -0.2f);
            PropLibrary.Token(props).transform.position = table + new Vector3(0.15f, 0f, -0.25f);
            ExpectModels(props.gameObject, ("VintageCamera", 1), ("Passport_Closed", 1), ("FestivalToken", 1));
            yield return Frame(apt, table + new Vector3(-0.05f, 0.55f, -0.75f), table + new Vector3(0f, 0f, -0.05f), "m06_apartment_table_props");
            Destroy(props.gameObject);
            // The opening shot: camera, passport and glasses case packed in the open bag, seen from where the story starts.
            var bag = new GameObject("ModelReviewBag").transform;
            bag.SetParent(apt.transform, false);
            var s = apt.Suitcase.position;
            PropLibrary.VintageCamera(bag).transform.position = s + new Vector3(-0.18f, 0.22f, 0.02f);
            PropLibrary.PassportClosed(bag).transform.position = s + new Vector3(0.05f, 0.21f, -0.05f);
            PropLibrary.GlassesCase(bag, false).transform.position = s + new Vector3(0.22f, 0.21f, 0.05f);
            yield return Frame(apt, s + new Vector3(0.05f, DesktopRig.EyeHeight, -1.3f), s + new Vector3(0f, 0.15f, 0f), "m07_apartment_bag");
            Destroy(bag.gameObject);
            yield return Frame(apt, new Vector3(-2.3f, 1.6f, -2.4f), new Vector3(-4.48f, 1.05f, -2.6f), "m08_apartment_front_door");
            yield return Frame(apt, new Vector3(2.75f, 1.6f, -4.9f), new Vector3(2.75f, 1.1f, -6.55f), "m09_apartment_bathroom");
            yield return Controllers(apt);

            var margin = Game.Cells.Activate(CellId.ApartmentCorrupt);
            ExpectModels(margin, ("Sofa", 1), ("DiningChair", 4), ("Suitcase", 1));
            yield return Frame(margin, new Vector3(2.6f, 1.6f, -1.3f), new Vector3(-1f, 0.8f, 2f), "m11_apartment_margin");

            var paris = Game.Cells.Activate(CellId.Paris);
            ExpectModels(paris, ("VerificationDesk", 1), ("StreetLamp_Paris", 3), ("Landmark_Eiffel", 1));
            yield return Frame(paris, new Vector3(0.9f, 1.6f, 3.6f), new Vector3(0f, 1f, 6f), "m12_paris_desk");
            yield return Frame(paris, new Vector3(0.4f, 1.6f, 0.4f), new Vector3(3.2f, 2.4f, 2.8f), "m13_paris_lamp");
            yield return Frame(paris, ParisCell.FiringLocal + new Vector3(0f, 1.6f, 0f), ParisCell.FiringLocal + Quaternion.Euler(0f, ParisCell.BubbleYaw, 0f) * Vector3.forward * ParisCell.BubbleRange + Vector3.up * 2f, "m13b_paris_bubbles");
            yield return Landmark(paris, "EiffelSilhouette", 89f, 80f, "m14_paris_eiffel");

            var egypt = (EgyptCell)Game.Cells.Activate(CellId.Egypt);
            ExpectModels(egypt, ("Reflector", 1));
            EgyptGroundToStamp(egypt);
            yield return Frame(egypt, new Vector3(-1.5f, 1.6f, 3.8f), new Vector3(-2.5f, 1.1f, 1.5f), "m15_egypt_reflector");
            yield return EgyptRunReview(egypt); // the temple run: gate, a tomb door and its mummy, a fallen stone, coins, the portal (AutoTestArcadeC.cs)

            var ny = (NewYorkCell)Game.Cells.Activate(CellId.NewYork);
            ExpectModels(ny, ("Noisemaker", 1), ("Landmark_Liberty", 1));
            var radio = ny.Noisemaker.transform.position;
            var radioLight = ReviewLight(ny, radio + new Vector3(0.2f, 0.5f, -0.5f));
            yield return Frame(ny, radio + new Vector3(0.25f, 0.45f, -0.75f), radio + new Vector3(0f, 0.08f, 0f), "m17_newyork_noisemaker");
            Destroy(radioLight);
            yield return Landmark(ny, "LibertySilhouette", 93f, 110f, "m18_newyork_liberty");

            var japan = (JapanCell)Game.Cells.Activate(CellId.Japan);
            ExpectModels(japan, ("Landmark_Pagoda", 1));
            var claw = japan.transform.InverseTransformPoint(japan.Claw.transform.position);
            yield return Frame(japan, claw + new Vector3(0.6f, 1.6f, -2.2f), claw + new Vector3(0f, 1.3f, 0f), "m19_japan_clawmachine");
            yield return Landmark(japan, "PagodaSilhouette", 39f, 50f, "m20_japan_pagoda");
            // The precinct gate seen from the street's lane: an open passage on the pagoda's sight line.
            var pagoda = japan.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == "PagodaSilhouette");
            if (pagoda != null)
            {
                var pp = japan.transform.InverseTransformPoint(pagoda.position);
                var toViewer = new Vector3(-pp.x, 0f, -pp.z).normalized;
                var laneEye = pp + toViewer * 84f;
                laneEye.y = pp.y + 1.6f;
                yield return Frame(japan, laneEye, pp + toViewer * 60f + Vector3.up * 1.2f, "m20b_japan_gate");
            }

            var riyadh = (RiyadhCell)Game.Cells.Activate(CellId.Riyadh);
            ExpectModels(riyadh, ("Landmark_ArchTower", 1));
            var mazeStart = riyadh.Maze.CellCenter(riyadh.Maze.Start);
            yield return Frame(riyadh, mazeStart + new Vector3(0f, 1.6f, -0.6f), mazeStart + new Vector3(0f, 1.4f, 4f), "m21_riyadh_maze");
            yield return Frame(riyadh, riyadh.Maze.PortalLocal + new Vector3(0f, 1.6f, -4.5f), riyadh.Maze.PortalLocal + new Vector3(0f, 1.6f, 0f), "m22_riyadh_portal");
            yield return Landmark(riyadh, "ArchTowerSilhouette", 150f, 130f, "m23_riyadh_archtower");

            var agra = (AgraCell)Game.Cells.Activate(CellId.Agra);
            ExpectModels(agra, ("Landmark_DomedPavilion", 1));
            agra.Puzzle.Scramble();
            var puzzleTable = agra.transform.InverseTransformPoint(agra.Puzzle.transform.position);
            yield return Frame(agra, puzzleTable + new Vector3(0f, 1.65f, -0.9f), puzzleTable + new Vector3(0f, 0.85f, 0.05f), "m24_agra_tajpuzzle");
            yield return Landmark(agra, "DomedPavilion", 65f, 120f, "m25_agra_pavilion");
            // The open garden: the raised tank on the axis, with its steps and the sandstone paths round it.
            yield return Frame(agra, new Vector3(6.5f, 1.6f, 80f), new Vector3(0f, 0.4f, 96f), "m25b_agra_tank");

            var london = Game.Cells.Activate(CellId.London);
            ExpectModels(london, ("Landmark_ClockTower", 1));
            yield return Landmark(london, "ClockTowerSilhouette", 100f, 80f, "m26_london_clocktower");

            var berlin = (BerlinCell)Game.Cells.Activate(CellId.Berlin);
            ExpectModels(berlin, ("VoidStamp", 1), ("Landmark_ColumnGate", 1));
            var stamp = berlin.VoidStamp.transform.position;
            var stampLight = ReviewLight(berlin, stamp + new Vector3(0.2f, 0.55f, 0.4f));
            yield return Frame(berlin, stamp + new Vector3(0.25f, 0.5f, 0.55f), stamp + new Vector3(0f, 0.07f, 0f), "m27_berlin_void_stamp");
            Destroy(stampLight);
            yield return Landmark(berlin, "ColumnGate", 27f, 40f, "m28_berlin_gate");
            yield return SaeedModel();

            rig.enabled = true;
        }

        /// <summary>
        /// Saeed (Saeed.fbx, built by Tools/blender/build_saeed.py): one skinned mesh on an armature with Unity-style bone
        /// names, feet on the origin, facing +Z, about 1.78 m to the top of the skull; turning the Head bone turns the face
        /// and leaves the feet. Framed front, side and face in Egypt's daylight, a few steps ahead of the arrival point
        /// (open ground by design), on his baked textures.
        /// </summary>
        IEnumerator SaeedModel()
        {
            var egypt = Game.Cells.Activate(CellId.Egypt);
            var cellT = egypt.transform;
            var ahead = Vector3.ProjectOnPlane(egypt.Arrival.forward, Vector3.up).normalized;
            var spotW = egypt.Arrival.position + ahead * 2.6f;
            if (Physics.Raycast(spotW + Vector3.up * 3f, Vector3.down, out var floor, 6f, Layers.FloorMask, QueryTriggerInteraction.Ignore))
                spotW = floor.point;
            var court = cellT.InverseTransformPoint(spotW);
            var facing = cellT.InverseTransformDirection(-ahead);            // he faces back toward the arrival point
            facing.y = 0f;
            facing.Normalize();
            var right = Vector3.Cross(Vector3.up, facing);
            var sideW = spotW + Vector3.up * 1.4f;
            if (Physics.Raycast(sideW, cellT.TransformDirection(right), 3f, ~0, QueryTriggerInteraction.Ignore)) right = -right; // the open side
            float yaw = Mathf.Atan2(facing.x, facing.z) * Mathf.Rad2Deg;
            var model = Models.Spawn("Saeed", cellT, Models.SaeedSlots(), court, yaw);
            Check(model != null, "Saeed.fbx loads");
            if (model == null) yield break;
            string[] bones = { "Hips", "Spine", "Chest", "Neck", "Head", "Shoulder_L", "Shoulder_R", "UpperArm_L", "UpperArm_R", "Forearm_L",
                               "Forearm_R", "Hand_L", "Hand_R", "UpperLeg_L", "UpperLeg_R", "LowerLeg_L", "LowerLeg_R", "Foot_L", "Foot_R" };
            var all = model.GetComponentsInChildren<Transform>(true);
            var missing = bones.Where(b => all.All(t => t.name != b)).ToList();
            Check(missing.Count == 0, "Saeed: the armature has the bones the mirror drives" + (missing.Count > 0 ? " (missing " + string.Join(", ", missing) + ")" : " (" + bones.Length + " named, " + all.Length + " transforms)"));
            var smrs = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            Check(smrs.Length == 1, "Saeed: one skinned mesh (" + smrs.Length + ")");
            if (smrs.Length != 1 || missing.Count > 0) yield break;
            var smr = smrs[0];
            var slots = smr.sharedMaterials.Select(m => m != null && m.name.StartsWith("Baked_saeed_") ? m.name.Substring(12) : (m != null ? m.name : "null")).ToList();
            int tris = 0;                                                   // index counts: the mesh is not CPU-readable
            for (int i = 0; i < smr.sharedMesh.subMeshCount; i++) tris += (int)smr.sharedMesh.GetIndexCount(i) / 3;
            Check(slots.Count == 6 && smr.sharedMaterials.All(m => m != null && m.name.StartsWith("Baked_saeed_") && m.GetTexture("_BaseMap") != null && m.GetTextureScale("_BaseMap") == Vector2.one),
                "Saeed: six slots on his baked textures, untiled (" + string.Join(", ", slots) + ")");
            Check(tris >= 20000 && tris <= 35000, "Saeed: " + tris + " triangles (20-35k)");

            // Rest pose, in the model's own space.
            var mesh = new Mesh();
            smr.BakeMesh(mesh, true);
            var toWorld = Matrix4x4.TRS(smr.transform.position, smr.transform.rotation, Vector3.one);
            var local = mesh.vertices.Select(v => model.transform.InverseTransformPoint(toWorld.MultiplyPoint3x4(v))).ToArray();
            float minY = local.Min(v => v.y), maxY = local.Max(v => v.y);
            var head = all.First(t => t.name == "Head");
            var headLocal = model.transform.InverseTransformPoint(head.position);
            var band = local.Select((v, i) => (v, i)).Where(p => Mathf.Abs(p.v.y - (headLocal.y + 0.06f)) < 0.025f && Mathf.Abs(p.v.x) < 0.03f).ToList();
            var nose = band.OrderByDescending(p => p.v.z).First();
            var back = band.OrderBy(p => p.v.z).First();
            Check(Mathf.Abs(minY) < 0.03f && maxY > 1.74f && maxY < 1.9f, "Saeed: feet on the origin, " + maxY.ToString("0.00") + " m to the top of the hair (" + minY.ToString("0.000") + " at the soles)");
            Check(nose.v.z - headLocal.z > headLocal.z - back.v.z, "Saeed: faces +Z (the face " + (nose.v.z - headLocal.z).ToString("0.00") + " m in front of the head bone, the back of the head " + (headLocal.z - back.v.z).ToString("0.00") + " m behind)");
            var low = local.Select((v, i) => (v, i)).OrderBy(p => p.v.y).First();
            var rest = head.localRotation;
            head.Rotate(model.transform.up, 35f, Space.World);
            var turned = new Mesh();
            smr.BakeMesh(turned, true);
            var noseNow = model.transform.InverseTransformPoint(toWorld.MultiplyPoint3x4(turned.vertices[nose.i]));
            var lowNow = model.transform.InverseTransformPoint(toWorld.MultiplyPoint3x4(turned.vertices[low.i]));
            head.localRotation = rest;
            Check(Mathf.Abs(noseNow.x - nose.v.x) > 0.03f && (lowNow - low.v).magnitude < 0.001f,
                "Saeed: turning the Head bone 35 degrees turns the face (the nose moves " + ((noseNow - nose.v).magnitude * 100f).ToString("0") + " cm) and leaves the feet");
            Destroy(mesh);
            Destroy(turned);

            var up = Vector3.up;
            yield return Frame(egypt, court + facing * 2.7f + up * 1.45f, court + up * 0.95f, "m29_saeed_front");
            yield return Frame(egypt, court + right * 2.7f + up * 1.45f, court + up * 0.95f, "m30_saeed_side");
            yield return Frame(egypt, court + facing * 0.62f + right * 0.2f + up * (headLocal.y + 0.04f), court + up * (headLocal.y + 0.03f), "m31_saeed_face");
            Destroy(model);
        }

        /// <summary>
        /// Egypt: solid ground every 25 cm along the straight line from the arrival (the temple run's start) to the stamp spot
        /// in the portal at its far end (no gaps, no step the player cannot take), sand underfoot there, and the spot itself.
        /// </summary>
        void EgyptGroundToStamp(Cell egypt)
        {
            var site = egypt.GetComponentsInChildren<VistaInfo>(true).FirstOrDefault(i => i.HasStampSpot);
            if (site == null) { Check(false, "Egypt: the temple run's portal has a stamp spot"); return; }
            var space = site.transform.parent != null ? site.transform.parent : egypt.transform;
            var spot = space.TransformPoint(site.StampLocal);
            var from = egypt.Arrival.position; // the start of the temple run
            from.y = spot.y;
            // Every 25 cm, so a dune's slope reads as small rises and only a real ledge shows up as a step the player
            // cannot climb (the VR body steps 30 cm, the desktop one 32 cm).
            float length = Vector3.Distance(from, spot);
            int samples = Mathf.CeilToInt(length / 0.25f), gaps = 0;
            float step = 0f, stepAt = 0f, last = float.NaN;
            string underfoot = "";
            for (int i = 0; i <= samples; i++)
            {
                var p = Vector3.Lerp(from, spot, i / (float)samples);
                if (!Physics.Raycast(p + Vector3.up * 6f, Vector3.down, out var hit, 12f, Layers.FloorMask, QueryTriggerInteraction.Ignore)) { gaps++; continue; }
                if (!float.IsNaN(last) && Mathf.Abs(hit.point.y - last) > step) { step = Mathf.Abs(hit.point.y - last); stepAt = egypt.transform.InverseTransformPoint(p).z; }
                last = hit.point.y;
                if (i == samples) underfoot = Surface.Of(hit.collider);
            }
            Check(gaps == 0 && step <= 0.3f, "Egypt: solid ground all the way down the temple run to the stamp spot (" + length.ToString("0") + " m every 25 cm, " + gaps + " gaps, largest rise " + (step * 100f).ToString("0") + " cm at z " + stepAt.ToString("0") + ")");
            Check(underfoot == "sand", "Egypt: sand underfoot at the stamp spot (" + underfoot + ")");
        }

        /// <summary>Both VR controllers held up in front of the camera, with the binding callouts they show when looked at.</summary>
        IEnumerator Controllers(Cell cell)
        {
            yield return Frame(cell, new Vector3(3.6f, 1.6f, -0.8f), new Vector3(1.6f, 1.6f, -0.8f), null);
            var head = Game.Rig.Head;
            foreach (var side in new[] { HandSide.Left, HandSide.Right })
            {
                var grip = new GameObject("ReviewGrip").transform;
                grip.SetParent(cell.transform, false);
                grip.position = head.position + head.forward * 0.3f + Vector3.down * 0.02f;
                grip.rotation = head.rotation * Quaternion.Euler(-60f, side == HandSide.Left ? 20f : -20f, 0f); // face plate toward the eye
                var m = ControllerModel.Create(grip, side);
                Check(m.transform.Find("Model_Controller_" + side) != null, side + " controller model in use");
                string[] ids = { "thumbstick", "primaryButton", "secondaryButton", side == HandSide.Left ? "menu" : "system", "trigger", "grip" };
                Check(ids.All(id => m.Control(id) != m.transform && m.Control(id).GetComponent<Renderer>() != null), side + " controller: all six controls are model buttons");
                yield return new WaitForSeconds(0.5f); // looked at from 0.3 m: the binding callouts appear
                int callouts = m.GetComponentsInChildren<Transform>().Count(t => t.name.StartsWith("Callout_") && t.gameObject.activeInHierarchy);
                Check(callouts >= 4, side + " controller callouts attach to the model's controls (" + callouts + ")");
                yield return Shot("m10_controller_" + side.ToString().ToLowerInvariant());
                var trigger = m.Control("trigger").GetComponent<Renderer>();
                var plain = trigger.sharedMaterial;
                m.SetCallout("trigger", "Use", true);
                bool lit = trigger.sharedMaterial != plain;
                m.ClearCallouts();
                Check(lit && trigger.sharedMaterial == plain, side + " controller: onboarding highlight lights and clears the model trigger");
                Destroy(grip.gameObject);
            }
        }

        /// <summary>A temporary lamp for a prop in a corner the scene leaves too dark to judge (review shots only).</summary>
        GameObject ReviewLight(Cell cell, Vector3 position)
        {
            var l = new GameObject("ReviewLight").AddComponent<Light>();
            l.transform.SetParent(cell.transform, false);
            l.transform.position = position;
            l.type = LightType.Point;
            l.color = new Color(1f, 0.95f, 0.88f);
            l.intensity = 1.6f;
            l.range = 2.5f;
            return l.gameObject;
        }

        /// <summary>Places the eye at <paramref name="eye"/> looking at <paramref name="target"/> (cell-local points), then a screenshot.</summary>
        IEnumerator Frame(Cell cell, Vector3 eye, Vector3 target, string shot)
        {
            var e = cell.transform.TransformPoint(eye);
            var d = cell.transform.TransformPoint(target) - e;
            Game.Rig.PlaceAt(e - Vector3.up * DesktopRig.EyeHeight, Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg);
            ((DesktopRig)Game.Rig).SetPitch(-Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg);
            yield return new WaitForSeconds(0.6f);
            if (shot != null) yield return Shot(shot);
        }

        /// <summary>
        /// A landmark seen from <paramref name="distance"/> metres on the side facing the cell, standing on its vista's ground
        /// and framed to its height.
        /// </summary>
        IEnumerator Landmark(Cell cell, string root, float height, float distance, string shot)
        {
            var t = cell.GetComponentsInChildren<Transform>(true).FirstOrDefault(x => x.name == root);
            if (t == null) { Check(false, cell.Id + ": landmark " + root + " found"); yield break; }
            var site = t.GetComponent<VistaInfo>();
            int parts = t.GetComponentsInChildren<Renderer>(true).Count(r => r.name.StartsWith("Vista_") || r.name == "Dune");
            Check(site != null && parts >= 8, cell.Id + ": " + root + " stands in its vista (" + parts + " scenery meshes)");
            if (site != null && site.HasStampSpot)
            {
                // Open world: the ground under the stamp spot is solid, and the spot is only offered once the cell opens up.
                var space = t.parent != null ? t.parent : cell.transform;
                var spot = space.TransformPoint(site.StampLocal);
                bool solid = Physics.Raycast(spot + Vector3.up * 0.5f, Vector3.down, out var hit, 4f, Layers.FloorMask, QueryTriggerInteraction.Ignore);
                Check(solid, cell.Id + ": walkable ground under its stamp spot" + (solid ? " (" + (spot.y - hit.point.y).ToString("0.0") + " m below the stamp)" : ""));
                bool opened = site.Reachable; // the cell opens its own route (Landmarks.MarkReachable) once it exists
                bool offeredNow = Landmarks.TryStampSpot(cell, out var offered);
                Check(offeredNow == opened && (!opened || Vector3.Distance(offered, spot) < 0.01f),
                    cell.Id + (opened ? ": its route is open and the stamp spot is offered" : ": its stamp spot stays off until the cell opens a route"));
                Landmarks.MarkReachable(cell, false);
                bool off = !Landmarks.TryStampSpot(cell, out _);
                Landmarks.MarkReachable(cell, true);
                bool on = Landmarks.TryStampSpot(cell, out var again) && Vector3.Distance(again, spot) < 0.01f;
                Landmarks.MarkReachable(cell, opened); // leave the cell as it opened itself
                Check(off && on, cell.Id + ": MarkReachable turns its stamp spot off and on");
            }
            var p = cell.transform.InverseTransformPoint(t.position);
            var toCell = new Vector3(-p.x, 0f, -p.z).normalized;
            var eye = new Vector3(p.x, site != null ? site.GroundY : 0f, p.z) + toCell * distance + Vector3.up * 1.6f;
            yield return Frame(cell, eye, p + Vector3.up * height * 0.42f, shot);
            var mats = t.GetComponentsInChildren<Renderer>(true).SelectMany(r => r.sharedMaterials).Where(m => m != null && m.HasProperty("_FogDensity")).Distinct().ToList();
            var fog = mats.Select(m => m.GetFloat("_FogDensity")).ToList();
            foreach (var m in mats) m.SetFloat("_FogDensity", 0f);
            yield return Shot(shot + "_nofog");
            for (int i = 0; i < mats.Count; i++) mats[i].SetFloat("_FogDensity", fog[i]);
        }

        void ExpectModels(Cell cell, params (string model, int count)[] expected) => ExpectModels(cell.gameObject, expected);

        void ExpectModels(GameObject root, params (string model, int count)[] expected)
        {
            var names = root.GetComponentsInChildren<Transform>(true).Select(t => t.name).ToList();
            foreach (var (model, count) in expected)
            {
                int n = names.Count(x => x == "Model_" + model);
                Check(n == count, root.name + ": " + model + " model in use (" + n + "/" + count + ")");
            }
        }

        /// <summary>Big smart-UV meshes must have their tiling textures scaled to the world (UvDensity from the import rule).</summary>
        void CheckTiled(Cell cell, string model, float atLeast)
        {
            var m = cell.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "Model_" + model);
            float best = 0f;
            if (m != null)
                foreach (var r in m.GetComponentsInChildren<Renderer>(true))
                    foreach (var mat in r.sharedMaterials)
                        if (mat != null && mat.HasProperty("_BaseMap") && mat.GetTexture("_BaseMap") != null) best = Mathf.Max(best, mat.GetTextureScale("_BaseMap").x);
            Check(best >= atLeast, cell.Id + ": " + model + " textures tiled to world scale (x" + best.ToString("0.0") + ")");
        }
    }
}
