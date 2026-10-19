using System.Collections;
using System.Linq;
using UnityEngine;

namespace TwelfthStamp
{
    /// <summary>
    /// --autotest=guide: every place has a walkthrough. Arriving shows the briefing; then the step banner, a marker on the
    /// first target and the minimap with its map; the full map opens on M. Egypt is played through: crossing the temple
    /// run's finish line verifies it and moves the guide on to the stamp. At any window
    /// size: the minimap keeps clear of the passport card and a tear's taller card, and a marker held at the screen edge
    /// keeps clear of the objectives panel. Screenshots of each. Desktop only.
    /// </summary>
    public partial class AutoTest
    {
        IEnumerator GuideFlow()
        {
            Note("--- Guide: walkthroughs, markers, minimap and map ---");
            MenuSystem.I.HideAll();
            yield return new WaitForSeconds(0.3f);
            Check(Guide.I != null && MapView.I != null, "the guide and the map are running");
            if (Guide.I == null || MapView.I == null) yield break;

            var places = new[] { CellId.ApartmentCorrupt, CellId.Egypt, CellId.Paris, CellId.NewYork, CellId.Japan, CellId.Riyadh, CellId.Agra, CellId.Rio, CellId.London, CellId.ShopCorrupt, CellId.Berlin };
            foreach (var id in places)
            {
                yield return Enter(id);
                yield return new WaitForSeconds(0.4f);
                var g = Guide.I.Current;
                Check(g != null && g.Steps.Count >= 2 && Guide.I.StepIndex >= 0, id + ": a walkthrough with " + (g != null ? g.Steps.Count : 0) + " steps, on step " + (Guide.I.StepIndex + 1));
                Check(Guide.I.BriefingShowing, id + ": the briefing shows on arrival");
                if (id == CellId.Egypt || id == CellId.Paris) yield return Shot("g_" + id + "_briefing");
                Guide.I.DismissBriefing();
                yield return new WaitForSeconds(0.3f);
                Check(Guide.I.BannerShowing, id + ": the step banner is on screen");
                Check(MapView.I.HasMap && !MapView.I.MiniShowing, id + ": the place has a map for M, and the minimap stays off (" + MapView.I.MapSize + ")");
                var st = g != null && Guide.I.StepIndex >= 0 && Guide.I.StepIndex < g.Steps.Count ? g.Steps[Guide.I.StepIndex] : null;
                Check(st != null && (st.Target == null || Guide.I.Target.HasValue), id + ": the current step's marker is placed" + (st != null && st.Target == null ? " (this step has no place)" : ""));
                var country = Game.Cells.Active.Country;
                if (country != null && Postcard.All.Exists(p => p != null && p.isActiveAndEnabled && p.Country == country.Id))
                    Check(MapView.I.MiniLabel.Contains("postcards"), id + ": the minimap counts this place's postcards (" + MapView.I.MiniLabel + ")");
                yield return Shot("g_" + id + "_banner");
                MapView.I.SetFull(true);
                yield return new WaitForSeconds(0.25f);
                if (id == CellId.Egypt || id == CellId.Japan || id == CellId.Riyadh || id == CellId.London || id == CellId.ShopCorrupt) yield return Shot("g_" + id + "_map");
                MapView.I.SetFull(false);
            }

            // The minimap and the passport card share the right edge; on a wide, short screen the minimap shrinks for it.
            yield return Enter(CellId.London);
            Guide.I.DismissBriefing();
            yield return new WaitForSeconds(0.4f);
            Game.Passport.Open(HandSide.Desktop);
            yield return new WaitForSeconds(0.6f);
            var card = Hud.I.PassportCard;
            var mini = MapView.I.MiniScreenRect;
            Check(card != null && mini.HasValue && !ScreenOf(card).Overlaps(mini.Value),
                "with the passport open its card is clear of the minimap, its label and hint (minimap " + Show(mini) + ", card " + Show(card) + ")");
            yield return Shot("g_passport_card");
            Game.Passport.Close();
            yield return new WaitForSeconds(0.6f);

            // A target off to the left with Tab open: the marker waits at the edge, beside the objectives panel, not on it.
            Hud.I.ToggleObjectives();
            var aim = Guide.I.Target;
            if (aim.HasValue)
            {
                var feet = Game.Rig.Feet;
                var to = aim.Value - feet;
                to.y = 0f;
                Game.Rig.PlaceAt(feet, Quaternion.LookRotation(to).eulerAngles.y + 90f);
            }
            yield return new WaitForSeconds(0.5f);
            var tab = Hud.I.ObjectivesPanel;
            var mk = Guide.I.MarkerRect;
            Check(tab != null && mk.HasValue && !ScreenOf(tab).Overlaps(mk.Value) && mk.Value.center.x < Screen.width / 2f,
                "a marker held at the left edge is clear of the objectives panel (marker " + Show(mk) + ", panel " + Show(tab) + ")");
            yield return Shot("g_marker_tab");
            Hud.I.ToggleObjectives();

            // Egypt, played through: the temple run (AutoTestArcadeC.cs).
            yield return GuideEgyptRun();

            // A tear's card (gauges, contamination, more rows) is taller than the passport's: the minimap keeps clear of it too.
            yield return Enter(CellId.ApartmentCorrupt);
            Guide.I.DismissBriefing();
            yield return new WaitForSeconds(0.4f);
            var flat = (ApartmentCell)Game.Cells.Active;
            Game.Passport.Open(HandSide.Desktop);
            Game.Passport.Select(CountryId.Egypt, true);
            var seam = flat.Seams.First(x => x.Id == "kitchen");
            var stand = seam.Center + seam.Forward * 1.3f;
            var look = seam.Center - stand;
            look.y = 0f;
            Game.Rig.PlaceAt(new Vector3(stand.x, 0f, stand.z), Quaternion.LookRotation(look).eulerAngles.y);
            yield return null;
            yield return null;
            yield return Hold(dt => Game.Tears.HoldWiden(dt, HandSide.Desktop), 3.6f);
            yield return new WaitForSeconds(0.3f);
            card = Hud.I.PassportCard;
            mini = MapView.I.MiniScreenRect;
            Check(Game.Tears.Active != null && card != null && mini.HasValue && !ScreenOf(card).Overlaps(mini.Value),
                "with a tear open its taller card is clear of the minimap (minimap " + Show(mini) + ", card " + Show(card) + ")");
            yield return Shot("g_tear_card");
        }

        static Rect ScreenOf(RectTransform rt)
        {
            var c = new Vector3[4];
            rt.GetWorldCorners(c);   // the desktop HUD is a screen-space overlay: these are screen pixels
            return Rect.MinMaxRect(c[0].x, c[0].y, c[2].x, c[2].y);
        }

        static string Show(Rect? r) => r.HasValue ? "x " + r.Value.xMin.ToString("0") + ".." + r.Value.xMax.ToString("0") + ", y " + r.Value.yMin.ToString("0") + ".." + r.Value.yMax.ToString("0") : "none";
        static string Show(RectTransform rt) => rt != null ? Show(ScreenOf(rt)) : "none";
    }
}
