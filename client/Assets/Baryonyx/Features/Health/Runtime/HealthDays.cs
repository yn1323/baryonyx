using System;

namespace Baryonyx.Health
{
    // ゲームの1日は現地時刻の朝4時に切り替わる。4時より前の歩数は前日に数える。
    // 端末の集計（HealthBridge.kt の HealthDays）とサーバーの検証（DAY_START_HOUR）も同じ時刻を使う。
    public static class HealthDays
    {
        public const int StartHour = 4;

        public static DateTime DayOf(DateTime localTime) => localTime.AddHours(-StartHour).Date;

        public static DateTime Today() => DayOf(DateTime.Now);
    }
}
