using System;
using System.Collections.Generic;
using RandomHeroSelectMod;
using Xunit;

namespace RandomHeroSelectMod.Tests
{
    public sealed class ShufflePlanTests
    {
        [Fact]
        public void Build_ReturnsExactClickCount()
        {
            foreach (int clicks in new[] { 1, 5, 22, 34, 99 })
            {
                var plan = ShufflePlan.Build(6, clicks, new Random(1234));
                Assert.Equal(clicks, plan.Length);
            }
        }

        [Fact]
        public void Build_StaysInsideCandidateRange()
        {
            var plan = ShufflePlan.Build(4, 60, new Random(7));
            foreach (int index in plan)
            {
                Assert.InRange(index, 0, 3);
            }
        }

        [Fact]
        public void Build_NeverClicksSameCardTwiceInARow()
        {
            var plan = ShufflePlan.Build(5, 40, new Random(99));
            for (int i = 1; i < plan.Length; i++)
            {
                Assert.NotEqual(plan[i - 1], plan[i]);
            }
        }

        [Fact]
        public void Build_CoversEveryCardWhenClicksExceedCandidates()
        {
            var plan = ShufflePlan.Build(6, 12, new Random(2024));
            var seen = new HashSet<int>(plan);
            Assert.Equal(6, seen.Count);
        }

        [Fact]
        public void Build_IsReproducibleWithSameSeed()
        {
            var first = ShufflePlan.Build(8, 30, new Random(4242));
            var second = ShufflePlan.Build(8, 30, new Random(4242));
            Assert.Equal(first, second);
        }

        [Fact]
        public void Build_DiffersWithDifferentSeeds()
        {
            var first = ShufflePlan.Build(8, 30, new Random(1));
            var second = ShufflePlan.Build(8, 30, new Random(2));
            Assert.NotEqual(first, second);
        }

        [Theory]
        [InlineData(0, 10)]
        [InlineData(-1, 10)]
        [InlineData(5, 0)]
        [InlineData(5, -3)]
        public void Build_ReturnsEmptyForDegenerateInputs(int candidateCount, int clicks)
        {
            Assert.Empty(ShufflePlan.Build(candidateCount, clicks, new Random(1)));
        }

        [Fact]
        public void Shuffle_IsAPermutation()
        {
            var deck = ShufflePlan.Shuffle(10, new Random(5));
            Assert.Equal(10, deck.Length);
            var seen = new HashSet<int>(deck);
            Assert.Equal(10, seen.Count);
        }

        [Fact]
        public void PickFinal_StaysInRangeOrMinusOne()
        {
            Assert.Equal(-1, ShufflePlan.PickFinal(0, new Random(1)));
            var random = new Random(11);
            for (int i = 0; i < 100; i++)
            {
                Assert.InRange(ShufflePlan.PickFinal(3, random), 0, 2);
            }
        }

        [Fact]
        public void Jitter_StaysNearBaselineAndNeverNegative()
        {
            var random = new Random(3);
            for (int i = 0; i < 200; i++)
            {
                int value = ShufflePlan.Jitter(80, 0.2, random);
                Assert.InRange(value, 64, 96);
            }
            Assert.Equal(0, ShufflePlan.Jitter(0, 0.2, random));
            Assert.Equal(80, ShufflePlan.Jitter(80, 0, random));
        }
    }
}
