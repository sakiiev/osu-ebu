// Copyright (c) ppy Pty Ltd <contact@ppy.sh>. Licensed under the MIT Licence.
// See the LICENCE file in the repository root for full licence text.

using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using osu.Framework.Allocation;
using osu.Framework.Audio.Sample;
using osu.Framework.Bindables;
using osu.Framework.Graphics;
using osu.Framework.Graphics.Rendering;
using osu.Framework.Graphics.Sprites;
using osu.Framework.Graphics.Textures;
using osu.Framework.IO.Stores;
using osu.Framework.Testing;
using osu.Game.Audio;
using osu.Game.Rulesets.Osu;
using osu.Game.Rulesets.Scoring;
using osu.Game.Skinning;

namespace osu.Game.Tests.Visual.Gameplay
{
    [HeadlessTest]
    public partial class TestSceneComboBurst : OsuTestScene
    {
        [Cached]
        private ScoreProcessor scoreProcessor = new ScoreProcessor(new OsuRuleset());

        [Resolved]
        private IRenderer renderer { get; set; } = null!;

        private ComboBurstSkin burstSkin = null!;
        private LegacyComboBurst comboBurst = null!;

        private IEnumerable<Sprite> bursts => comboBurst.ChildrenOfType<Sprite>();

        [Test]
        public void TestBurstCombos()
        {
            int[] expected = { 50, 100, 150, 200, 250, 300 };

            Assert.That(Enumerable.Range(0, 301).Where(LegacyComboBurst.IsBurstCombo), Is.EqualTo(expected));
        }

        [Test]
        public void TestBurstIndices()
        {
            Assert.That(LegacyComboBurst.GetBurstIndex(49), Is.Null);
            Assert.That(LegacyComboBurst.GetBurstIndex(50), Is.EqualTo(0));
            Assert.That(LegacyComboBurst.GetBurstIndex(100), Is.EqualTo(1));
            Assert.That(LegacyComboBurst.GetBurstIndex(950), Is.EqualTo(18));
            Assert.That(LegacyComboBurst.GetBurstIndex(2000), Is.EqualTo(39));

            int[] custom = { 25, 75, 200 };
            Assert.That(LegacyComboBurst.GetBurstIndex(50, custom), Is.Null);
            Assert.That(LegacyComboBurst.GetBurstIndex(25, custom), Is.EqualTo(0));
            Assert.That(LegacyComboBurst.GetBurstIndex(200, custom), Is.EqualTo(2));
        }

        [Test]
        public void TestDefaultSamplesBundled()
        {
            AddAssert("all default samples available", () =>
            {
                var store = new NamespacedResourceStore<byte[]>(Resources, @"Skins/Legacy");
                return Enumerable.Range(0, 19).All(i => store.Get($"{LegacyComboBurst.SAMPLE_NAME}-{i}.wav") != null);
            });

            AddAssert("classic skin provides samples", () =>
            {
                var classicSkin = Dependencies.Get<SkinManager>().DefaultClassicSkin;
                return Enumerable.Range(0, 19).All(i => classicSkin.GetSample(new SampleInfo($"Gameplay/{LegacyComboBurst.SAMPLE_NAME}-{i}")) != null)
                       && classicSkin.GetSample(new SampleInfo($"Gameplay/{LegacyComboBurst.SAMPLE_NAME}-19")) == null;
            });
        }

        [Test]
        public void TestBurstOnlyAtMilestones()
        {
            createComboBurst(2);

            AddStep("combo to 49", () => setCombo(49));
            AddAssert("no burst", () => !bursts.Any());

            AddStep("combo to 50", () => setCombo(50));
            AddAssert("burst shown", () => bursts.Count() == 1);
            AddUntilStep("burst expires", () => !bursts.Any());

            AddStep("reset combo", () => scoreProcessor.Combo.Value = 0);
            AddAssert("no burst on reset", () => !bursts.Any());
        }

