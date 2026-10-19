using System.IO;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace TwelfthStamp.Tests
{
    public class CountryDataTests
    {
        [Test]
        public void ExactlyElevenCountriesWithUniqueStableIds()
        {
            Assert.AreEqual(11, CountryDatabase.All.Count);
            Assert.AreEqual(11, CountryDatabase.All.Select(c => c.StableId).Distinct().Count());
            Assert.AreEqual(11, CountryDatabase.All.Select(c => c.Cell).Distinct().Count());
            Assert.AreEqual(11, CountryDatabase.All.Select(c => c.RecommendedOrder).Distinct().Count());
        }

        [Test]
        public void CounterfeitSealIsSeparateFromTheEleven()
        {
            Assert.IsFalse(CountryDatabase.All.Any(c => c.StampTexture == CountryDatabase.CounterfeitStampTexture));
            Assert.IsFalse(System.Enum.GetNames(typeof(CountryId)).Any(n => n.ToLower().Contains("home")));
        }

        [Test]
        public void RouteStartsHomeAndEndsInBerlin()
        {
            var route = CountryDatabase.InRecommendedOrder.ToList();
            Assert.AreEqual(CountryId.UAE, route.First().Id);
            Assert.AreEqual(CountryId.Egypt, route[1].Id);
            Assert.AreEqual(CountryId.France, route[2].Id);
            Assert.AreEqual(CountryId.Germany, route.Last().Id);
        }
    }

    public class CampaignTests
    {
        [Test]
        public void VerifyIsIdempotentAndGrantsResources()
        {
            var c = CampaignState.NewGame();
            Assert.IsTrue(c.Verify(CountryId.Egypt));
            Assert.IsFalse(c.Verify(CountryId.Egypt));
            Assert.AreEqual(1, c.VerifiedCount);
            Assert.IsTrue(c.HasResource(ResourceId.Sunlight));
            Assert.IsTrue(c.IsVisited(CountryId.Egypt));
        }

        [Test]
        public void VisitingDoesNotVerify()
        {
            var c = CampaignState.NewGame();
            c.MarkVisited(CountryId.Japan);
            Assert.IsTrue(c.IsVisited(CountryId.Japan));
            Assert.IsFalse(c.IsVerified(CountryId.Japan));
        }

        [Test]
        public void GenuineHomeNeedsElevenSealsInsertAndCancelledForgery()
        {
            var c = CampaignState.NewGame();
            foreach (var d in CountryDatabase.All.Where(d => d.Id != CountryId.Germany)) c.Verify(d.Id);
            Assert.IsFalse(c.AllElevenVerified);
            Assert.IsTrue(c.CanValidateFinalPattern, "ten seals + insert (Morocco provides it) should allow the final pattern");
            Assert.IsFalse(c.CanOpenGenuineHome);
            c.Verify(CountryId.Germany);
            Assert.IsTrue(c.AllElevenVerified);
            Assert.IsFalse(c.CanOpenGenuineHome, "forged authorisation must be cancelled first");
            c.SetFlag(StoryFlags.ForgedCancelled);
            Assert.IsTrue(c.CanOpenGenuineHome);
        }

        [Test]
        public void FinalPatternNeedsTheCalibrationInsert()
        {
            var c = CampaignState.NewGame();
            foreach (var d in CountryDatabase.All.Where(d => d.Id != CountryId.Germany && d.Id != CountryId.Morocco)) c.Verify(d.Id);
            c.verified.Add("morocco"); // verified without the resource (simulated corrupted state)
            Assert.IsFalse(c.CanValidateFinalPattern);
            c.AddResource(ResourceId.CalibrationInsert);
            Assert.IsTrue(c.CanValidateFinalPattern);
        }

        [Test]
        public void DestinationsUnlockInOrder()
        {
            var c = CampaignState.NewGame();
            Assert.IsFalse(c.IsDestinationAvailable(CountryId.Egypt));
            c.SetFlag(StoryFlags.SeamRevealed);
            Assert.IsTrue(c.IsDestinationAvailable(CountryId.Egypt));
            Assert.IsFalse(c.IsDestinationAvailable(CountryId.France));
            c.SetFlag(StoryFlags.TutorialDone);
            Assert.IsTrue(CountryDatabase.All.All(d => c.IsDestinationAvailable(d.Id)));
        }

        [Test]
        public void SunlightOnlyFlowsFromVerifiedEgypt()
        {
            var c = CampaignState.NewGame();
            Assert.IsFalse(c.ResourceFlowsFrom(CountryId.Egypt, ResourceId.Sunlight));
            c.Verify(CountryId.Egypt);
            Assert.IsTrue(c.ResourceFlowsFrom(CountryId.Egypt, ResourceId.Sunlight));
            Assert.IsFalse(c.ResourceFlowsFrom(CountryId.France, ResourceId.Sunlight));
        }

        [Test]
        public void SanitizeRejectsFutureVersionAndCleansIds()
        {
            var c = CampaignState.NewGame();
            c.version = 99;
            Assert.IsFalse(c.Sanitize());
            c.version = 1;
            c.verified.Add("atlantis");
            c.verified.Add("egypt");
            c.verified.Add("egypt");
            c.vitality = -3f;
            Assert.IsTrue(c.Sanitize());
            CollectionAssert.AreEquivalent(new[] { "egypt" }, c.verified);
            Assert.AreEqual(1f, c.vitality);
        }
    }

    public class SaveTests
    {
        string m_Dir;

        [SetUp]
        public void SetUp()
        {
            m_Dir = Path.Combine(Path.GetTempPath(), "ts_save_test_" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(m_Dir);
            SaveSystem.OverrideDirectory(m_Dir);
            SaveSystem.WritesSuspended = false;
        }

        [TearDown]
        public void TearDown()
        {
            SaveSystem.OverrideDirectory(null);
            try { Directory.Delete(m_Dir, true); } catch { }
        }

        [Test]
        public void MissingSaveIsNull()
        {
            Assert.IsNull(SaveSystem.LoadCampaign());
            Assert.IsFalse(SaveSystem.HasValidCampaign());
        }

        [Test]
        public void RoundTrip()
        {
            var c = CampaignState.NewGame();
            c.Verify(CountryId.Egypt);
            c.SetFlag(StoryFlags.SeamRevealed);
            c.CheckpointCell = CellId.Paris;
            Assert.IsTrue(SaveSystem.SaveCampaign(c));
            var l = SaveSystem.LoadCampaign();
            Assert.IsNotNull(l);
            Assert.IsTrue(l.IsVerified(CountryId.Egypt));
            Assert.AreEqual(CellId.Paris, l.CheckpointCell);
        }

        [Test]
        public void CorruptPrimaryFallsBackToBackup()
        {
            var c = CampaignState.NewGame();
            c.Verify(CountryId.UAE);
            SaveSystem.SaveCampaign(c);
            c.Verify(CountryId.Egypt);
            SaveSystem.SaveCampaign(c); // previous save becomes .bak
            File.WriteAllText(SaveSystem.PathOf(SaveSystem.CampaignFile), "{ this is not json");
            var l = SaveSystem.LoadCampaign();
            Assert.IsNotNull(l, "backup should be used");
            Assert.IsTrue(l.IsVerified(CountryId.UAE));
        }

        [Test]
        public void CorruptWithoutBackupIsNull()
        {
            File.WriteAllText(SaveSystem.PathOf(SaveSystem.CampaignFile), "garbage");
            Assert.IsNull(SaveSystem.LoadCampaign());
        }

        [Test]
        public void SuspendedWritesDoNotTouchDisk()
        {
            SaveSystem.WritesSuspended = true;
            Assert.IsFalse(SaveSystem.SaveCampaign(CampaignState.NewGame()));
            Assert.IsFalse(File.Exists(SaveSystem.PathOf(SaveSystem.CampaignFile)));
            SaveSystem.WritesSuspended = false;
        }

        [Test]
        public void SettingsPersistAndClamp()
        {
            var s = new GameSettings { master = 0.3f, snapAngle = 500f, subtitleSize = 9, leftHanded = true };
            Assert.IsTrue(SaveSystem.SaveSettings(s));
            var l = SaveSystem.LoadSettings();
            Assert.AreEqual(0.3f, l.master, 1e-4);
            Assert.AreEqual(90f, l.snapAngle);
            Assert.AreEqual(2, l.subtitleSize);
            Assert.IsTrue(l.leftHanded);
        }
    }

    public class ArgsAndBindingTests
    {
        [Test]
        public void ParsesOurModeArgument()
        {
            AppArgs.Parse(new[] { "TheTwelfthStamp.exe", "--mode=vr" });
            Assert.AreEqual(GameMode.VR, AppArgs.Mode);
            AppArgs.Parse(new[] { "TheTwelfthStamp.exe", "--mode=desktop" });
            Assert.AreEqual(GameMode.Desktop, AppArgs.Mode);
            AppArgs.Parse(new[] { "TheTwelfthStamp.exe" });
            Assert.IsNull(AppArgs.Mode);
            Assert.IsFalse(AppArgs.ModeArgumentPresent);
            AppArgs.Parse(new[] { "TheTwelfthStamp.exe", "--mode=banana" });
            Assert.IsNull(AppArgs.Mode);
            Assert.AreEqual("banana", AppArgs.InvalidModeValue);
            AppArgs.Parse(new[] { "x", "--demo", "--autotest=demo", "--report-dir=C:\\tmp" });
            Assert.IsTrue(AppArgs.Demo);
            Assert.AreEqual("demo", AppArgs.AutoTest);
        }

        [Test]
        public void DesktopLabelsComeFromRealBindings()
        {
            var input = new GameInput();
            Assert.AreEqual("E", GameInput.DesktopLabel(input.Interact));
            Assert.AreEqual("F", GameInput.DesktopLabel(input.PassportToggle));
            Assert.AreEqual("Left Mouse", GameInput.DesktopLabel(input.Tear));
            Assert.AreEqual("Right Mouse", GameInput.DesktopLabel(input.Seal));
            Assert.AreEqual("R", GameInput.DesktopLabel(input.Recall));
            Assert.AreEqual("Esc", GameInput.DesktopLabel(input.Pause));
            Assert.AreEqual("Mouse Wheel", GameInput.DesktopLabel(input.DestScroll));
        }

        [Test]
        public void VrControlsMapToQuestButtons()
        {
            var input = new GameInput();
            Assert.AreEqual("X", GameInput.XRControl(input.X).Label);
            Assert.AreEqual("Y", GameInput.XRControl(input.Y).Label);
            Assert.AreEqual("A", GameInput.XRControl(input.A).Label);
            Assert.AreEqual("B", GameInput.XRControl(input.B).Label);
            Assert.AreEqual(HandSide.Left, GameInput.XRControl(input.Menu).Hand);
            Assert.AreEqual("grip", GameInput.XRControl(input.RGrip).Control);
        }
    }
}
