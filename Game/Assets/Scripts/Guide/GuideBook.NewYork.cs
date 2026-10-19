using System;
using UnityEngine;

namespace TwelfthStamp
{
    public static partial class GuideBook
    {
        // ------------------------------------------------------------------ USA: New York (the space-station arcade)
        static PlaceGuide NewYork(NewYorkCell n)
        {
            const CountryId id = CountryId.USA;
            var enc = n.Encounter as NewYorkEncounter;
            var s = n.Station;
            var g = new PlaceGuide
            {
                Title = "United States · Spot the Impostor",
                Briefing = "The arrival hall has become a space-station arcade. One of the three crewmates on the pads is an impostor: do the two station tasks, and the security screen over the gate tells you which one to vote out.",
                Danger = "The figure walks in off the apron and hunts the hall while you work, and now and then it rushes. A wrong vote sets it on you at once. Run until it loses you, then come back.",
            };
            Func<bool> solved = () => Verified(id) || (enc != null && enc.Solved);
            g.Steps.Add(new GuideStep
            {
                Text = "Task 1, <b>Fix wiring</b>: press the three coloured buttons in the order its little screen shows  " + Use,
                Hint = "The console on the left of the hall. The little screen shows three coloured squares, 1 to 2 to 3: press the buttons in that order. A wrong button starts it again.",
                Done = () => solved() || (s != null && s.WiringDone),
                Target = () => s != null ? s.WiringPosition : (Vector3?)null,
                Label = "Fix wiring",
            });
            g.Steps.Add(new GuideStep
            {
                Text = "Task 2, <b>Swipe card</b>: take the crew keycard and put it in the reader  " + Grab,
                Hint = "The console on the right of the hall: the keycard lies in a tray beside the reader. Take it, then put it in the slot on top of the reader.",
                Done = () => solved() || (s != null && s.CardDone),
                Target = () => s == null ? (Vector3?)null : Holding(s.Keycard) ? s.ReaderPosition : s.KeycardPosition,
                Label = "Keycard",
                LabelNow = () => s != null && Holding(s.Keycard) ? "Card reader" : "Keycard",
            });
            g.Steps.Add(new GuideStep
            {
                Text = "Read the clue on the security screen over the gate, then press <b>VOTE</b> in front of the impostor  " + Use,
                Hint = "Each crewmate looks a little off: one has no backpack, one has a cracked visor, one stands in green vent slime. The screen says which of those the impostor has. A wrong vote sets the figure on you; you can vote again.",
                Done = solved,
                Target = () => s != null ? s.ScreenPosition + Vector3.up * 0.46f : (Vector3?)null, // over the frame, not on the clue
                Label = "Security screen",
            });
            g.Steps.Add(StampStep(id, "the figure"));
            g.Steps.Add(TravelStep(n));
            g.Alert = () => PassengerAlert(7f, "The figure is close. Run, lose it, come back to the crew.");
            return g;
        }
    }
}
