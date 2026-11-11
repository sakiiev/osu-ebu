// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using osu.Framework.Allocation;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Containers;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.Localisation;
using osu.Framework.Utils;
using osu.Game.Audio;
using osu.Game.Configuration;
using osu.Game.Localisation.HUD;
using osu.Game.Overlays.SkinEditor;
using osu.Game.Rulesets.Scoring;
using osuTK;

namespace osu.Game.Skinning
{
    /// <summary>
    /// Displays the skin's "comboburst" image(s) sliding in from the side of the screen when reaching certain combo milestones, as in osu!stable.
    /// </summary>
    /// <remarks>
    /// The bounds of this component represent the area in which a burst on the left side is displayed.
    /// Bursts on the right side are displayed horizontally flipped, at the position mirroring this component within its parent.
    /// </remarks>
    public partial class LegacyComboBurst : CompositeDrawable, ISerialisableDrawable
    {
        public const string TEXTURE_NAME = @"comboburst";

        public const string SAMPLE_NAME = @"comboburst";

        /// <summary>
        /// A combo burst is displayed every time the combo reaches a multiple of this value.
        /// </summary>
        public const int BURST_INTERVAL = 50;

        /// <summary>
        /// Upper bound on the number of numbered "comboburst-N" samples looked up from the skin.
        /// </summary>
        private const int max_samples = 100;

        private const double slide_in_duration = 700;
        private const double fade_in_duration = 100;
        private const double hold_duration = 700;
        private const double fade_out_duration = 1000;

        [SettingSource(typeof(ComboBurstStrings), nameof(ComboBurstStrings.Side), nameof(ComboBurstStrings.SideDescription))]
        public Bindable<ComboBurstSide> Side { get; } = new Bindable<ComboBurstSide>();

        [SettingSource(typeof(ComboBurstStrings), nameof(ComboBurstStrings.PlaySamples))]
        public BindableBool PlaySamples { get; } = new BindableBool(true);

        public bool UsesFixedAnchor { get; set; }

        // Mirroring to the right side relies on this component being axis-aligned and not flipped.
        public bool CanBeRotated => false;

        public bool CanBeScaled(Axes axes) => axes == Axes.Both;

        [Resolved]
        private ISkinSource skin { get; set; } = null!;

        private readonly IBindable<int> combo = new BindableInt();

        private Texture[] textures = Array.Empty<Texture>();
        private bool randomTextureOrder;
        private int nextTextureIndex;

        private int[]? customSampleCombos;
        private SkinnableSound[] burstSamples = Array.Empty<SkinnableSound>();

        private Sprite? activeBurst;

        public LegacyComboBurst()
        {
            Anchor = Anchor.BottomLeft;
            Origin = Anchor.BottomLeft;
        }

        [BackgroundDependencyLoader]
        private void load(ScoreProcessor scoreProcessor, SkinEditor? skinEditor)
        {
            combo.BindTo(scoreProcessor.Combo);

            textures = skin.GetTextures(TEXTURE_NAME, default, default, true, @"-", null, out _);
            randomTextureOrder = skin.GetConfig<SkinConfiguration.LegacySetting, bool>(SkinConfiguration.LegacySetting.ComboBurstRandom)?.Value ?? false;
            customSampleCombos = parseCustomSampleCombos(skin.GetConfig<SkinConfiguration.LegacySetting, string>(SkinConfiguration.LegacySetting.CustomComboBurstSounds)?.Value);

            Size = textures.Length > 0
                ? new Vector2(textures.Max(t => t.DisplayWidth), textures.Max(t => t.DisplayHeight)) * LegacySkin.STABLE_MAGIC_SCALE_FACTOR
                : new Vector2(200, 400);

            // Skins may provide numbered samples ("comboburst-0", "comboburst-1", ...) which play in order as the combo grows,
            // or a single "comboburst" sample which plays for every burst.
            int sampleCount = 0;
            while (sampleCount < max_samples && skin.GetSample(new SampleInfo(getSampleName(sampleCount))) != null)
                sampleCount++;

            burstSamples = sampleCount > 0
                ? Enumerable.Range(0, sampleCount).Select(i => new SkinnableSound(new SampleInfo(getSampleName(i)))).ToArray()
                : new[] { new SkinnableSound(new SampleInfo($@"Gameplay/{SAMPLE_NAME}")) };

            AddRangeInternal(burstSamples);

            if (skinEditor != null)
                PlaySamples.Value = false;
        }

        protected override void LoadComplete()
        {
            base.LoadComplete();

            combo.BindValueChanged(c =>
            {
                // Only react to combo increasing naturally, not to resets or seeks.
                if (c.NewValue <= c.OldValue)
                    return;

                // The image and the sample are always triggered together and share the same burst index,
                // so "comboburst-N.png" appears alongside "comboburst-N.wav".
                if (GetBurstIndex(c.NewValue, customSampleCombos) is not int burstIndex)
                    return;

                Burst(burstIndex);

                if (PlaySamples.Value)
                    burstSamples[Math.Min(burstIndex, burstSamples.Length - 1)].Play();
            });
        }

