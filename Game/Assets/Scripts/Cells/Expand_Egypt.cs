using UnityEngine;

namespace TwelfthStamp
{
    /// <summary>
    /// Egypt grows an oasis camp just outside the court's open east side (the expansion pass: "expand the country maps ...
    /// more interactive animative and cool"). A palm-ringed pool with reeds, a mud-brick well whose bucket you can lower
    /// and draw up again, a dark goat-hair tent with rugs and a hanging brass lantern, doves at the water and sparrows in
    /// the camp, pennants in the wind, two lamps to light, and three postcards: at the well, in the tent, and on top of the
    /// dune that looks at the pyramids. Everything sits east of x = 9, clear of the court, the seams, the puzzle and the
    /// walk north to the pyramids.
    /// </summary>
    public static class Expand_Egypt
    {
        public static void Build(EgyptCell cell, Transform parent)
        {
            var root = new GameObject("Expand_Egypt").transform;
            root.SetParent(cell.transform, false);

            var stone = Mats.Lit(new Color(0.86f, 0.76f, 0.6f), 0.2f, 0f, "plaza_stone");
            var mud = Mats.Lit(new Color(0.84f, 0.68f, 0.52f), 0.12f, 0f, "sandstone_blocks");
            var wood = Mats.Lit(new Color(1f, 0.9f, 0.8f), 0.35f, 0f, "wood_dark");
            var tentCloth = Mats.Lit(new Color(0.5f, 0.36f, 0.28f), 0.15f, 0f, "fabric_linen");
            var stripe = Mats.Lit(new Color(0.95f, 0.88f, 0.75f), 0.15f, 0f, "fabric_linen");
            var rugRed = Mats.Lit(new Color(0.78f, 0.3f, 0.22f), 0.12f, 0f, "carpet_rug");
            var rugBlue = Mats.Lit(new Color(0.34f, 0.4f, 0.66f), 0.12f, 0f, "carpet_rug");
            var cushion = Mats.Lit(new Color(0.85f, 0.62f, 0.3f), 0.15f, 0f, "fabric_linen");
            var brass = Mats.Lit(new Color(0.88f, 0.68f, 0.38f), 0.72f, 0.92f);
            var clay = Mats.Lit(new Color(0.74f, 0.44f, 0.3f), 0.25f);
            var reed = Mats.Lit(new Color(0.5f, 0.6f, 0.3f), 0.25f);
            var rope = Mats.Lit(new Color(0.72f, 0.6f, 0.42f), 0.2f);
            var trunk = Mats.Lit(new Color(1f, 0.9f, 0.8f), 0.15f, 0f, "wood_dark");
            var frond = Mats.Lit(new Color(0.45f, 0.62f, 0.3f), 0.25f, 0f, "fabric_linen");
            var deadFrond = Mats.Lit(new Color(0.85f, 0.7f, 0.45f), 0.15f, 0f, "fabric_linen");
            var dates = Mats.Lit(new Color(0.58f, 0.3f, 0.09f), 0.45f);
            var pennant = Mats.Lit(new Color(0.95f, 0.38f, 0.26f), 0.2f, 0f, "fabric_linen");
            var pennant2 = Mats.Lit(new Color(0.3f, 0.5f, 0.75f), 0.2f, 0f, "fabric_linen");

            // Packed ground for the camp (2 cm proud of the sand, walkable).
            MeshFactory.Floor("CampGround", root, new Vector3(15.5f, 0f, 6.2f), new Vector2(12f, 11.5f), stone, "sand", 0.2f, 1.6f);

            // A low mud-brick wall half round the camp, open towards the court.
            var k = new KitBatch();
            foreach (var (c, s) in new[] { (new Vector3(21.6f, 0.45f, 6.2f), new Vector3(0.45f, 0.9f, 10.5f)),
                                          (new Vector3(17.4f, 0.4f, 11.9f), new Vector3(8.6f, 0.8f, 0.45f)),
                                          (new Vector3(18.6f, 0.35f, 0.5f), new Vector3(6.2f, 0.7f, 0.45f)) })
            {
                k.Box(mud, c, s, 1.5f);
                var col = new GameObject("WallCollider").AddComponent<BoxCollider>();
                col.transform.SetParent(root, false);
                col.transform.localPosition = c;
                col.size = s;
            }
            for (int i = 0; i < 9; i++) // crenels along the far wall
                k.Box(mud, new Vector3(21.6f, 1.0f, 1.6f + i * 1.15f), new Vector3(0.5f, 0.22f, 0.5f), 1.5f);

            // The pool: a stone rim too high to step over, water just under its lip, reeds at the corners, palms round it.
            var poolC = new Vector3(17.2f, 0f, 8.4f);
            Vector2 poolSize = new Vector2(5f, 3.2f);
            foreach (var (c, s) in new[] { (new Vector3(0f, 0.24f, poolSize.y / 2f + 0.15f), new Vector3(poolSize.x + 0.6f, 0.48f, 0.3f)),
                                          (new Vector3(0f, 0.24f, -poolSize.y / 2f - 0.15f), new Vector3(poolSize.x + 0.6f, 0.48f, 0.3f)),
                                          (new Vector3(poolSize.x / 2f + 0.15f, 0.24f, 0f), new Vector3(0.3f, 0.48f, poolSize.y)),
                                          (new Vector3(-poolSize.x / 2f - 0.15f, 0.24f, 0f), new Vector3(0.3f, 0.48f, poolSize.y)) })
                MeshFactory.Box("PoolRim", root, poolC + c, s, stone, true, 1.2f);
            MeshFactory.Box("PoolBed", root, poolC + new Vector3(0f, 0.02f, 0f), new Vector3(poolSize.x, 0.04f, poolSize.y), mud, false, 1f);
            var water = Kit.Water(root, poolC + new Vector3(0f, 0.34f, 0f), poolSize, new Color(0.16f, 0.34f, 0.36f), 0.94f);
            water.name = "OasisWater";
            var rnd = new System.Random(77);
            foreach (var corner in new[] { new Vector3(-2.3f, 0f, -1.4f), new Vector3(2.3f, 0f, 1.4f), new Vector3(2.2f, 0f, -1.3f) })
                for (int i = 0; i < 9; i++)
                {
                    var p = poolC + corner + new Vector3((float)rnd.NextDouble() * 0.5f - 0.25f, 0.34f, (float)rnd.NextDouble() * 0.5f - 0.25f);
                    float h = 0.6f + (float)rnd.NextDouble() * 0.7f;
                    k.Box(reed, p + Vector3.up * h / 2f, new Vector3(0.025f, h, 0.025f), Quaternion.Euler((float)rnd.NextDouble() * 14f - 7f, 0f, (float)rnd.NextDouble() * 14f - 7f));
                }
            foreach (var (pos, h, seed) in new[] { (new Vector3(14.2f, -0.02f, 11.0f), 7.2f, 21), (new Vector3(20.4f, -0.02f, 10.4f), 8.1f, 22),
                                                  (new Vector3(20.2f, -0.02f, 6.0f), 6.6f, 23), (new Vector3(13.6f, -0.02f, 6.4f), 7.6f, 24) })
                MonumentKit.Palm(root, pos, h, seed, trunk, frond, deadFrond, dates);

            // The well (set piece).
            BuildWell(root, new Vector3(12.4f, 0f, 9.6f), mud, wood, rope, brass);

            // The tent: a dark ridge tent open at the front, rugs and cushions, a low table with a brass pot.
            var tentC = new Vector3(16.6f, 0f, 2.6f);
            var tent = new GameObject("Tent").transform;
            tent.SetParent(root, false);
            tent.localPosition = tentC;
            tent.localRotation = Quaternion.Euler(0f, 0f, 0f);
            float tw = 4.2f, td = 3.2f, th = 2.4f;
            MeshFactory.Box("RoofL", tent, new Vector3(-tw * 0.25f, th - 0.42f, 0f), new Vector3(tw * 0.56f, 0.04f, td), tentCloth, false, 1f, Quaternion.Euler(0f, 0f, 22f));
            MeshFactory.Box("RoofR", tent, new Vector3(tw * 0.25f, th - 0.42f, 0f), new Vector3(tw * 0.56f, 0.04f, td), tentCloth, false, 1f, Quaternion.Euler(0f, 0f, -22f));
            MeshFactory.Box("Back", tent, new Vector3(0f, (th - 0.5f) / 2f, td / 2f), new Vector3(tw, th - 0.5f, 0.04f), tentCloth, true, 1f);
            MeshFactory.Box("SideL", tent, new Vector3(-tw / 2f, 0.85f, td * 0.15f), new Vector3(0.04f, 1.7f, td * 0.7f), tentCloth, true, 1f);
            MeshFactory.Box("SideR", tent, new Vector3(tw / 2f, 0.85f, td * 0.15f), new Vector3(0.04f, 1.7f, td * 0.7f), tentCloth, true, 1f);
            MeshFactory.Box("StripeL", tent, new Vector3(-tw * 0.25f, th - 0.4f, -td / 2f + 0.3f), new Vector3(tw * 0.56f, 0.045f, 0.12f), stripe, false, 1f, Quaternion.Euler(0f, 0f, 22f));
            MeshFactory.Box("StripeR", tent, new Vector3(tw * 0.25f, th - 0.4f, -td / 2f + 0.3f), new Vector3(tw * 0.56f, 0.045f, 0.12f), stripe, false, 1f, Quaternion.Euler(0f, 0f, -22f));
            foreach (var z in new[] { -td / 2f + 0.05f, td / 2f - 0.05f })
                MeshFactory.Cylinder("Pole", tent, new Vector3(0f, 0f, z), 0.04f, th, wood, 8);
            MeshFactory.Box("Rug1", tent, new Vector3(-0.7f, 0.025f, 0.4f), new Vector3(1.6f, 0.02f, 2.2f), rugRed, false, 1f);
            MeshFactory.Box("Rug2", tent, new Vector3(0.9f, 0.022f, 0.2f), new Vector3(1.4f, 0.02f, 2f), rugBlue, false, 1f, Quaternion.Euler(0f, 8f, 0f));
            foreach (var (c, r) in new[] { (new Vector3(-1.5f, 0.12f, 1.0f), 0f), (new Vector3(-1.5f, 0.12f, 0.1f), 6f), (new Vector3(1.6f, 0.12f, 1.1f), -4f), (new Vector3(0.2f, 0.12f, 1.35f), 90f) })
                MeshFactory.Box("Cushion", tent, c, new Vector3(0.75f, 0.22f, 0.5f), cushion, false, 1f, Quaternion.Euler(0f, r, 0f));
            MeshFactory.Box("LowTable", tent, new Vector3(0.1f, 0.18f, 0.35f), new Vector3(0.8f, 0.05f, 0.6f), wood, true, 1f);
            foreach (var x in new[] { -0.25f, 0.45f }) foreach (var z in new[] { 0.1f, 0.6f })
                MeshFactory.Box("Leg", tent, new Vector3(x, 0.08f, z), new Vector3(0.04f, 0.16f, 0.04f), wood, false);
            var pot = MeshFactory.Sphere("TeaPot", tent, new Vector3(0.05f, 0.29f, 0.35f), 0.08f, brass, 12, 8);
            pot.transform.localScale = new Vector3(1f, 0.85f, 1f);
            MeshFactory.Box("Spout", tent, new Vector3(0.15f, 0.31f, 0.35f), new Vector3(0.12f, 0.02f, 0.02f), brass, false, 1f, Quaternion.Euler(0f, 0f, 25f));
            for (int i = 0; i < 3; i++)
                MeshFactory.Cylinder("Glass", tent, new Vector3(0.3f + i * 0.08f, 0.205f, 0.5f), 0.022f, 0.06f, Mats.Lit(new Color(0.75f, 0.85f, 0.8f), 0.9f), 8, false);
            foreach (var (c, s) in new[] { (new Vector3(1.75f, 0f, -0.9f), 0.32f), (new Vector3(1.95f, 0f, -0.4f), 0.26f), (new Vector3(-1.85f, 0f, -0.8f), 0.3f) })
            {
                var jar = MeshFactory.Sphere("Jar", tent, c + Vector3.up * s, s, clay, 12, 8);
                jar.transform.localScale = new Vector3(1f, 1.25f, 1f);
                MeshFactory.Cylinder("JarNeck", tent, c + Vector3.up * s * 2.1f, s * 0.35f, s * 0.35f, clay, 10, false);
            }

            // The brass lantern hangs from the ridge and sways; it is one of the lamps you can light.
            var lanternPivot = new GameObject("LanternPivot").transform;
            lanternPivot.SetParent(tent, false);
            lanternPivot.localPosition = new Vector3(0f, th - 0.05f, -0.4f);
            MeshFactory.Cylinder("Chain", lanternPivot, new Vector3(0f, -0.45f, 0f), 0.006f, 0.45f, brass, 6, false);
            MeshFactory.Box("LanternCap", lanternPivot, new Vector3(0f, -0.47f, 0f), new Vector3(0.16f, 0.04f, 0.16f), brass, false);
            var lanternGlass = MeshFactory.Box("LanternGlass", lanternPivot, new Vector3(0f, -0.6f, 0f), new Vector3(0.13f, 0.2f, 0.13f), brass, false);
            MeshFactory.Box("LanternBase", lanternPivot, new Vector3(0f, -0.72f, 0f), new Vector3(0.15f, 0.04f, 0.15f), brass, false);
            var lanternLight = new GameObject("LanternLight").AddComponent<Light>();
            lanternLight.transform.SetParent(lanternPivot, false);
            lanternLight.transform.localPosition = new Vector3(0f, -0.6f, 0f);
            lanternLight.type = LightType.Point;
            lanternLight.color = new Color(1f, 0.72f, 0.4f);
            lanternLight.range = 5.5f;
            lanternLight.intensity = 2.4f;
            lanternLight.shadows = LightShadows.None;
            WindSway.Add(lanternPivot, 4f, 2.8f);
            Flicker.Add(lanternLight, 0.12f, 7f);
            SwitchLamp.Attach(tent.gameObject, lanternLight, new[] { lanternGlass.GetComponent<Renderer>() }, new Color(1f, 0.72f, 0.4f), 2.4f, false, root.TransformPoint(tentC));

            // Lamps along the way in and on towards the dunes.
            SwitchLamp.Create(root, new Vector3(10.8f, 0f, 2.6f), new Color(1f, 0.78f, 0.5f));
            SwitchLamp.Create(root, new Vector3(20.8f, 0f, 13.8f), new Color(1f, 0.78f, 0.5f));

            // Pennants on poles at the camp mouth, and a tent flap moving in the wind.
            foreach (var (pos, mat) in new[] { (new Vector3(10.6f, 0f, 5.4f), pennant), (new Vector3(10.6f, 0f, 7.6f), pennant2) })
            {
                MeshFactory.Cylinder("PennantPole", root, pos, 0.035f, 3.2f, wood, 8, true);
                ClothFlag.Create(root, "Pennant", pos + new Vector3(0.03f, 3.15f, 0f), 1.1f, 0.4f, mat, -90f, 0.1f);
            }
            ClothFlag.Create(tent, "Flap", new Vector3(-tw / 2f + 0.02f, 1.75f, -td / 2f + 0.05f), 0.9f, 1.6f, tentCloth, 90f, 0.05f);

            // Life: doves at the water, sparrows in the camp.
            AmbientBirds.Create(root, new Vector3(16.6f, 0.02f, 5.9f), 1.6f, 7, AmbientBirds.Kind.Dove);
            AmbientBirds.Create(root, new Vector3(12.6f, 0.02f, 3.8f), 1.2f, 6, AmbientBirds.Kind.Sparrow);

            k.Bake(root, "Oasis");

            // Postcards: at the well, inside the tent, and on the dune that looks at the pyramids.
            Postcard.Create(cell.transform, CountryId.Egypt, 1, new Vector3(11.1f, 1.2f, 8.9f), "The well answers if you lower the bucket. The water tastes of someone else's morning.");
            Postcard.Create(cell.transform, CountryId.Egypt, 2, new Vector3(15.9f, 1.05f, 3.6f), "Rested here an hour. When I woke, my shadow was facing the wrong way too.");
            Postcard.Create(cell.transform, CountryId.Egypt, 3, new Vector3(14f, 4.05f, 26f), "From up here the pyramids look painted on. I stopped walking toward them.");

            // Where the art review stands: the camp mouth, looking across the pool.
            var shot = new GameObject("ShotPoint").transform;
            shot.SetParent(root, false);
            shot.localPosition = new Vector3(10.2f, 0.02f, 1.6f);
            shot.localRotation = Quaternion.Euler(0f, 40f, 0f);
        }

