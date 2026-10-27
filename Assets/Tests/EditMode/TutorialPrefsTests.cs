using BuildATower;
using NUnit.Framework;
using UnityEngine;

namespace BuildATower.Tests
{
    public class TutorialPrefsTests
    {
        [SetUp]
        public void SetUp() => TutorialPrefs.ResetForTests();

        [TearDown]
        public void TearDown() => TutorialPrefs.ResetForTests();

        [Test]
        public void GuidedTutorialEnabled_defaults_true_when_unset()
        {
            Assert.IsFalse(PlayerPrefs.HasKey(TutorialPrefs.GuidedKey));
            Assert.IsTrue(TutorialPrefs.GuidedTutorialEnabled);
        }

        [Test]
        public void HoverTipsEnabled_defaults_true_when_unset()
        {
            Assert.IsFalse(PlayerPrefs.HasKey(TutorialPrefs.HoverTipsKey));
            Assert.IsTrue(TutorialPrefs.HoverTipsEnabled);
        }

        [Test]
        public void GuidedTutorialEnabled_round_trips_false_and_true()
        {
            TutorialPrefs.GuidedTutorialEnabled = false;
            Assert.IsFalse(TutorialPrefs.GuidedTutorialEnabled);
            Assert.AreEqual(0, PlayerPrefs.GetInt(TutorialPrefs.GuidedKey));

            TutorialPrefs.GuidedTutorialEnabled = true;
            Assert.IsTrue(TutorialPrefs.GuidedTutorialEnabled);
            Assert.AreEqual(1, PlayerPrefs.GetInt(TutorialPrefs.GuidedKey));
        }

        [Test]
        public void HoverTipsEnabled_round_trips_false_and_true()
        {
            TutorialPrefs.HoverTipsEnabled = false;
            Assert.IsFalse(TutorialPrefs.HoverTipsEnabled);
            Assert.AreEqual(0, PlayerPrefs.GetInt(TutorialPrefs.HoverTipsKey));

            TutorialPrefs.HoverTipsEnabled = true;
            Assert.IsTrue(TutorialPrefs.HoverTipsEnabled);
            Assert.AreEqual(1, PlayerPrefs.GetInt(TutorialPrefs.HoverTipsKey));
        }

        [Test]
        public void ResetForTests_deletes_pref_keys()
        {
            TutorialPrefs.GuidedTutorialEnabled = false;
            TutorialPrefs.HoverTipsEnabled = false;
            Assert.IsTrue(PlayerPrefs.HasKey(TutorialPrefs.GuidedKey));
            Assert.IsTrue(PlayerPrefs.HasKey(TutorialPrefs.HoverTipsKey));

            TutorialPrefs.ResetForTests();

            Assert.IsFalse(PlayerPrefs.HasKey(TutorialPrefs.GuidedKey));
            Assert.IsFalse(PlayerPrefs.HasKey(TutorialPrefs.HoverTipsKey));
            Assert.IsTrue(TutorialPrefs.GuidedTutorialEnabled);
            Assert.IsTrue(TutorialPrefs.HoverTipsEnabled);
        }
    }
}
