using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

namespace TwelfthStamp
{
    /// <summary>
    /// The escape and the real home: a near miss at the threshold, a safe virtual glasses-removal gesture (never the
    /// real headset), normal sound returning, breaking the glasses without striking anything, retreating to the
    /// bathroom, a short comfortably framed epilogue, then results and credits.
    /// </summary>
    public static class EndingChapter
    {
        public static IEnumerator Run()
        {
            var c = Game.Campaign;
            var rig = Game.Rig;
            Game.InputLocks++;
            Narration.I?.StopAll();
            Game.Passport?.Close();

            // 1) The near miss. Darkness, its fingers stop a hand's width from your face.
            AudioDirector.I?.SetAmbience(null, 0.4f);
            AudioDirector.I?.SetMusic(null, 0.4f);
            AudioDirector.I?.Play("pass_sting", rig.Head.position + rig.Head.forward, 0.9f, 1f, Bus.SFX, true);
            Fader.I.FadeTo(0.82f, 0.25f);
            var hand = new GameObject("NearMissAnchor").transform;
            var fingers = PassengerHand.Create(hand);
            Game.Passenger?.Despawn();
            Game.Tears.ForceReset();
            var heart = AudioDirector.I?.Loop("heartbeat_loop", rig.Head, 0.7f, Bus.SFX, 0f);
            Narration.I?.Say("fin_02", null, false, null, true);

            // The removal gesture. Desktop: hold E or G. VR: take the glasses floating in front of you and pull them away.
            GameObject proxy = null;
            Grabbable proxyGrab = null;
            if (Game.Mode == GameMode.VR)
            {
                proxy = PropLibrary.Glasses(Game.Root, false);
                proxy.transform.position = rig.Head.position + rig.Forward * 0.32f + Vector3.down * 0.08f;
                proxy.transform.rotation = Quaternion.LookRotation(rig.Forward, Vector3.up);
                proxy.AddComponent<BoxCollider>().size = new Vector3(0.16f, 0.06f, 0.08f);
                proxyGrab = PropLibrary.MakeGrabbable(proxy, "glasses", "Take the glasses off");
                proxyGrab.UsePhysicsOnRelease = false;
                Game.InputLocks = Mathf.Max(0, Game.InputLocks - 1); // hands must work for the gesture
            }
            Hud.I?.ShowToast(Game.Mode == GameMode.VR ? "Grab the glasses in front of you and pull them away" : "Hold E (or G) to take the glasses off", 6f);
            float held = 0f, t = 0f;
            bool removed = false;
            var fingerTip = new GameObject("FingerTip").transform;
            while (!removed)
            {
                t += Time.deltaTime;
                // The fingers creep closer, then stop just short (they never touch).
                float approach = Mathf.Clamp01(t / 3.5f);
                fingerTip.position = rig.Head.position + rig.Forward * Mathf.Lerp(0.9f, 0.24f, approach) + Vector3.down * 0.05f;
                if (Game.Mode == GameMode.Desktop)
                {
                    bool key = (Keyboard.current != null && (Keyboard.current.eKey.isPressed || Keyboard.current.gKey.isPressed)) || Game.IsAutoTest;
                    held = key ? held + Time.deltaTime : Mathf.Max(0f, held - Time.deltaTime * 2f);
                    Hud.I?.ShowTear(Mathf.Clamp01(held / 1.2f), 0f, "Taking the glasses off...");
                    if (held >= 1.2f) removed = true;
                }
                else if (proxyGrab != null)
                {
                    if (proxyGrab.Holder != null && Vector3.Distance(proxy.transform.position, rig.Head.position) > 0.42f) removed = true;
                }
                if (t > 1.5f && fingers != null && !fingers.gameObject.activeSelf)
                {
                    fingers.gameObject.SetActive(true);
                    fingers.transform.position = fingerTip.position;
                    fingers.transform.rotation = Quaternion.LookRotation(-rig.Forward, Vector3.up);
                }
                if (fingers != null && fingers.gameObject.activeSelf)
                {
                    fingers.transform.position = Vector3.Lerp(fingers.transform.position, fingerTip.position, Time.deltaTime * 3f);
                    fingers.transform.rotation = Quaternion.LookRotation(rig.Head.position - fingers.transform.position, Vector3.up);
                }
                yield return null;
            }

            // Connection broken: silence, then the real world.
            if (proxy != null) Object.Destroy(proxy);
            if (Game.Mode == GameMode.VR) Game.InputLocks++;
            if (heart != null) AudioDirector.I?.FadeOutAndStop(heart, 0.1f);
            AudioDirector.I?.Play("glasses_unfold", rig.Head.position, 0.8f);
            PostFx.I?.Flash(1f);
            Fader.I.FadeTo(1f, 0.15f);
            yield return new WaitForSeconds(0.9f);
            Object.Destroy(fingerTip.gameObject);
            if (fingers != null) Object.Destroy(fingers.gameObject);
            Object.Destroy(hand.gameObject);
            c.SetFlag(StoryFlags.Escaped);
            Game.SaveCheckpoint(CellId.ApartmentNormal);

            // 2) Real home: normal audio resumes.
            var home = (ApartmentCell)Game.Cells.Activate(CellId.ApartmentNormal);
            home.RefreshProbe();
            rig.PlaceAt(home.TableTop.position + new Vector3(0.2f, -0.77f, -1.1f), 0f);
            StoryDirector.I.TearsAllowed = false;
            StoryDirector.I.ThreatAllowed = false;
            StoryDirector.I.ShowVitality = false;
            c.vitality = 1f;
            var hum = AudioDirector.I?.Loop("fridge_hum_loop", home.FridgeRoot, 0.5f, Bus.Ambience, 1f, 1f, 8f);
            AudioDirector.I?.SetMusic("mus_home", 4f, 0.6f, false);
            Fader.I.FadeTo(0f, 2f);
            Game.InputLocks = Mathf.Max(0, Game.InputLocks - 1);
            yield return new WaitForSeconds(1.2f);
            Narration.I?.Say("end_01");

            // Put the glasses on the table, then break them (no striking anything real).
            var props = new GameObject("EndingProps").transform;
            props.SetParent(home.transform, false);
            var glassesGo = PropLibrary.Glasses(props, true);
            glassesGo.AddComponent<BoxCollider>().size = new Vector3(0.15f, 0.06f, 0.06f);
            var glasses = PropLibrary.MakeGrabbable(glassesGo, "glasses", "Hold the glasses");
            rig.MainHand.Grab(glasses);
            var spotGo = new GameObject("GlassesSpot");
            spotGo.transform.SetParent(props, false);
            spotGo.transform.position = home.TableTop.position + new Vector3(0.1f, 0.0f, -0.15f);
            var sc = spotGo.AddComponent<BoxCollider>(); sc.isTrigger = true; sc.size = new Vector3(0.3f, 0.12f, 0.25f); sc.center = new Vector3(0, 0.05f, 0);
            var spot = spotGo.AddComponent<Socket>();
            spot.Accepts.Add("glasses");
            spot.LockWhenFilled = true;
            spot.Prompt = "Put the glasses down";
            home.Recache();
            Hud.I?.SetObjective("Put the glasses down on the table.", false);
            if (Game.IsAutoTest) { yield return new WaitForSeconds(0.5f); rig.MainHand.PlaceInto(spot); }
            while (spot.Current == null) yield return null;
            var breakIt = new GameObject("BreakGlasses");
            breakIt.transform.SetParent(spotGo.transform, false);
            var bcol = breakIt.AddComponent<BoxCollider>(); bcol.isTrigger = true; bcol.size = new Vector3(0.22f, 0.1f, 0.12f); bcol.center = new Vector3(0, 0.03f, 0);
            var breaker = breakIt.AddComponent<Interactable>();
            breaker.Prompt = "Break them";
            bool broken = false;
            breaker.OnUse = _ => broken = true;
            Hud.I?.SetObjective("Break the glasses.", false);
            if (Game.IsAutoTest) { yield return new WaitForSeconds(0.5f); broken = true; }
            while (!broken) yield return null;
            Object.Destroy(breakIt);
            yield return BreakAnimation(glassesGo.transform, props);
            c.SetFlag(StoryFlags.GlassesBroken);
            Game.SaveNow();

            // Mum calls back: normal life.
            yield return new WaitForSeconds(1.5f);
            AudioDirector.I?.Play("phone_buzz", home.PhoneRoot.position, 0.8f);
            AudioDirector.I?.Play("voicemail_beep", home.PhoneRoot.position, 0.6f);
            Narration.I?.Say("end_02", home.PhoneRoot);

            // 3) Retreat to the bathroom (ordinary movement; nobody is asked to run).
            Hud.I?.SetObjective("Go into the bathroom and close the door.", false);
            if (Game.IsAutoTest) rig.PlaceAt(home.BathroomInside.position, 180f);
            while (Vector3.Distance(new Vector3(rig.Feet.x, 0, rig.Feet.z), new Vector3(home.BathroomInside.position.x, 0, home.BathroomInside.position.z)) > 1.1f) yield return null;
            var doorUse = home.BathroomDoorPivot.GetComponentInChildren<Collider>().gameObject.AddComponent<Interactable>();
            doorUse.Prompt = "Close the door";
            bool closed = false;
            doorUse.OnUse = _ => closed = true;
            if (Game.IsAutoTest) closed = true;
            while (!closed) yield return null;
            Object.Destroy(doorUse);
            AudioDirector.I?.Play("door_close", home.BathroomDoorPivot.position, 0.9f);
            float k = 0f;
            var from = home.BathroomDoorPivot.localRotation;
            while (k < 1f) { k += Time.deltaTime / 0.9f; home.BathroomDoorPivot.localRotation = Quaternion.Slerp(from, Quaternion.identity, Mathf.SmoothStep(0, 1, k)); yield return null; }
            Hud.I?.SetObjective("", false);
            yield return new WaitForSeconds(1.2f);
            Game.InputLocks++;
            yield return Fader.I.FadeOut(1.5f);

            // 4) Epilogue: a fixed, comfortable vantage; nothing moves the player's view.
            rig.PlaceAt(home.TableTop.position + new Vector3(0.1f, -0.77f, -1.25f), 0f);
            if (hum != null) AudioDirector.I?.FadeOutAndStop(hum, 0.1f);
            var shard = MeshFactory.Box("LensFragment", props, Vector3.zero, new Vector3(0.025f, 0.002f, 0.02f), Mats.Glass(new Color(0.2f, 0.28f, 0.26f, 0.85f), 0.98f, new Color(0.05f, 0.18f, 0.15f)), false);
            shard.transform.position = home.TableTop.position + new Vector3(0.18f, 0.003f, -0.2f);
            var tag = PassengerHand.Create(props);
            tag.gameObject.SetActive(true);
            tag.transform.position = shard.transform.position;
            tag.transform.rotation = Quaternion.LookRotation(Vector3.up, Vector3.forward);
            tag.transform.localScale = Vector3.zero;
            yield return Fader.I.FadeIn(1.5f);
            yield return new WaitForSeconds(1.5f);
            AudioDirector.I?.Play("reflection_sting", shard.transform.position, 0.5f);
            float e = 0f;
            while (e < 1f)
            {
                e += Time.deltaTime / 3f;
                tag.transform.localScale = Vector3.one * Mathf.Lerp(0f, 0.12f, Mathf.SmoothStep(0, 1, e));
                tag.transform.position = shard.transform.position + Vector3.up * 0.004f * e;
                yield return null;
            }
            yield return new WaitForSeconds(2.5f);
            yield return Fader.I.FadeOut(1.2f);
            Object.Destroy(props.gameObject);
            if (Branding.I != null) yield return Branding.I.EndCard(Branding.Tagline, 3.6f);

            // 5) Results, credits, menu.
            c.SetFlag(StoryFlags.CampaignComplete);
            Game.SaveNow();
            Game.InputLocks = 0;
            int m = Mathf.FloorToInt(c.playTimeSeconds / 60f), s = Mathf.FloorToInt(c.playTimeSeconds % 60f);
            string summary = "You came home.\n\n" +
                             "Time  " + m + ":" + s.ToString("00") + "\n" +
                             "Seals verified  " + c.VerifiedCount + " / 11\n" +
                             "Tears opened  " + c.tearsOpened + "\n" +
                             "False exits  " + c.falseExits + "\n" +
                             "Glasses taken off early  " + c.forcedReturns + "\n" +
                             "Times the Margin took you  " + c.failures;
            Fader.I.FadeTo(0f, 0.6f);
            Game.Cells.Activate(CellId.Menu);
            rig.PlaceAt(Game.Cells.Active.Arrival.position, Game.Cells.Active.Arrival.eulerAngles.y);
            AudioDirector.I?.SetMusic("mus_credits", 2f, 0.7f);
            MenuSystem.I.ShowResults(summary, () => MenuSystem.I.ShowCreditsThenMenu());
        }

