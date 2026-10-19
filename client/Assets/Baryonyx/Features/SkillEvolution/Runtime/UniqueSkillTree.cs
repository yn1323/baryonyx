using System;
using System.Collections.Generic;
using Baryonyx.Combat;

namespace Baryonyx.SkillEvolution
{
    /// <summary>
    /// The shape every unique skill's evolution tree shares (doc/features/skill-evolution.md):
    /// the first stage branches three ways and each branch goes one stage further, seven stages
    /// in all. Stage 0 is the first; stages 1 and 2 are the first branch's two steps, 3 and 4 the
    /// second's, 5 and 6 the third's.
    /// </summary>
    public static class UniqueSkillTreeShape
    {
        public const int Count = 7;
        public const int Branches = 3;

        private static readonly int[] RootChildren = { 1, 3, 5 };

        public static bool IsStage(int stage) => stage >= 0 && stage < Count;

        /// <summary>The stage this one evolves from, or -1 for the first stage.</summary>
        public static int Parent(int stage) =>
            !IsStage(stage) || stage == 0 ? -1
            : Step(stage) == 1 ? 0
            : stage - 1;

        /// <summary>The stages this one evolves into: three from the first, one from a first step, none from a last.</summary>
        public static IReadOnlyList<int> Children(int stage) =>
            stage == 0 ? RootChildren
            : IsStage(stage) && Step(stage) == 1 ? new[] { stage + 1 }
            : Array.Empty<int>();

        /// <summary>The branch of the stage (0 to 2), or -1 for the first stage.</summary>
        public static int Branch(int stage) => IsStage(stage) && stage > 0 ? (stage - 1) / 2 : -1;

        /// <summary>How many evolutions the stage is from the first: 0, 1 or 2.</summary>
        public static int Step(int stage) =>
            !IsStage(stage) || stage == 0 ? 0
            : stage % 2 == 1 ? 1
            : 2;

        /// <summary>The stage at <paramref name="step"/> (1 or 2) of <paramref name="branch"/>.</summary>
        public static int StageAt(int branch, int step) => 1 + branch * 2 + (step - 1);
    }

    /// <summary>One stage of a tree: its card and the card art it shows.</summary>
    public sealed class UniqueSkillStage
    {
        public CardSkill Card;

        // カードの挿絵のファイル名（"CardUnique" + ツリー + 段階）。
        public string Art;
    }

    /// <summary>
    /// One unique skill's evolution tree: a stage for each of the seven places of
    /// <see cref="UniqueSkillTreeShape"/>, null where the stage is not made yet.
    /// </summary>
    public sealed class UniqueSkillTree
    {
        public string Id;
        public IReadOnlyList<UniqueSkillStage> Stages = Array.Empty<UniqueSkillStage>();

        public UniqueSkillStage Root => StageAt(0);

        public UniqueSkillStage StageAt(int stage) =>
            stage >= 0 && stage < Stages.Count ? Stages[stage] : null;
    }

    /// <summary>
    /// What a character has of one unique skill's tree: the stages acquired (the first always)
    /// and the one set in the formation. Mock data gives the starting progress.
    /// </summary>
    [Serializable]
    public sealed class UniqueSkillProgress
    {
        // 編成でセットしている段階（0が最初の段階）。
        public int Set;

        // 進化で取得した段階。最初の段階（0）は書かなくても取得している。
        public int[] Acquired = Array.Empty<int>();

        public bool Has(int stage) =>
            stage == 0
            || (
                UniqueSkillTreeShape.IsStage(stage)
                && Acquired != null
                && Array.IndexOf(Acquired, stage) >= 0
            );

        /// <summary>The stage set, falling back to the first when it is not one acquired.</summary>
        public int SetStage => Has(Set) ? Set : 0;

        public UniqueSkillProgress Copy() =>
            new() { Set = Set, Acquired = (int[])(Acquired ?? Array.Empty<int>()).Clone() };
    }
}
