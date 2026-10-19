using System.Collections;
using System.Linq;
using UnityEngine;

namespace TwelfthStamp
{
    public partial class AutoTest
    {
        // full: an outdoor country with a new side area, lamps and birds; the two Margin interiors only hide postcards.
        static readonly (CellId cell, CountryId country, bool full)[] ExploreCountries =
        {
            (CellId.Egypt, CountryId.Egypt, true), (CellId.Paris, CountryId.France, true), (CellId.NewYork, CountryId.USA, true),
            (CellId.Japan, CountryId.Japan, true), (CellId.Riyadh, CountryId.SaudiArabia, true), (CellId.Agra, CountryId.India, true),
            (CellId.Rio, CountryId.Brazil, true), (CellId.London, CountryId.UnitedKingdom, true), (CellId.Berlin, CountryId.Germany, true),
            (CellId.ApartmentCorrupt, CountryId.UAE, false), (CellId.ShopCorrupt, CountryId.Morocco, false),
        };

        /// <summary>The guide's arrival card, found by name so this test also builds without the guide (Guide/Guide.cs).</summary>
        static void DismissGuideBriefing()
        {
            var type = System.Type.GetType("TwelfthStamp.Guide");
            var instance = type?.GetProperty("I")?.GetValue(null);
            if (instance != null) type.GetMethod("DismissBriefing")?.Invoke(instance, null);
        }

