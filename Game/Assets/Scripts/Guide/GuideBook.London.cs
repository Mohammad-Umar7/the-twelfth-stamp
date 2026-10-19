using System;
using UnityEngine;

namespace TwelfthStamp
{
    public static partial class GuideBook
    {
        // ------------------------------------------------------------------ United Kingdom: London, Stop the Clock
        static PlaceGuide London(LondonCell l)
        {
            const CountryId id = CountryId.UnitedKingdom;
            var enc = l.Encounter as LondonEncounter;
            var g = new PlaceGuide
            {
                Title = "United Kingdom · Stop the Clock",
                Briefing = "Big Ben's clock face stands on the promenade at one minute to midnight, its needle spinning. Stop the needle in the green zone three times in a row and the clock lets you go.",
                Danger = "The figure in the clock coat walks the river while you play, and a miss buzzes loud enough to bring it running. Run down the promenade until it loses you, then come back to the clock.",
            };
            Func<bool> solved = () => Verified(id) || (enc != null && enc.Solved) || (l.Clock != null && l.Clock.Won);
            g.Steps.Add(new GuideStep
            {
                Text = "Press the big red <b>STOP</b> button  " + Use + "  when the spinning needle is inside the <b>green zone</b>. Three hits in a row",
                Hint = (Vr ? "Touch the red button and pull the trigger" : "Look at the red button (or the clock face) and press " + Use) +
                       " the moment the needle enters the green. Each hit lights a lamp over the dial, moves the green, shrinks it and speeds the needle up; a miss puts you back to nought.",
                Done = solved,
                Target = () => l.Clock != null ? l.Clock.ButtonPosition : (Vector3?)null,
                Progress = () => l.Clock != null ? l.Clock.Hits / (float)StopTheClock.HitsToWin : -1f,
                LabelNow = () => l.Clock == null ? "STOP button" : "STOP  " + l.Clock.Hits + " / " + StopTheClock.HitsToWin,
            });
            g.Steps.Add(StampStep(id, "the figure in the clock coat"));
            g.Steps.Add(TravelStep(l));
            g.Alert = () => PassengerAlert(7f, "The figure in the clock coat is close. Run, lose it, come back to the clock.");
            return g;
        }
    }
}
