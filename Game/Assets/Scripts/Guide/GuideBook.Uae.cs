using UnityEngine;

namespace TwelfthStamp
{
    public static partial class GuideBook
    {
        // ------------------------------------------------------------------ UAE: the Margin apartment, whack-a-ghost
        static PlaceGuide Uae(ApartmentCell a)
        {
            const CountryId id = CountryId.UAE;
            var enc = a.Encounter as UaeEncounter;
            var g = new PlaceGuide
            {
                Title = "The Margin · Whack-a-Ghost",
                Briefing = "Your apartment, copied, and haunted: little sheet ghosts pop out of the furniture. Stamp ten of them with your passport stamp to verify the homecoming seal. Then your passport opens a tear out of here.",
                Danger = "A ghost that gets away steals a little of your breath. Once the seal is verified, something made of glass steps out of the bathroom: slow, but it kills at a touch. Leave through a tear.",
            };
            g.Steps.Add(new GuideStep
            {
                Text = "Stamp the ghosts as they pop up: look at one and press  " + Use + "  while it is up (behind the sofa, the fridge, the bookcase, under the table, the TV...)",
                Hint = Vr ? "Touch a ghost with your hand, or point at it, and pull the trigger while it is up. They come faster as you go." :
                            "Get within a few steps, put the dot on a ghost and press " + Use + " while it is up. They come faster as you go.",
                Done = () => Verified(id),
                Target = () =>
                {
                    var s = a.Ghosts != null ? a.Ghosts.FirstUp : null;
                    return s != null ? s.HeadPoint : (Vector3?)null;
                },
                Progress = () => enc != null ? enc.Stamped / (float)GhostWhack.HitsToWin : -1f,
                LabelNow = () => (enc != null ? enc.Stamped : 0) + " / " + GhostWhack.HitsToWin,
            });
            g.Steps.Add(new GuideStep
            {
                Text = "Open your passport  " + Key(Intent.Passport),
                Hint = "Your passport is your key to every country. " + (Vr ? "Press X on the left controller." : "Press F."),
                Done = () => PassportOpen || TearOpen,
            });
            g.Steps.Add(new GuideStep
            {
                Text = "Get out: aim at a seam (a faint glowing line on the wall), open a tear  " + Key(Intent.Tear) + ", widen it to a <b>doorway</b> (keep holding) and walk through",
                Hint = "Don't let the glass figure reach you. Any seam works; the tear is a doorway once it is tall enough to walk into.",
                Done = () => false,
                Target = () => TearOpen ? TearAt : NearestSeam(a),
                Label = "Way out",
            });
            g.Alert = () => PassengerAlert(6f, "It is close. Move away and open your way out.");
            return g;
        }
    }
}
