using UnityEngine;

namespace TwelfthStamp
{
    public static partial class GuideBook
    {
        // ------------------------------------------------------------------ Germany: Berlin, the finale (the shooting gallery)
        static PlaceGuide Berlin(BerlinCell b)
        {
            const CountryId id = CountryId.Germany;
            var enc = b.Encounter as BerlinEncounter;
            var gal = b.Gallery;
            var g = new PlaceGuide
            {
                Title = "Germany · The Ghost Shooting Gallery",
                Briefing = "The way home is here, and so is a forged one. Validate every seal, wear the Passenger down with beams of Egypt's light, cancel the forged HOME stamp with VOID, then escape through the genuine way home.",
                Danger = "It flickers from place to place round the hall and walks toward you between appearances. A miss costs nothing, but if it reaches you it has you: keep your distance and keep shooting.",
            };
            g.Steps.Add(new GuideStep
            {
                Text = "Validate the eleven seals at the registration plinth  " + Use + "  (you need the other ten seals and Morocco's calibration insert)",
                Hint = "If the plinth says something is missing, go back for it: open your passport and tear a way to the countries you haven't verified.",
                Done = () => Verified(id),
                Target = () => Pos(b.Plinth),
                Label = "Plinth",
            });
            g.Steps.Add(new GuideStep
            {
                Text = "Take the sun torch from the plinth  " + Grab,
                Hint = "A brass torch holding Egypt's sunlight lies across the plinth's near edge.",
                Done = () => (enc != null && enc.Exposed) || (gal != null && gal.TorchHeld),
                Target = () => gal != null && gal.TorchOut && !gal.TorchHeld ? gal.Torch.transform.position : (Vector3?)null,
                Label = "Sun torch",
            });
            g.Steps.Add(new GuideStep
            {
                Text = "Shoot the glowing target on its chest five times: aim and fire (" + Activate + ")",
                Hint = "It holds still a moment where it appears, then walks toward you. Every hit sends it flickering to a new place: turn and find it. Back away if it gets close.",
                Done = () => enc != null && enc.Exposed,
                Target = () => gal != null && gal.TargetUp ? gal.TargetPoint : (Vector3?)null,
                Progress = () => gal != null ? gal.Hits / (float)GhostGallery.HitsToWin : -1f,
                LabelNow = () => gal != null && gal.TargetUp ? "Target" : "",
            });
            g.Steps.Add(new GuideStep
            {
                Text = "Take the VOID stamp  " + Grab + "  and use it (" + Activate + ") to cancel the forged HOME seal",
                Hint = "The VOID stamp is on the side table near the south wall.",
                Done = () => Game.Campaign != null && Game.Campaign.HasFlag(StoryFlags.ForgedCancelled),
                Target = () => Holding(b.VoidStamp) ? (Vector3?)null : Pos(b.VoidStamp),
                Label = "VOID stamp",
            });
            g.Steps.Add(new GuideStep
            {
                Text = "Escape: open the real way home on the <b>blank wall</b>, widen it to a doorway and step through",
                Hint = "Not the door that looks like home. The genuine seam is on the blank wall; be quick, it is up again and walks to an open home tear.",
                Done = () => false,
                Target = () => b.HomeSeam != null ? b.HomeSeam.Center : (Vector3?)null,
                Label = "The way home",
            });
            g.Alert = () => FirstOf(TearAlert, () => PassengerAlert(5f, "It is close. Step back and shoot the glowing target."));
            return g;
        }
    }
}