        static void BuildWell(Transform root, Vector3 c, Material mud, Material wood, Material rope, Material brass)
        {
            var well = new GameObject("Well").transform;
            well.SetParent(root, false);
            well.localPosition = c;
            MeshFactory.Cylinder("WellWall", well, Vector3.zero, 0.62f, 0.78f, mud, 18, true, 1.2f);
            MeshFactory.Cylinder("WellLip", well, new Vector3(0f, 0.78f, 0f), 0.66f, 0.08f, mud, 18, false, 1.2f);
            MeshFactory.Cylinder("WellDark", well, new Vector3(0f, 0.55f, 0f), 0.5f, 0.02f, Mats.Lit(new Color(0.03f, 0.05f, 0.05f), 0.95f), 16, false);
            foreach (var x in new[] { -0.7f, 0.7f })
                MeshFactory.Box("Post", well, new Vector3(x, 0.95f, 0f), new Vector3(0.1f, 1.9f, 0.1f), wood, true);
            var axle = new GameObject("Axle").transform;
            axle.SetParent(well, false);
            axle.localPosition = new Vector3(0f, 1.62f, 0f);
            var drum = new GameObject("DrumPivot").transform; // the windlass drum lies along X
            drum.SetParent(axle, false);
            drum.localPosition = new Vector3(-0.55f, 0f, 0f);
            drum.localRotation = Quaternion.Euler(0f, 0f, -90f);
            MeshFactory.Cylinder("Drum", drum, Vector3.zero, 0.08f, 1.1f, wood, 10, false);
            MeshFactory.Box("Crank", axle, new Vector3(0.75f, -0.12f, 0f), new Vector3(0.04f, 0.26f, 0.04f), wood, false);
            MeshFactory.Box("Handle", axle, new Vector3(0.82f, -0.24f, 0f), new Vector3(0.16f, 0.04f, 0.04f), wood, false);
            var ropeT = MeshFactory.Cylinder("Rope", well, new Vector3(0f, 0.95f, 0f), 0.012f, 0.6f, rope, 6, false).transform;
            var bucket = new GameObject("Bucket").transform;
            bucket.SetParent(well, false);
            bucket.localPosition = new Vector3(0f, 0.95f, 0f);
            MeshFactory.Cylinder("Pail", bucket, new Vector3(0f, -0.22f, 0f), 0.13f, 0.22f, wood, 12, false);
            MeshFactory.Box("Bail", bucket, new Vector3(0f, 0.02f, 0f), new Vector3(0.26f, 0.015f, 0.015f), brass, false);
            var waterTop = MeshFactory.Cylinder("Filled", bucket, new Vector3(0f, -0.04f, 0f), 0.115f, 0.005f, Mats.Lit(new Color(0.2f, 0.42f, 0.45f), 0.95f), 12, false);
            waterTop.SetActive(false);
            var w = well.gameObject.AddComponent<OasisWell>();
            w.Setup(axle, ropeT, bucket, waterTop);
            var trig = new GameObject("WellUse").AddComponent<SphereCollider>();
            trig.transform.SetParent(well, false);
            trig.transform.localPosition = new Vector3(0f, 1.1f, 0f);
            trig.radius = 0.55f;
            trig.isTrigger = true;
            // The interactable sits on the trigger so the solid wall keeps its own layer.
            var use = trig.gameObject.AddComponent<Interactable>();
            use.Prompt = "Draw water from the well";
            use.MaxDistance = 2.4f;
            use.OnUse = _ => w.Draw();
        }
    }