        [Test]
        public void TestTexturesCycleSequentially()
        {
            createComboBurst(2);

            AddStep("burst", () => comboBurst.Burst());
            AddAssert("first texture used", () => bursts.Last().Texture == burstSkin.Textures[0]);

            AddStep("burst", () => comboBurst.Burst());
            AddAssert("second texture used", () => bursts.Last().Texture == burstSkin.Textures[1]);

            AddStep("burst", () => comboBurst.Burst());
            AddAssert("wraps to first texture", () => bursts.Last().Texture == burstSkin.Textures[0]);
        }

        [Test]
        public void TestImageFollowsBurstIndex()
        {
            createComboBurst(3);

            AddStep("combo to 50", () => setCombo(50));
            AddAssert("first image", () => bursts.Last().Texture == burstSkin.Textures[0]);

            AddStep("combo to 100", () => setCombo(100));
            AddAssert("second image", () => bursts.Last().Texture == burstSkin.Textures[1]);

            AddStep("break combo", () => scoreProcessor.Combo.Value = 0);
            AddStep("combo to 50", () => setCombo(50));
            AddAssert("first image again", () => bursts.Last().Texture == burstSkin.Textures[0]);

            AddStep("combo to 200", () => setCombo(200));
            AddAssert("fourth burst wraps to first image", () => bursts.Last().Texture == burstSkin.Textures[0]);
        }

        [Test]
        public void TestSides()
        {
            createComboBurst(1);

            AddStep("set left side", () => comboBurst.Side.Value = LegacyComboBurst.ComboBurstSide.Left);
            AddStep("burst", () => comboBurst.Burst());
            AddUntilStep("burst at left edge", () => bursts.Last().ScreenSpaceDrawQuad.AABBFloat.Left, () => Is.EqualTo(Content.ScreenSpaceDrawQuad.AABBFloat.Left).Within(1));
            AddAssert("not flipped", () => bursts.Last().Scale.X > 0);

            AddStep("set right side", () => comboBurst.Side.Value = LegacyComboBurst.ComboBurstSide.Right);
            AddStep("burst", () => comboBurst.Burst());
            AddUntilStep("burst at right edge", () => bursts.Last().ScreenSpaceDrawQuad.AABBFloat.Right, () => Is.EqualTo(Content.ScreenSpaceDrawQuad.AABBFloat.Right).Within(1));
            AddAssert("flipped", () => bursts.Last().Scale.X < 0);
        }

        [Test]
        public void TestNoTextures()
        {
            createComboBurst(0);

            AddStep("combo to 50", () => setCombo(50));
            AddAssert("no burst", () => !bursts.Any());
        }

        private void setCombo(int target)
        {
            while (scoreProcessor.Combo.Value < target)
                scoreProcessor.Combo.Value++;
        }

        private void createComboBurst(int textureCount)
        {
            AddStep("create combo burst", () =>
            {
                scoreProcessor.Combo.Value = 0;
                burstSkin = new ComboBurstSkin(renderer, textureCount);

                Child = new SkinProvidingContainer(burstSkin)
                {
                    RelativeSizeAxes = Axes.Both,
                    Child = comboBurst = new LegacyComboBurst(),
                };
            });
            AddUntilStep("wait for load", () => comboBurst.IsLoaded);
        }

        private class ComboBurstSkin : ISkin
        {
            public readonly Texture[] Textures;

            public ComboBurstSkin(IRenderer renderer, int textureCount)
            {
                Textures = Enumerable.Range(0, textureCount).Select(_ => renderer.CreateTexture(100, 200)).ToArray();
            }

            public Drawable? GetDrawableComponent(ISkinComponentLookup lookup) => null;

            public Texture? GetTexture(string componentName, WrapMode wrapModeS, WrapMode wrapModeT)
            {
                for (int i = 0; i < Textures.Length; i++)
                {
                    if (componentName == $"{LegacyComboBurst.TEXTURE_NAME}-{i}")
                        return Textures[i];
                }

                return null;
            }

            public ISample? GetSample(ISampleInfo sampleInfo) => null;

            public IBindable<TValue>? GetConfig<TLookup, TValue>(TLookup lookup)
                where TLookup : notnull
                where TValue : notnull
                => null;
        }
    }
}
