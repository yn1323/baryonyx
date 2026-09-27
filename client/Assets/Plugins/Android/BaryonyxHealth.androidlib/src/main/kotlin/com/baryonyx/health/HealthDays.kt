package com.baryonyx.health

import java.time.Instant
import java.time.LocalDate
import java.time.ZoneId

/** The game day switches at 4:00 local time; steps before 4:00 count toward the previous day. */
internal object HealthDays {
    // Keep in sync with HealthDays.cs on the client and DAY_START_HOUR on the server.
    const val START_HOUR = 4

    fun start(date: LocalDate, zone: ZoneId): Instant = date.atTime(START_HOUR, 0).atZone(zone).toInstant()

    fun today(now: Instant, zone: ZoneId): LocalDate {
        val date = now.atZone(zone).toLocalDate()
        return if (now < start(date, zone)) date.minusDays(1) else date
    }
}