    /// <summary>The oasis well: the crank turns, the bucket drops into the dark with a splash and comes up full, dripping.</summary>
    public class OasisWell : MonoBehaviour
    {
        Transform m_Axle, m_Rope, m_Bucket;
        GameObject m_Water;
        ParticleSystem m_Splash;
        bool m_Busy;
        const float Top = 0.95f, Bottom = 0.25f;

        /// <summary>Times the bucket has been drawn (tests).</summary>
        public int Draws { get; private set; }

        public void Setup(Transform axle, Transform rope, Transform bucket, GameObject water)
        {
            m_Axle = axle; m_Rope = rope; m_Bucket = bucket; m_Water = water;
            m_Splash = MakeSplash();
            SetDepth(Top);
        }

        public void Draw()
        {
            if (!m_Busy) StartCoroutine(Run());
        }

        System.Collections.IEnumerator Run()
        {
            m_Busy = true;
            Draws++;
            m_Water.SetActive(false);
            AudioDirector.I?.Play("drawer_open", transform.position + Vector3.up * 1.4f, 0.7f, 0.75f);
            yield return Move(Top, Bottom, 1.1f, 1f);
            m_Splash.transform.position = m_Bucket.position;
            m_Splash.Emit(28);
            AudioDirector.I?.Play("drop_soft", transform.position + Vector3.up * 0.4f, 0.8f, 0.7f);
            yield return new WaitForSeconds(0.6f);
            m_Water.SetActive(true);
            AudioDirector.I?.Play("drawer_open", transform.position + Vector3.up * 1.4f, 0.75f, 0.65f);
            yield return Move(Bottom, Top, 1.6f, -1f);
            for (int i = 0; i < 6; i++) { m_Splash.transform.position = m_Bucket.position - Vector3.up * 0.25f; m_Splash.Emit(2); yield return new WaitForSeconds(0.25f); }
            m_Busy = false;
        }

