using UnityEngine;

namespace BuildATower
{
    public static class TutorialPrefs
    {
        public const string GuidedKey = "bat.tutorial.guided";
        public const string HoverTipsKey = "bat.tutorial.hoverTips";

        const int DefaultEnabled = 1;

        public static bool GuidedTutorialEnabled
        {
            get => PlayerPrefs.GetInt(GuidedKey, DefaultEnabled) != 0;
            set
            {
                PlayerPrefs.SetInt(GuidedKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        public static bool HoverTipsEnabled
        {
            get => PlayerPrefs.GetInt(HoverTipsKey, DefaultEnabled) != 0;
            set
            {
                PlayerPrefs.SetInt(HoverTipsKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        public static void ResetForTests()
        {
            PlayerPrefs.DeleteKey(GuidedKey);
            PlayerPrefs.DeleteKey(HoverTipsKey);
            PlayerPrefs.Save();
        }
    }
}
