using System.Collections;
using System.Linq;
using UnityEngine;

namespace TwelfthStamp
{
    /// <summary>
    /// --autotest=shift: an art-review pass over the glasses moment and the dimension shift. The glasses rise to the eyes;
    /// the room comes back as the Margin's copy and its kitchen seam wakes: it opens on Egypt by itself (sunlight and the
    /// desert through the gap) and pulls shut, counting for nothing. Then a tear-travel shift into Egypt, which carries no red
    /// (playtest, 18 Oct): its glow and its fade are the destination's light; and a Gentle one. Desktop only.
    /// </summary>
    public partial class AutoTest
    {
        IEnumerator ShiftFlow()
        {
            Note("--- Shift: the glasses go on, the seam wakes on Egypt, the dimension shift ---");
            MenuSystem.I.HideAll();
            var rig = Game.Rig as DesktopRig;
            Check(rig != null, "shift review runs on the desktop rig");
            if (rig == null) yield break;

            var apt = (ApartmentCell)Game.Cells.Activate(CellId.ApartmentNormal);
            yield return Frame(apt, new Vector3(0.6f, 1.6f, -1.6f), new Vector3(0.4f, 1.1f, 0.4f), null);
            rig.enabled = false;
            var head = Game.Rig.Head;

            // The glasses rise from in front of the player to the eyes.
            var props = new GameObject("ShiftReviewProps").transform;
            props.SetParent(apt.transform, false);
            var glasses = PropLibrary.Glasses(props, false).transform;
            glasses.position = head.position + head.forward * 0.55f - head.up * 0.25f;
            glasses.rotation = Quaternion.LookRotation(head.forward, Vector3.up) * Quaternion.Euler(-60f, 0f, 0f);
            var rise = StartCoroutine(HomecomingChapter.GlassesOn(glasses));
            yield return new WaitForSeconds(0.8f);
            yield return Shot("s01_glasses_rising");
            yield return rise;
            Check(!glasses.gameObject.activeSelf, "the glasses end up worn (hidden once on)");
            Destroy(props.gameObject);

            // The Margin copy: the kitchen seam wakes and opens on Egypt by itself, then pulls shut.
            var corrupt = (ApartmentCell)Game.Cells.Activate(CellId.ApartmentCorrupt);
            Game.Campaign.SetFlag(StoryFlags.SeamRevealed);
            var seam = HomecomingChapter.KitchenSeam(corrupt);
            HomecomingChapter.FaceSeam(seam, 1.3f);
            int opened = Game.Campaign.tearsOpened;
            bool visited = Game.Campaign.IsVisited(CountryId.Egypt);
            var wake = StartCoroutine(HomecomingChapter.SeamWakes(seam));
            yield return new WaitForSeconds(0.4f);
            yield return Shot("s02_seam_flares");
            yield return WaitUntil(() => Game.Tears.Active != null && Game.Tears.StoryTear, 4f, "the seam opens on Egypt by itself");
            Check(Game.Tears.Active != null && Game.Tears.Active.Destination.Id == CountryId.Egypt, "the waking seam opens on Egypt");
            yield return new WaitForSeconds(0.8f);
            yield return Shot("s03_seam_open_egypt");
            yield return wake;
            Check(Game.Tears.Active == null && !Game.Tears.StoryTear, "the seam pulls shut by itself");
            Check(Game.Campaign.tearsOpened == opened && Game.Campaign.IsVisited(CountryId.Egypt) == visited, "the waking seam counts for nothing (no tear counted, no visit)");
            yield return new WaitForSeconds(0.5f);
            yield return Shot("s04_seam_scar");

            // Tear travel: Egypt's sun through the cracks, no red anywhere, then the arrival there.
            Cell egypt = null;
            var travel = StartCoroutine(DimensionShift.Run(() =>
            {
                egypt = Game.Cells.Activate(CellId.Egypt);
                Game.Rig.PlaceAt(egypt.Arrival.position, egypt.Arrival.eulerAngles.y);
            }, 1.8f, CellId.Egypt));
            yield return new WaitForSeconds(1.45f);
            yield return Shot("s05_shift_egypt_light");
            var quick = DimensionShift.Last;
            Check(quick.Glimpse == DimensionShift.GlimpseOf(CellId.Egypt) && quick.Loose > 0 && quick.Pages > 0 && quick.Tears > 0,
                "a 1.8 s shift tears the room loose (" + quick.Loose + " props, " + quick.Pages + " pages, " + quick.Tears + " cracks in Egypt's light)");
            Check(quick.Slam.r <= quick.Slam.g * 1.5f + 0.01f, "no red in the shift: its fade is a dark shade of Egypt's light " + quick.Slam);
            yield return WaitUntil(() => DimensionShift.Phase == DimensionShift.Stage.Arrival, 3f, "the shift reaches Egypt");
            yield return new WaitForSeconds(0.6f);
            yield return Shot("s06_egypt_assembling");
            yield return travel;
            Check(egypt != null && Game.Cells.Active == egypt && Game.InputLocks == 0 && !DimensionShift.Running, "arrived in Egypt, input unlocked");

            // Gentle: shorter and softer; the same beats.
            int scare = Game.Settings.scareIntensity;
            Game.Settings.scareIntensity = 0;
            var gentle = StartCoroutine(DimensionShift.Run(() => { }, 3f, CellId.Paris));
            yield return new WaitForSeconds(1.2f);
            yield return Shot("s07_gentle_peak");
            yield return gentle;
            Game.Settings.scareIntensity = scare;
            Check(Game.InputLocks == 0 && !DimensionShift.Running, "Gentle shift finishes");
            rig.enabled = true;
        }
    }
}
