using System;
using UnityEngine;

namespace TwelfthStamp
{
    public static partial class GuideBook
    {
        // ------------------------------------------------------------------ France: Paris, the bubble shooter
        static PlaceGuide Paris(ParisCell p)
        {
            const CountryId id = CountryId.France;
            var enc = p.Encounter as ParisEncounter;
            var board = p.Bubbles;
            var g = new PlaceGuide
            {
                Title = "France · Bubbles over the Seine",
                Briefing = "A huge bubble board floats over the Seine, the Eiffel Tower beyond it. Shoot coloured bubbles at matching ones: three or more of one colour pop, " +
                           "and bubbles left hanging from nothing drop. Clear " + (board != null ? board.Target : 18) + " bubbles to escape France.",
                Danger = "A figure in a raincoat comes down the street while you play, and now and then it rushes you. It kills at a touch: run down the street until it loses you, then come back to the board.",
            };
            Func<bool> solved = () => Verified(id) || (enc != null && enc.Solved);
            g.Steps.Add(new GuideStep
            {
                Text = "Stand on the glowing ring by the river wall, facing the bubble board",
                Hint = "The ring is on the pavement against the parapet, just in front of the board floating over the water.",
                Done = () => solved() || (board != null && (board.Shots > 0 || (board.FiringSpot != null && Dist(board.FiringSpot.position) < 1.4f))),
                Target = () => board != null && board.FiringSpot != null ? board.FiringSpot.position : (Vector3?)null,
                Label = "Stand here",
            });
            g.Steps.Add(new GuideStep
            {
                Text = "Aim at the board and shoot (" + Activate + "): hit bubbles of the same colour as the one in the launcher. Clear " + (board != null ? board.Target : 18),
                Hint = "The bubble flies toward where you aim, bounces off the side walls and sticks where it touches. Three of a colour pop; anything cut loose drops. The marker shows a good spot for the bubble you hold; the next one waits beside the launcher.",
                Done = solved,
                Target = () =>
                {
                    if (board == null || board.Won) return null;
                    return board.BestShot(out _, out var cell, out _) ? board.WorldOf(cell.y, cell.x) : board.CentreWorld;
                },
                Progress = () => board != null ? Mathf.Clamp01(board.Cleared / (float)Mathf.Max(1, board.Target)) : -1f,
                LabelNow = () => board == null ? "Bubbles" : board.Cleared + " / " + board.Target + " · " + BubbleBoard.ColourNames[board.CurrentColour],
                Label = "Bubbles",
            });
            g.Steps.Add(StampStep(id, "the figure in the raincoat"));
            g.Steps.Add(TravelStep(p));
            g.Alert = () => PassengerAlert(7f, "The figure in the raincoat is close. Run down the street, lose it, come back.");
            return g;
        }
    }
}
