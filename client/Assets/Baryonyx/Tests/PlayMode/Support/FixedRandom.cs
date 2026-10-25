using System;
using Random = UnityEngine.Random;

namespace Baryonyx.Tests.PlayMode
{
    // Unityの乱数を決まった種から始め、終わったら元の乱数の続きへ戻す。手札を配る順や狙う相手、
    // 敵の行動、演出の揺らぎが乱数で決まるテストを、いつも同じ流れにする。
    public sealed class FixedRandom : IDisposable
    {
        public const int DefaultSeed = 1;

        private readonly Random.State saved;

        private FixedRandom(int seed)
        {
            saved = Random.state;
            Random.InitState(seed);
        }

        public static FixedRandom Use(int seed = DefaultSeed) => new(seed);

        public void Dispose() => Random.state = saved;
    }
}
