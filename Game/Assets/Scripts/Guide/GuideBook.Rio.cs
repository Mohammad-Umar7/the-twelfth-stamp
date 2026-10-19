using System;
using UnityEngine;

namespace TwelfthStamp
{
    public static partial class GuideBook
    {
        // ------------------------------------------------------------------ Brazil: Rio (the carnival ring toss)
        static PlaceGuide Rio(RioCell r)
        {
            const CountryId id = CountryId.Brazil;
            var enc = r.Encounter as RioEncounter;
            var t = r.Toss;
            var g = new PlaceGuide
            {
                Title = "Brazil · Carnival Ring Toss",
                Briefing = "A ring toss stall stands on the frozen carnival avenue in front of the float. Land five rings on the sliding bottles and the stall gives you your passport stamp.",
                Danger = "One of the frozen dancers starts walking while you play, and now and then it rushes. Run down the avenue until it loses you, then come back to the stall.",
            };
            Func<bool> solved = () => Verified(id) || (enc != null && enc.Solved);
            g.Steps.Add(new GuideStep
            {
                Text = "Go to the ring toss stall in front of the float",
                Done = () => solved() || (t != null && (t.Thrown > 0 || Dist(t.PlayerSpot) < 2.0f)),
                Target = () => t != null ? t.PlayerSpot + Vector3.up : (Vector3?)null,
                Label = "Ring toss",
            });
            g.Steps.Add(new GuideStep
            {
                Text = "Aim over the counter and throw rings onto the sliding bottles (" + Activate + "). <b>Five ringers</b> win",
                Hint = "The ring comes down where you point on a bottle's rail. The bottles keep sliding: aim a little ahead of one, or wait for it to slow at the end of its rail.",
                Done = solved,
                Target = () => t != null ? t.TargetsPosition : (Vector3?)null,
                Progress = () => t != null ? t.Score / (float)RingToss.Target : -1f,
                Label = "Bottles",
            });
            g.Steps.Add(StampStep(id, "the figure"));
            g.Steps.Add(TravelStep(r));
            g.Alert = () => PassengerAlert(7f, "It is close. Run, lose it, come back to the stall.");
            return g;
        }
    }
}
