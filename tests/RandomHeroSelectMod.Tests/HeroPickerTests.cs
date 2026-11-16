using System;
using System.Collections.Generic;
using RandomHeroSelectMod;
using Xunit;

namespace RandomHeroSelectMod.Tests
{
    public sealed class HeroPickerTests
    {
        private static HeroCandidate Card(
            int listIndex, int heroId, bool owned = true, int pveLevel = 6,
            bool banned = false, bool taken = false, bool locked = false, bool grayed = false)
        {
            return new HeroCandidate
            {
                ListIndex = listIndex,
                HeroId = heroId,
                Owned = owned,
                PveLevel = pveLevel,
                Banned = banned,
                Taken = taken,
                Locked = locked,
                Grayed = grayed
            };
        }

        [Fact]
        public void Filter_KeepsOnlyMaxedOwnedCards()
        {
            var all = new List<HeroCandidate>
            {
                Card(0, 101, pveLevel: 6),
                Card(1, 102, pveLevel: 5),           // 未满级
                Card(2, 103, owned: false),          // 未拥有
                Card(3, 104, banned: true),          // 本局禁选
                Card(4, 105, taken: true),           // 已被占
                Card(5, 106, locked: true),          // 场馆未解锁
                Card(6, 107, grayed: true),          // 置灰
                Card(7, 108, pveLevel: 7)            // 超过阈值也算满级
            };

            var selection = HeroPicker.Filter(all, isPve: true, requireOwned: true, maxLevel: 6);

            Assert.Equal(2, selection.Eligible.Count);
            Assert.Equal(101, selection.Eligible[0].HeroId);
            Assert.Equal(108, selection.Eligible[1].HeroId);
            Assert.Equal(6, selection.Rejections.Count);
        }

        [Fact]
        public void Filter_IgnoresLevelInPvp()
        {
            var all = new List<HeroCandidate>
            {
                Card(0, 101, pveLevel: 1),
                Card(1, 102, pveLevel: 0)
            };

            var selection = HeroPicker.Filter(all, isPve: false, requireOwned: true, maxLevel: 6);

            Assert.Equal(2, selection.Eligible.Count);
            Assert.Empty(selection.Rejections);
        }

        [Fact]
        public void Filter_AllowsTrialHeroesWhenRequireOwnedIsOff()
        {
            var all = new List<HeroCandidate> { Card(0, 101, owned: false, pveLevel: 6) };

            var strict = HeroPicker.Filter(all, isPve: true, requireOwned: true, maxLevel: 6);
            Assert.Empty(strict.Eligible);

            var relaxed = HeroPicker.Filter(all, isPve: true, requireOwned: false, maxLevel: 6);
            Assert.Single(relaxed.Eligible);
        }

        [Fact]
        public void Filter_HandlesNullAndEmpty()
        {
            Assert.Empty(HeroPicker.Filter(null, true, true, 6).Eligible);
            Assert.Empty(HeroPicker.Filter(new List<HeroCandidate>(), true, true, 6).Eligible);

            var withNull = new List<HeroCandidate> { null, Card(1, 102) };
            var selection = HeroPicker.Filter(withNull, true, true, 6);
            Assert.Single(selection.Eligible);
        }

        [Fact]
        public void Reject_ReportsFirstFailedRule()
        {
            // 判定优先级：grayed → locked → banned → taken → not-owned → level
            Assert.Equal("grayed", HeroPicker.Reject(
                Card(0, 101, owned: false, banned: true, grayed: true), true, true, 6));
            Assert.Equal("locked", HeroPicker.Reject(Card(0, 101, banned: true, locked: true), true, true, 6));
            Assert.Equal("banned", HeroPicker.Reject(Card(0, 101, taken: true, banned: true), true, true, 6));
            Assert.Equal("taken", HeroPicker.Reject(Card(0, 101, owned: false, taken: true), true, true, 6));
            Assert.Equal("not-owned", HeroPicker.Reject(Card(0, 101, owned: false), true, true, 6));
            Assert.Equal("level", HeroPicker.Reject(Card(0, 101, pveLevel: 3), true, true, 6));
            Assert.Null(HeroPicker.Reject(Card(0, 101), true, true, 6));
        }
    }
}