        /// <summary>
        /// The expanded countries (--autotest=explore): in each, the new side area exists and stands on solid floor (with a
        /// screenshot from its ShotPoint), three postcards are placed and one can be taken, lamps light and make a safe
        /// pool, and a flock scatters when the player walks up. The Passenger is kept away so nothing interrupts.
        /// </summary>
        IEnumerator ExploreFlow()
        {
            Note("--- Explore: expanded countries, postcards, lamps, birds ---");
            foreach (var (cellId, country, full) in ExploreCountries)
            {
                yield return Enter(cellId);
                var cell = Game.Cells.Active;
                if (cell == null || cell.Id != cellId) continue;
                Game.Passenger?.Despawn();
                if (Game.Passenger != null) Game.Passenger.Threatening = false;
                DismissGuideBriefing(); // the first-arrival card would cover the review shots
                Hud.I?.HideTitleCard();
                string tag = "e_" + country.ToString().ToLowerInvariant();

                // The new area.
                var shot = full ? cell.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name == "ShotPoint") : null;
                if (full) Check(shot != null, country + ": the new side area is built (ShotPoint found)");
                if (shot != null)
                {
                    bool floor = Physics.Raycast(shot.position + Vector3.up * 0.5f, Vector3.down, out var hit, 1.2f, ~((1 << Layers.PlayerBody) | (1 << Layers.MirrorOnly) | (1 << Layers.Interactable)), QueryTriggerInteraction.Ignore);
                    bool clear = !Physics.CheckCapsule(shot.position + Vector3.up * 0.35f, shot.position + Vector3.up * 1.5f, 0.25f,
                        ~((1 << Layers.PlayerBody) | (1 << Layers.MirrorOnly) | (1 << Layers.Interactable) | (1 << Layers.Floor)), QueryTriggerInteraction.Ignore);
                    Check(floor && clear, country + ": the new area's viewpoint stands on solid floor with headroom" + (floor ? "" : " (no floor)") + (clear ? "" : " (blocked)"));
                    Game.Rig.PlaceAt(floor ? hit.point : shot.position, shot.eulerAngles.y);
                    Hud.I?.HideTitleCard();
                    yield return new WaitForSeconds(1.2f);
                    yield return Shot(tag + "_area");
                }

                // Postcards: three here, and one can be taken.
                var cards = Postcard.All.Where(p => p != null && p.Country == country && p.transform.IsChildOf(cell.transform)).ToList();
                Check(cards.Count == Postcard.PerCountry && cards.Select(c => c.Number).Distinct().Count() == Postcard.PerCountry,
                    country + ": three postcards are hidden here (" + cards.Count + ")");
                var card = cards.FirstOrDefault(c => !c.Collected);
                if (card != null)
                {
                    // Stand 1.3 m off, on the side the player was coming from, looking at it.
                    var away = Vector3.ProjectOnPlane(Game.Rig.Head.position - card.transform.position, Vector3.up);
                    if (away.sqrMagnitude < 0.01f) away = Vector3.back;
                    var stand = card.transform.position + away.normalized * 1.3f;
                    var floorHit = Physics.Raycast(stand + Vector3.up * 1.5f, Vector3.down, out var fh, 4f, ~((1 << Layers.PlayerBody) | (1 << Layers.MirrorOnly) | (1 << Layers.Interactable)), QueryTriggerInteraction.Ignore);
                    Game.Rig.PlaceAt(floorHit ? fh.point : new Vector3(stand.x, card.transform.position.y - 1.1f, stand.z), Quaternion.LookRotation(-away).eulerAngles.y);
                    yield return new WaitForSeconds(0.8f);
                    yield return Shot(tag + "_postcard");
                    card.Use(Game.Rig.MainHand);
                    yield return new WaitForSeconds(0.7f);
                    Check(card.Collected && Game.Campaign.HasFlag(Postcard.FlagFor(country, card.Number)), country + ": a postcard can be taken and is kept (flag " + Postcard.FlagFor(country, card.Number) + ")");
                }

                if (!full) continue;

                // Lamps make safe light.
                var lamps = SwitchLamp.All.Where(l => l != null && l.transform.IsChildOf(cell.transform)).ToList();
                Check(lamps.Count >= 2, country + ": lamps to light (" + lamps.Count + ")");
                var lamp = lamps.FirstOrDefault(l => !l.On);
                if (lamp != null)
                {
                    lamp.Use(Game.Rig.MainHand);
                    yield return new WaitForSeconds(0.6f);
                    float lit = lamp.Pool != null ? LightVolumes.Exposure(lamp.Pool.transform.position + Vector3.up * 1.2f) : 0f;
                    Check(lamp.On && lit > 0.5f, country + ": a lit lamp makes a safe pool of light (exposure " + lit.ToString("0.00") + ")");
                }

                // Birds scatter when you walk up.
                var flock = AmbientBirds.All.FirstOrDefault(b => b != null && b.transform.IsChildOf(cell.transform));
                Check(flock != null, country + ": birds about the place");
                if (flock != null)
                {
                    // Walk away first (earlier steps may have startled them), let them settle, then come up beside them on
                    // ground at their own level.
                    Game.Rig.PlaceAt(cell.Arrival.position, cell.Arrival.eulerAngles.y);
                    yield return null;
                    flock.SettleNow();
                    yield return null;
                    int before = flock.TakeOffs;
                    Vector3 stand = flock.transform.position + flock.transform.right * 2.5f;
                    bool found = false;
                    const int solid = ~((1 << Layers.PlayerBody) | (1 << Layers.MirrorOnly) | (1 << Layers.Interactable));
                    for (int a = 0; a < 8 && !found; a++)
                    {
                        var c = flock.transform.position + Quaternion.Euler(0f, a * 45f, 0f) * Vector3.forward * 2.6f;
                        if (!Physics.Raycast(c + Vector3.up * 1.8f, Vector3.down, out var gh, 3.5f, solid, QueryTriggerInteraction.Ignore)) continue;
                        if (Mathf.Abs(gh.point.y - flock.transform.position.y) > 0.7f) continue;
                        if (Physics.CheckCapsule(gh.point + Vector3.up * 0.35f, gh.point + Vector3.up * 1.5f, 0.25f, solid & ~(1 << Layers.Floor), QueryTriggerInteraction.Ignore)) continue;
                        stand = gh.point; found = true;
                    }
                    var look = Vector3.ProjectOnPlane(flock.transform.position - stand, Vector3.up);
                    Game.Rig.PlaceAt(stand, look.sqrMagnitude > 1e-4f ? Quaternion.LookRotation(look).eulerAngles.y : 0f);
                    yield return new WaitForSeconds(0.9f);
                    Check(flock.TakeOffs > before && flock.Airborne, country + ": the birds burst into the air as you come close" + (found ? "" : " (no clear ground beside them)"));
                    yield return Shot(tag + "_birds");
                }
            }
        }
    }
}
