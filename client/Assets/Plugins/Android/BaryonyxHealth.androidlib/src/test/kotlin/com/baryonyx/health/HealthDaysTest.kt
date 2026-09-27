package com.baryonyx.health

import org.junit.Assert.assertEquals
import org.junit.Test
import java.time.Instant
import java.time.LocalDate
import java.time.ZoneId

class HealthDaysTest {
    private val tokyo = ZoneId.of("Asia/Tokyo")

    @Test
    fun dayStartsAt4amLocalTime() {
        assertEquals(Instant.parse("2026-09-26T19:00:00Z"), HealthDays.start(LocalDate.of(2026, 9, 27), tokyo))
    }

    @Test
    fun stepsBefore4amBelongToThePreviousDay() {
        assertEquals(LocalDate.of(2026, 9, 26), HealthDays.today(Instant.parse("2026-09-26T18:59:59Z"), tokyo))
        assertEquals(LocalDate.of(2026, 9, 27), HealthDays.today(Instant.parse("2026-09-26T19:00:00Z"), tokyo))
    }
}
