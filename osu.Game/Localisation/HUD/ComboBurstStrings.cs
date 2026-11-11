// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using osu.Framework.Localisation;

namespace osu.Game.Localisation.HUD
{
    public static class ComboBurstStrings
    {
        private const string prefix = @"osu.Game.Resources.Localisation.HUD.ComboBurst";

        /// <summary>
        /// "Side"
        /// </summary>
        public static LocalisableString Side => new TranslatableString(getKey(@"side"), @"Side");

        /// <summary>
        /// "Which side of the screen combo bursts appear on. The right side mirrors this element's position."
        /// </summary>
        public static LocalisableString SideDescription => new TranslatableString(getKey(@"side_description"),
            @"Which side of the screen combo bursts appear on. The right side mirrors this element's position.");

        /// <summary>
        /// "Random"
        /// </summary>
        public static LocalisableString SideRandom => new TranslatableString(getKey(@"side_random"), @"Random");

        /// <summary>
        /// "Left"
        /// </summary>
        public static LocalisableString SideLeft => new TranslatableString(getKey(@"side_left"), @"Left");

        /// <summary>
        /// "Right"
        /// </summary>
        public static LocalisableString SideRight => new TranslatableString(getKey(@"side_right"), @"Right");

        /// <summary>
        /// "Play samples"
        /// </summary>
        public static LocalisableString PlaySamples => new TranslatableString(getKey(@"play_samples"), @"Play samples");

        private static string getKey(string key) => $@"{prefix}:{key}";
    }
}
