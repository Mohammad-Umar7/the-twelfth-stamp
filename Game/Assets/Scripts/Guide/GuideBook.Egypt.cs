using System;
using UnityEngine;

namespace TwelfthStamp
{
    public static partial class GuideBook
    {
        // ------------------------------------------------------------------ Egypt: the temple run
        static PlaceGuide Egypt(EgyptCell e)
        {
            const CountryId id = CountryId.Egypt;
            var enc = e.Encounter as EgyptEncounter;
            var run = e.Run;
            var g = new PlaceGuide
            {
                Title = "Egypt · The Temple Run",
                Briefing = "A long temple causeway runs north from the gate to a portal. Run it: mummies burst out of the tomb doors and shuffle down the lanes at you, " +
                           "fallen stones block single lanes, and gold passport coins line the way. Reach the portal at the far end to verify Egypt.",
                Danger = "A mummy's grip drains your energy and throws you back a few metres: change lanes before it reaches you. Something wrapped in linen also follows you slowly from behind, so keep moving.",
            };
            Func<bool> solved = () => Verified(id) || (enc != null && enc.Solved);
            Func<Vector3> feet = () => Game.Rig != null ? e.transform.InverseTransformPoint(Game.Rig.Feet) : TempleRun.StartLocal;
            g.Steps.Add(new GuideStep
            {
                Text = "Run the causeway north to the portal (run: " + RunKey + "). Change lanes to dodge the mummies, go round the fallen stones and collect the passport coins",
                Hint = "Three lanes, each about two metres wide. A mummy picks your lane when it bursts out of its tomb: step into another lane. Run through the gold coins to collect them; each one steadies you a little.",
                Done = solved,
                Target = () => run != null ? e.transform.TransformPoint(run.WaypointLocal(feet())) : (Vector3?)null,
                Progress = () => run != null ? run.Progress(feet()) : -1f,
                LabelNow = () => run == null ? "Run" : "Coins " + run.Coins + " / " + run.CoinTotal,
                Label = "Run",
            });
            g.Steps.Add(StampStep(id, "the wrapped figure"));
            g.Steps.Add(TravelStep(e));
            g.Alert = () =>
            {
                if (run != null && !solved() && run.Live && run.ThreatAhead(feet(), 7f) != null) return "<b>A mummy is coming down your lane:</b> change lanes now.";
                return PassengerAlert(8f, "The wrapped figure is close behind you. Keep running (" + RunKey + ").");
            };
            return g;
        }
    }
}
