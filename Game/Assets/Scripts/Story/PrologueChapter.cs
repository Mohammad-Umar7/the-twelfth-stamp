using System.Collections;
using UnityEngine;

namespace TwelfthStamp
{
    /// <summary>
    /// Marrakech, remembered. While Saeed goes through his bag at home, the glasses case brings back the evening he bought
    /// them: the player walks the souk lane at dusk (it paints itself in around them as they go, see MemoryReveal) to the
    /// corner camera shop, looks at the old frames and pays with the festival token; then the memory unmakes itself. Warm
    /// memory grade, no passport, tears or threat; skippable at any time; the camera is never moved for effect.
    /// </summary>
    public static class PrologueChapter
    {
        static bool s_Skip;

        public static IEnumerator Flashback()
        {
            var rig = Game.Rig;
            var c = Game.Campaign;
            var story = Game.Story;
            var homeCell = Game.Cells.Active;
            var homeFeet = rig.Feet;
            float homeYaw = rig.Head.eulerAngles.y;
            s_Skip = false;
            Game.InputLocks++;
            Game.Passport?.Close();
            Hud.I?.SetObjective("", false);

            AudioDirector.I?.Play("pp_seam_shimmer", rig.Head.position + rig.Head.forward, 0.7f);
            yield return Fader.I.FadeOut(1f);

            // The memory: the same shop the player will later see corrupted, in its warm, ordinary evening.
            var shop = (ShopCell)Game.Cells.Activate(CellId.ShopNormal);
            var memory = shop.Lighting.Clone();
            memory.Saturation = -30f;
            memory.ColorFilter = new Color(1f, 0.88f, 0.7f);
            memory.Vignette = 0.42f;
            memory.PostExposure += 0.15f;
            memory.Bloom += 0.2f;
            PostFx.I?.SetPreset(memory);

            var props = new GameObject("FlashbackProps").transform;
            props.SetParent(shop.transform, false);
            var glasses = PropLibrary.Glasses(props, true).transform;
            glasses.position = shop.DisplaySpot.position + Vector3.up * 0.015f;
            glasses.rotation = Quaternion.Euler(-90f, 180f, 0f);
            var caseGo = PropLibrary.GlassesCase(props, true);
            caseGo.transform.position = shop.CounterTop.position + new Vector3(0f, 0f, 0.05f);
            caseGo.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            // The festival token: the player pays with it (pick it up, put it in the brass tray).
            var tokenGo = PropLibrary.Token(props);
            tokenGo.transform.position = shop.CounterTop.position + new Vector3(-0.3f, 0.004f, -0.24f);
            var tokenCol = tokenGo.GetComponent<SphereCollider>();
            if (tokenCol != null) tokenCol.radius = 0.06f; // a coin is a small target: easier to pick up
            var token = PropLibrary.MakeGrabbable(tokenGo, "festival_token", "Take your festival token");
            token.SetHome();
            // Playtest, 18 Oct: "there was no festival token". A 3 cm brass coin on a brass-and-wood counter vanished in the
            // memory grade, so it is a little bigger than a coin and gets a soft teal glow (the guide's "act here" colour)
            // while it is to be paid; the glow moves to the brass tray once the token is in hand.
            tokenGo.transform.localScale = Vector3.one * 1.5f;
            var glow = MakeGlow(props);
            var glowLight = glow.GetComponent<Light>();
            var traySock = new GameObject("TraySocket");
            traySock.transform.SetParent(props, false);
            traySock.transform.position = shop.TraySpot.position + Vector3.up * 0.006f;
            var trayCol = traySock.AddComponent<BoxCollider>();
            trayCol.isTrigger = true; trayCol.size = new Vector3(0.24f, 0.1f, 0.24f);
            var tray = traySock.AddComponent<Socket>();
            tray.Accepts.Add("festival_token");
            tray.LockWhenFilled = true;
            tray.Prompt = "Pay with the festival token";
            bool paid = false;
            tray.Filled += (s, g) => paid = true;
            shop.Recache();
            shop.RefreshProbe();

            var keeper = shop.Keeper;
            Vector3 L(float x, float y, float z) => shop.transform.TransformPoint(new Vector3(x, y, z));
            float LocalZ() => shop.transform.InverseTransformPoint(rig.Feet).z;

            // The walk: the souk lane at dusk paints itself in ahead of the player.
            bool passportWas = Game.Passport != null && Game.Passport.Available;
            bool tearsWas = story.TearsAllowed, threatWas = story.ThreatAllowed, vitalityWas = story.ShowVitality;
            if (Game.Passport != null) Game.Passport.Available = false;
            story.TearsAllowed = false; story.ThreatAllowed = false; story.ShowVitality = false;
            MemoryReveal.Begin(shop.transform);
            rig.PlaceAt(L(0f, 0f, ShopCell.SoukStartZ), shop.transform.eulerAngles.y);
            if (rig is DesktopRig desk0) desk0.SetPitch(-3f);
            Hud.I?.ShowTitleCard("MARRAKECH", "A few days earlier. The last stop before home.", 4.5f);
            Hud.I?.ShowToast(Game.Mode == GameMode.VR ? "Trigger: skip the memory" : Game.Input.Label(Intent.Skip) + ": skip the memory", 4f);
            Game.InputLocks = Mathf.Max(0, Game.InputLocks - 1);
            yield return Fader.I.FadeIn(1.4f);
            Narration.I?.Say("pro_01");
            Hud.I?.SetObjective("Walk down the souk to the camera shop.", false);
            float hint = 0f;
            while (!s_Skip && LocalZ() < shop.DoorPoint.localPosition.z - 0.4f)
            {
                if (SkipPressed()) { s_Skip = true; break; }
                hint += Time.deltaTime;
                if (hint > 30f) { Hud.I?.ShowToast(Game.Input.Label(Intent.Move) + ": walk towards the lit shop at the end of the lane", 4f); hint = 0f; }
                yield return null;
            }

            // Inside: the shopkeeper, the old frames.
            if (!s_Skip)
            {
                if (keeper != null) keeper.Gesture();
                Hud.I?.SetObjective("Look at the old frames in the display case.", false);
                yield return Line("shop_01", keeper != null ? keeper.transform : null, 7f);
                float t = 0f;
                while (!s_Skip && t < 12f && !LookingAt(glasses.position, 3.2f, 22f)) { if (SkipPressed()) s_Skip = true; t += Time.deltaTime; yield return null; }
            }
            if (!s_Skip)
            {
                AudioDirector.I?.Play("glasses_unfold", glasses.position, 0.5f);
                yield return Line("shop_02", keeper != null ? keeper.transform : null, 7f);
                if (keeper != null) keeper.Nod();
            }

            // The purchase: the player puts the festival token in the brass tray; the frames go into their case.
            if (!s_Skip)
            {
                bool vr = Game.Mode == GameMode.VR;
                string use = vr ? "grip" : Game.Input.Label(Intent.Interact);
                Hud.I?.SetObjective("Pay with your festival token: the glowing brass coin on the counter. Put it in the brass tray.", false);
                glow.gameObject.SetActive(true);
                float t = 0f;
                bool hinted = false;
                while (!s_Skip && !paid)
                {
                    if (SkipPressed()) { s_Skip = true; break; }
                    bool held = token.Holder != null;
                    var at = held ? shop.TraySpot.position + Vector3.up * 0.06f : tokenGo.transform.position + Vector3.up * 0.07f;
                    glow.position = at;
                    var toHead = Game.Rig.Head.position - at;
                    if (toHead.sqrMagnitude > 0.0001f) glow.rotation = Quaternion.LookRotation(-toHead.normalized, Vector3.up);
                    float k = 0.5f + 0.5f * Mathf.Sin(Time.time * Mathf.PI * 1.2f);
                    glow.localScale = Vector3.one * (0.85f + 0.3f * k);
                    if (glowLight != null) glowLight.intensity = 0.5f + 0.8f * k;
                    t += Time.deltaTime;
                    if (t > (hinted ? 15f : 6f))
                    {
                        hinted = true;
                        Hud.I?.ShowToast(held ? (vr ? "Let go of the token over the glowing brass tray" : use + " at the glowing brass tray: pay with the token")
                                              : "Your festival token is the small brass coin glowing on the counter. " + (vr ? "Grip it" : use + ": take it"), 5f);
                        t = 0f;
                    }
                    yield return null;
                }
                glow.gameObject.SetActive(false);
            }
            if (!s_Skip)
            {
                Hud.I?.SetObjective("", false);
                AudioDirector.I?.Play("coin_token", shop.TraySpot.position, 0.9f);
                if (keeper != null) keeper.Nod();
                yield return Move(glasses, caseGo.transform.position + Vector3.up * 0.022f, caseGo.transform.rotation * Quaternion.Euler(-90f, 0f, 0f), 1.1f);
                // He lifts the case with the frames in it and holds it out across the counter, then sets it back down.
                if (keeper != null && !s_Skip) keeper.Offer(caseGo.transform, glasses);
                yield return Line("shop_03", keeper != null ? keeper.transform : null, 8f);
                float waitOffer = 0f;
                while (!s_Skip && keeper != null && keeper.Busy && waitOffer < 6f) { if (SkipPressed()) s_Skip = true; waitOffer += Time.deltaTime; yield return null; }
            }
            if (!s_Skip)
            {
                PropLibrary.SetCaseOpen(caseGo, false);
                AudioDirector.I?.Play("case_close", caseGo.transform.position, 0.9f);
                yield return Line("shop_04", null, 5f);
            }
            // The memory unmakes itself around the counter: shelves, wall and souk sink back into the ground.
            if (!s_Skip)
            {
                Game.InputLocks++;
                MemoryReveal.FoldAway(shop.transform, rig.Feet, 2.4f);
                AudioDirector.I?.Play("pp_seam_shimmer", rig.Head.position + rig.Head.forward, 0.6f, 0.7f);
                yield return Wait(2.6f);
                Game.InputLocks = Mathf.Max(0, Game.InputLocks - 1);
            }

            c.SetFlag(StoryFlags.ShopPurchased);
            c.SetFlag(StoryFlags.PrologueDone);
            Narration.I?.StopAll();
            Hud.I?.SetObjective("", false);

            // Back to the present.
            Game.InputLocks++;
            yield return Fader.I.FadeOut(1f);
            MemoryReveal.End(shop.transform);
            UnityEngine.Object.Destroy(props.gameObject);
            if (Game.Passport != null) Game.Passport.Available = passportWas;
            story.TearsAllowed = tearsWas; story.ThreatAllowed = threatWas; story.ShowVitality = vitalityWas;
            Game.Cells.Activate(homeCell != null ? homeCell.Id : CellId.ApartmentNormal);
            Game.Cells.Active.RefreshProbe();
            rig.PlaceAt(homeFeet, homeYaw);
            Game.InputLocks = Mathf.Max(0, Game.InputLocks - 1);
            yield return Fader.I.FadeIn(1.2f);
        }

