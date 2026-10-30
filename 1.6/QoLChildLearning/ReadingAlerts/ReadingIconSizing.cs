using UnityEngine;

namespace ZCN.QoL.ChildLearning
{
    /// <summary>
    /// UI4: shared sizing and position helpers used by the actual colonist
    /// bar and the settings-only preview. The original size/placement
    /// calculations are preserved.
    /// </summary>
    internal static class ReadingIconSizing
    {
        internal const float VanillaFriendlySize = 18f;
        internal const float LargeMultiplier = 2f;
        internal const float PortraitInset = 2f;

        // UI4: one placement calculation shared by the real colonist-bar
        // icon and its settings-only preview. Keep the existing coordinates.
        internal static Rect GetIconRect(Rect portrait, float iconSize, float barScale, int position)
        {
            if (position < 0 || position > 5)
                position = 0;

            const float inset = 2f;
            float iconX;
            float iconY;
            switch (position)
            {
                case 1: // Top-left
                    iconX = portrait.xMin + inset;
                    iconY = portrait.yMin + inset;
                    break;
                case 2: // Bottom-right
                    iconX = portrait.xMax - iconSize - inset;
                    iconY = portrait.yMax - iconSize - inset;
                    break;
                case 3: // Bottom-left
                    iconX = portrait.xMin + inset;
                    iconY = portrait.yMax - iconSize - inset;
                    break;
                case 4: // Center
                    iconX = portrait.center.x - iconSize * 0.5f;
                    iconY = portrait.center.y - iconSize * 0.5f;
                    break;
                case 5: // Lower-center (chest)
                    iconX = portrait.center.x - iconSize * 0.5f;
                    iconY = portrait.yMax - iconSize - 4f * barScale;
                    break;
                default: // Top-right
                    iconX = portrait.xMax - iconSize - inset;
                    iconY = portrait.yMin + inset;
                    break;
            }

            return new Rect(iconX, iconY, iconSize, iconSize);
        }

        internal static float GetSize(Rect portrait, float barScale, bool large)
        {
            float original = VanillaFriendlySize * barScale;
            if (!large)
            {
                return original;
            }

            // A large icon should remain INSIDE its own portrait in very
            // crowded colonies. Do not change normal-size legacy geometry.
            float room = Mathf.Min(portrait.width, portrait.height) - PortraitInset * 2f;
            return Mathf.Max(0f, Mathf.Min(original * LargeMultiplier, room));
        }
    }
}