        /// <summary>
        /// Whether a combo burst should be displayed upon reaching the given combo, when the skin doesn't specify custom combos.
        /// </summary>
        public static bool IsBurstCombo(int combo) => combo > 0 && combo % BURST_INTERVAL == 0;

        /// <summary>
        /// Returns the index of the combo burst to display upon reaching the given combo, or <c>null</c> if no burst should be displayed.
        /// </summary>
        /// <remarks>
        /// The index is the ordinal of the burst within the current combo, so successive bursts use successive images and samples,
        /// starting from the first ones again after a combo break.
        /// </remarks>
        /// <param name="combo">The combo just reached.</param>
        /// <param name="customSampleCombos">The combos at which bursts should occur, as specified by the skin's "CustomComboBurstSounds" setting, if any.</param>
        public static int? GetBurstIndex(int combo, IReadOnlyList<int>? customSampleCombos = null)
        {
            if (customSampleCombos != null)
            {
                for (int i = 0; i < customSampleCombos.Count; i++)
                {
                    if (customSampleCombos[i] == combo)
                        return i;
                }

                return null;
            }

            return IsBurstCombo(combo) ? combo / BURST_INTERVAL - 1 : null;
        }

        private static string getSampleName(int index) => $@"Gameplay/{SAMPLE_NAME}-{index}";

        /// <summary>
        /// Immediately displays a combo burst, regardless of the current combo.
        /// </summary>
        /// <param name="burstIndex">
        /// The ordinal of this burst within the current combo, which selects the image (wrapping around the available images).
        /// If <c>null</c>, images are cycled through in order.
        /// </param>
        public void Burst(int? burstIndex = null)
        {
            Texture? texture = getTexture(burstIndex);

            if (texture == null)
                return;

            bool rightSide = Side.Value switch
            {
                ComboBurstSide.Left => false,
                ComboBurstSide.Right => true,
                _ => RNG.NextBool(),
            };

            // Clear out any burst which is still on screen.
            activeBurst?.FadeOut(100).Expire();

            float width = texture.DisplayWidth * LegacySkin.STABLE_MAGIC_SCALE_FACTOR;

            // Left side bursts are aligned to the left edge of this component, right side bursts to the right edge of the mirrored area.
            float targetX = rightSide ? getMirroredOffset() + DrawWidth : 0;
            float startX = rightSide ? targetX + width : targetX - width;

            AddInternal(activeBurst = new Sprite
            {
                Texture = texture,
                Anchor = Anchor.BottomLeft,
                Origin = Anchor.BottomLeft,
                // A negative horizontal scale flips the sprite around its origin, so right side bursts extend leftwards.
                Scale = new Vector2(rightSide ? -LegacySkin.STABLE_MAGIC_SCALE_FACTOR : LegacySkin.STABLE_MAGIC_SCALE_FACTOR, LegacySkin.STABLE_MAGIC_SCALE_FACTOR),
                X = startX,
                Alpha = 0,
            });

            // Become visible almost immediately so the image lines up with the sample, which starts playing instantly.
            activeBurst.MoveToX(targetX, slide_in_duration, Easing.OutQuint)
                       .FadeIn(fade_in_duration)
                       .Then(slide_in_duration - fade_in_duration + hold_duration)
                       .FadeOut(fade_out_duration)
                       .Expire();
        }

        private Texture? getTexture(int? burstIndex)
        {
            if (textures.Length == 0)
                return null;

            if (randomTextureOrder)
                return textures[RNG.Next(textures.Length)];

            if (burstIndex != null)
                return textures[burstIndex.Value % textures.Length];

            var texture = textures[nextTextureIndex];
            nextTextureIndex = (nextTextureIndex + 1) % textures.Length;
            return texture;
        }

        /// <summary>
        /// Returns the horizontal offset (in local space) from this component's area to the area mirroring it around the vertical centre line of its parent.
        /// </summary>
        private float getMirroredOffset()
        {
            if (Parent == null)
                return 0;

            var boundsInParent = Parent.ToLocalSpace(ScreenSpaceDrawQuad).AABBFloat;
            float mirroredLeft = Parent.DrawWidth - boundsInParent.Right;

            Vector2 current = ToLocalSpace(Parent.ToScreenSpace(new Vector2(boundsInParent.Left, boundsInParent.Top)));
            Vector2 mirrored = ToLocalSpace(Parent.ToScreenSpace(new Vector2(mirroredLeft, boundsInParent.Top)));

            return mirrored.X - current.X;
        }

        private static int[]? parseCustomSampleCombos(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return null;

            var combos = new SortedSet<int>();

            foreach (string part in value.Split(','))
            {
                if (int.TryParse(part.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out int c) && c > 0)
                    combos.Add(c);
            }

            return combos.Count > 0 ? combos.ToArray() : null;
        }

        public enum ComboBurstSide
        {
            [LocalisableDescription(typeof(ComboBurstStrings), nameof(ComboBurstStrings.SideRandom))]
            Random,

            [LocalisableDescription(typeof(ComboBurstStrings), nameof(ComboBurstStrings.SideLeft))]
            Left,

            [LocalisableDescription(typeof(ComboBurstStrings), nameof(ComboBurstStrings.SideRight))]
            Right,
        }
    }
}