        /// <summary>A soft pulsing teal light with a glow dot that marks a small thing to act on (the token, then the tray).</summary>
        static Transform MakeGlow(Transform parent)
        {
            var root = new GameObject("ActHereGlow").transform;
            root.SetParent(parent, false);
            var light = root.gameObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 0.45f;
            light.intensity = 0.9f;
            light.color = new Color(0.55f, 0.95f, 0.85f);
            light.shadows = LightShadows.None;
            var mat = new Material(Mats.Particle(Tex.SoftDot, true, new Color(0.7f, 1f, 0.92f), 1.4f)) { name = "ActHereDot" };
            var dot = MeshFactory.Quad("Dot", root, 0.14f, 0.14f, mat, Vector3.zero, Quaternion.identity);
            dot.GetComponent<Renderer>().shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            root.gameObject.SetActive(false);
            return root;
        }

        static bool LookingAt(Vector3 point, float maxDistance, float maxAngle)
        {
            var head = Game.Rig.Head;
            var to = point - head.position;
            return to.magnitude < maxDistance && Vector3.Angle(head.forward, to) < maxAngle;
        }

        /// <summary>Places the player at a fixed vantage looking at a point (desktop also tilts the view).</summary>
        static void Frame(Vector3 feet, Vector3 lookAt)
        {
            var rig = Game.Rig;
            var flat = lookAt - feet; flat.y = 0f;
            float yaw = flat.sqrMagnitude > 0.0001f ? Quaternion.LookRotation(flat).eulerAngles.y : 0f;
            rig.PlaceAt(feet, yaw);
            if (rig is DesktopRig desk)
            {
                float dist = Mathf.Max(0.05f, flat.magnitude);
                desk.SetPitch(Mathf.Atan2(rig.Head.position.y - lookAt.y, dist) * Mathf.Rad2Deg);
            }
        }