        static IEnumerator BreakAnimation(Transform glasses, Transform parent)
        {
            // Anticipation (a slight press), contact (crack + haptic), reaction (lenses craze), settle.
            var basePos = glasses.position;
            float t = 0f;
            while (t < 1f) { t += Time.deltaTime / 0.35f; glasses.position = basePos + Vector3.down * 0.004f * Mathf.Sin(t * Mathf.PI); yield return null; }
            AudioDirector.I?.Play("glasses_break", glasses.position, 0.9f);
            AudioDirector.Haptic(Game.Mode == GameMode.VR ? Game.Input.DominantHand : HandSide.Desktop, 0.9f, 0.18f);
            PostFx.I?.Flash(0.25f);
            var cracked = Mats.Glass(new Color(0.08f, 0.1f, 0.1f, 0.9f), 0.6f);
            foreach (var r in glasses.GetComponentsInChildren<Renderer>())
                if (r.name == "Lens") r.sharedMaterial = cracked;
            for (int i = 0; i < 7; i++)
            {
                var shard = MeshFactory.Box("Shard", parent, Vector3.zero, new Vector3(0.012f, 0.0015f, 0.009f), Mats.Glass(new Color(0.2f, 0.25f, 0.24f, 0.8f)), false, 1f, Quaternion.Euler(0, Random.Range(0f, 360f), 0));
                shard.transform.position = basePos + new Vector3(Random.Range(-0.06f, 0.06f), 0.001f, Random.Range(-0.05f, 0.05f));
            }
            glasses.rotation *= Quaternion.Euler(0, 0, 6f);
            yield return new WaitForSeconds(0.6f);
        }
    }
}
