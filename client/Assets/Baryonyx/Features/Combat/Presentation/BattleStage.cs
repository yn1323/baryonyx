using UnityEngine;

namespace Baryonyx.Combat.Presentation
{
    /// <summary>
    /// The places a battle can be fought in: one 3D stage (HD-2D) each, chosen on the battle
    /// scene's <see cref="BattleStageSelector"/>. The names in the Inspector say where it is.
    /// </summary>
    public enum BattleStage
    {
        [InspectorName("夕暮れの高原")]
        DuskHighland,

        [InspectorName("草原の街道（昼）")]
        MeadowRoad,

        [InspectorName("朝霧の森")]
        MistyWoods,

        [InspectorName("坑道")]
        MineTunnel,

        [InspectorName("水晶の洞窟")]
        CrystalCavern,

        [InspectorName("海辺の岩場（真昼）")]
        RockyShore,

        [InspectorName("霧の沼地")]
        MistySwamp,

        [InspectorName("雪原")]
        SnowField,

        [InspectorName("火山")]
        VolcanoCrater,

        [InspectorName("夜の墓地")]
        MoonlitGraveyard,

        [InspectorName("古城の大広間")]
        CastleHall,
    }
}