        static IEnumerator Cut(System.Action frame)
        {
            yield return Fader.I.FadeOut(0.45f);
            frame();
            yield return Fader.I.FadeIn(0.55f);
        }

        /// <summary>Plays a line and waits for it (or a timeout if the clip is missing), unless the player skips.</summary>
        static IEnumerator Line(string id, Transform speaker, float timeout)
        {
            if (s_Skip) yield break;
            bool done = false;
            Narration.I?.Say(id, speaker, false, () => done = true);
            if (Narration.I == null) done = true;
            float t = 0f;
            while (!done && t < timeout)
            {
                if (SkipPressed()) { s_Skip = true; yield break; }
                t += Time.deltaTime;
                yield return null;
            }
        }

        static IEnumerator Wait(float seconds)
        {
            float t = 0f;
            while (t < seconds) { if (SkipPressed()) { s_Skip = true; yield break; } t += Time.deltaTime; yield return null; }
        }

        static IEnumerator Move(Transform t, Vector3 to, Quaternion rot, float seconds)
        {
            var from = t.position; var r0 = t.rotation;
            for (float k = 0f; k < 1f; )
            {
                if (SkipPressed()) { s_Skip = true; break; }
                k = Mathf.Min(1f, k + Time.deltaTime / seconds);
                float e = Mathf.SmoothStep(0f, 1f, k);
                t.position = Vector3.Lerp(from, to, e) + Vector3.up * Mathf.Sin(e * Mathf.PI) * 0.06f;
                t.rotation = Quaternion.Slerp(r0, rot, e);
                yield return null;
            }
            t.position = to; t.rotation = rot;
        }

        static bool SkipPressed()
        {
            var input = Game.Input;
            if (input == null) return false;
            if (input.Skip.WasPressedThisFrame()) return true;
            return Game.Mode == GameMode.VR && (input.RTrigger.WasPressedThisFrame() || input.LTrigger.WasPressedThisFrame());
        }
    }
}
