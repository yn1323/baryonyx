using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Baryonyx.Account;

namespace Baryonyx.Combat.Records
{
    /// <summary>Where the weaknesses a player has revealed are read and saved.</summary>
    public interface IRevealedWeaknessSource
    {
        Task<IReadOnlyList<RevealedWeakness>> LoadAsync(CancellationToken token);

        /// <summary>Adds the weaknesses and returns all those revealed. Sending one again changes nothing.</summary>
        Task<IReadOnlyList<RevealedWeakness>> RevealAsync(
            IReadOnlyList<RevealedWeakness> weaknesses,
            CancellationToken token
        );
    }

    /// <summary>
    /// Reads and saves the revealed weaknesses on the game server with the shared session, so
    /// each player has their own.
    /// </summary>
    public sealed class RevealedWeaknessServerSource : IRevealedWeaknessSource
    {
        private readonly IAccountSessionRunner sessions;
        private readonly RevealedWeaknessApiClient api;

        public RevealedWeaknessServerSource(
            IAccountSessionRunner sessions,
            RevealedWeaknessApiClient api
        )
        {
            this.sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
            this.api = api ?? throw new ArgumentNullException(nameof(api));
        }

        public async Task<IReadOnlyList<RevealedWeakness>> LoadAsync(CancellationToken token) =>
            ToList(
                await sessions.WithSessionAsync(session => api.ReadAsync(session, token), token)
            );

        public async Task<IReadOnlyList<RevealedWeakness>> RevealAsync(
            IReadOnlyList<RevealedWeakness> weaknesses,
            CancellationToken token
        )
        {
            var body = (weaknesses ?? Array.Empty<RevealedWeakness>())
                .Select(weakness => new RevealedWeaknessApiClient.Weakness
                {
                    enemyId = weakness.EnemyId,
                    element = CardElementKeys.Key(weakness.Element),
                })
                .ToArray();
            return ToList(
                await sessions.WithSessionAsync(
                    session => api.RevealAsync(session, body, token),
                    token
                )
            );
        }

        // 知らない属性や、敵のない行は外す。
        public static IReadOnlyList<RevealedWeakness> ToList(RevealedWeaknessApiClient.State state)
        {
            var list = new List<RevealedWeakness>();
            foreach (
                var entry in state?.weaknesses ?? Array.Empty<RevealedWeaknessApiClient.Weakness>()
            )
                if (
                    entry != null
                    && !string.IsNullOrEmpty(entry.enemyId)
                    && CardElementKeys.TryParse(entry.element, out var element)
                )
                    list.Add(new RevealedWeakness(entry.enemyId, element));
            return list;
        }
    }

    /// <summary>
    /// Keeps the revealed weaknesses only while the app runs, without a server (the showcase,
    /// or no server URL).
    /// </summary>
    public sealed class RevealedWeaknessMemorySource : IRevealedWeaknessSource
    {
        private readonly RevealedWeaknesses kept = new();

        public Task<IReadOnlyList<RevealedWeakness>> LoadAsync(CancellationToken token) =>
            Task.FromResult<IReadOnlyList<RevealedWeakness>>(kept.All.ToArray());

        public Task<IReadOnlyList<RevealedWeakness>> RevealAsync(
            IReadOnlyList<RevealedWeakness> weaknesses,
            CancellationToken token
        )
        {
            kept.AddRange(weaknesses);
            return LoadAsync(token);
        }
    }
}
