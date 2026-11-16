using System;
using System.Collections.Generic;

namespace RandomHeroSelectMod
{
    /// <summary>
    /// 随机点击顺序。纯逻辑，无游戏引用，可用固定种子复现。
    ///
    /// 关键约束：面板的 <c>RequestChoiceHero</c> 会 <c>Retain()</c> 住**这张卡自己**的事件桥，
    /// 回包后才 <c>Release()</c>。所以对同一张卡的连续点击会被静默丢弃，而不同卡互不影响。
    /// 因此顺序按"洗牌后的整副牌"铺开：同一轮里每张最多点一次，数量超过候选数时换下一副。
    /// </summary>
    internal static class ShufflePlan
    {
        /// <summary>
        /// 生成点击序列（元素是候选列表里的下标）。
        /// </summary>
        /// <param name="candidateCount">候选数量。&lt;= 0 时返回空数组。</param>
        /// <param name="clicks">想要的点击次数。&lt;= 0 时返回空数组。</param>
        /// <param name="random">随机源；传 null 用真随机。</param>
        internal static int[] Build(int candidateCount, int clicks, Random random)
        {
            if (candidateCount <= 0 || clicks <= 0) return Array.Empty<int>();
            random = random ?? new Random();

            var sequence = new List<int>(clicks);
            while (sequence.Count < clicks)
            {
                int[] deck = Shuffle(candidateCount, random);
                // 换副牌时避免和上一张重复，减少被 Retain 吃掉的概率。
                int start = 0;
                if (sequence.Count > 0 && deck.Length > 1 && deck[0] == sequence[sequence.Count - 1])
                {
                    start = 1;
                }
                for (int i = start; i < deck.Length && sequence.Count < clicks; i++)
                {
                    sequence.Add(deck[i]);
                }
            }
            return sequence.ToArray();
        }

        /// <summary>Fisher–Yates 洗牌。</summary>
        internal static int[] Shuffle(int count, Random random)
        {
            if (count <= 0) return Array.Empty<int>();
            random = random ?? new Random();

            var deck = new int[count];
            for (int i = 0; i < count; i++) deck[i] = i;
            for (int i = count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                int tmp = deck[i];
                deck[i] = deck[j];
                deck[j] = tmp;
            }
            return deck;
        }

        /// <summary>从候选里随机挑一个（终选用）。空集合返回 -1。</summary>
        internal static int PickFinal(int candidateCount, Random random)
        {
            if (candidateCount <= 0) return -1;
            random = random ?? new Random();
            return random.Next(candidateCount);
        }

        /// <summary>间隔抖动：在基准值上做 ±<paramref name="jitter"/> 比例的浮动。</summary>
        internal static int Jitter(int milliseconds, double jitter, Random random)
        {
            if (milliseconds <= 0) return 0;
            if (jitter <= 0) return milliseconds;
            random = random ?? new Random();
            double factor = 1.0 + (random.NextDouble() * 2.0 - 1.0) * jitter;
            int value = (int)Math.Round(milliseconds * factor);
            return value < 0 ? 0 : value;
        }
    }
}
