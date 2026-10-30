using UnityEngine;

namespace ZCN.QoL.ChildLearning
{
    /// <summary>
    /// Named, high-visibility choices for child Learning number highlight.
    /// The palette changes presentation only. A different color can be
    /// preferable for a player's vision, display, or background colors.
    /// </summary>
    internal static class LearningHighlightPalette
    {
        internal const int Yellow = 0;
        internal const int Cyan = 1;
        internal const int Magenta = 2;
        internal const int Orange = 3;
        internal const int Blue = 4;
        internal const int Default = Cyan;
        internal const int MaxChoice = Blue;

        // Keep the old UI1B yellow as a selectable color; cyan is SET3 default.
        private static readonly Color YellowColor = new Color(1f, 0.83f, 0.24f, 1f);
        private static readonly Color CyanColor = new Color(0.24f, 0.87f, 1f, 1f);
        private static readonly Color MagentaColor = new Color(1f, 0.48f, 0.86f, 1f);
        private static readonly Color OrangeColor = new Color(1f, 0.58f, 0.22f, 1f);
        private static readonly Color BlueColor = new Color(0.49f, 0.67f, 1f, 1f);

        internal static int Normalize(int choice)
        {
            return choice >= Yellow && choice <= MaxChoice ? choice : Default;
        }

        internal static Color TextColor(int choice, float originalAlpha)
        {
            Color color;
            switch (Normalize(choice))
            {
                case Cyan: color = CyanColor; break;
                case Magenta: color = MagentaColor; break;
                case Orange: color = OrangeColor; break;
                case Blue: color = BlueColor; break;
                default: color = YellowColor; break;
            }

            // Preserve the vanilla colony/other-map portrait transparency.
            color.a = originalAlpha;
            return color;
        }
    }
}