        System.Collections.IEnumerator Move(float from, float to, float seconds, float spin)
        {
            float t = 0f;
            while (t < 1f)
            {
                t += Time.deltaTime / seconds;
                float y = Mathf.Lerp(from, to, Mathf.SmoothStep(0f, 1f, t));
                SetDepth(y);
                m_Axle.localRotation *= Quaternion.AngleAxis(spin * 360f * Time.deltaTime / seconds, Vector3.right);
                yield return null;
            }
        }

        void SetDepth(float y)
        {
            m_Bucket.localPosition = new Vector3(0f, y, 0f);
            float len = 1.62f - y;
            m_Rope.localPosition = new Vector3(0f, y, 0f);
            m_Rope.localScale = new Vector3(1f, Mathf.Max(0.05f, len / 0.6f), 1f);
        }

        ParticleSystem MakeSplash()
        {
            var go = new GameObject("Splash");
            go.transform.SetParent(transform, false);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.loop = false;
            main.playOnAwake = false;
            main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.6f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.015f, 0.04f);
            main.startColor = new Color(0.75f, 0.88f, 0.9f, 0.8f);
            main.gravityModifier = 1f;
            main.maxParticles = 80;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var em = ps.emission; em.rateOverTime = 0f;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 25f; shape.radius = 0.08f;
            go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f);
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.renderMode = ParticleSystemRenderMode.Billboard;
            r.sharedMaterial = Mats.Particle(Tex.SoftDot, false, Color.white, 1.2f, Mats.StencilNotEqual);
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return ps;
        }
    }
}
