using System;
using UnityEngine;

namespace TwelfthStamp
{
    public static partial class GuideBook
    {
        // ------------------------------------------------------------------ Morocco: the corrupted shop, Memory Match
        static PlaceGuide Morocco(ShopCell s)
        {
            const CountryId id = CountryId.Morocco;
            var enc = s.Encounter as MoroccoEncounter;
            var g = new PlaceGuide
            {
                Title = "Morocco · Memory Match",
                Briefing = "The optician's shop, as it was the day you bought the glasses. Eight cards lie face down on the counter: find the four pairs (glasses, camera, passport, stamp) and the shop gives up its secret.",
                Danger = "The optician stalks the aisles once you turn a card, and a wrong guess draws it closer. It kills at a touch, but the lamplight around the counter keeps it away: stay in the light.",
            };
            Func<bool> drawer = () => Verified(id) || (enc != null && enc.DrawerOpen);
            g.Steps.Add(new GuideStep
            {
                Text = "Memory match: flip two cards at a time  " + Use + "  and find all four pairs on the counter",
                Hint = (Vr ? "Touch a card and pull the trigger" : "Look at a card and press " + Use) +
                       " to turn it over, then turn another. A pair stays face up and glows; two that differ turn back over, so remember where each picture was.",
                Done = drawer,
                Target = () => enc != null && enc.Memory != null && Game.Rig != null ? enc.Memory.NextCardPosition(Game.Rig.Feet) : (s.CounterTop != null ? s.CounterTop.position : (Vector3?)null),
                Progress = () => enc != null && enc.Memory != null ? enc.Memory.MatchedPairs / (float)MemoryMatch.Pairs : 0f,
                LabelNow = () => enc != null && enc.Memory != null ? "Pairs " + enc.Memory.MatchedPairs + " / " + MemoryMatch.Pairs : "Cards",
            });
            g.Steps.Add(new GuideStep
            {
                Text = "Take the calibration insert from the drawer  " + Grab + "  and fit it inside the glasses case on the counter  " + Use,
                Done = () => Verified(id) || (enc != null && enc.NotesRead) || (Game.Campaign != null && Game.Campaign.HasFlag(StoryFlags.OpticianNotesRead)),
                Target = () => enc == null ? null : enc.Insert != null && !Holding(enc.Insert) ? Pos(enc.Insert) : Pos(enc.CaseSocket),
                LabelNow = () => enc != null && enc.Insert != null && Holding(enc.Insert) ? "Glasses case" : "Insert",
            });
            g.Steps.Add(new GuideStep
            {
                Text = "Listen to the optician's notes. The trace appears on the counter when they end",
                Done = () => Verified(id) || (s.Trace != null && s.Trace.Revealed),
                Target = () => s.CounterTop != null ? s.CounterTop.position : (Vector3?)null,
                Label = "Counter",
            });
            g.Steps.Add(TraceStep(id, () => s.Trace, "on the counter"));
            g.Steps.Add(StampStep(id, "the optician"));
            g.Steps.Add(TravelStep(s));
            g.Alert = () => PassengerAlert(7f, "The optician is close. Get back into the lamplight by the counter.");
            return g;
        }
    }
}
