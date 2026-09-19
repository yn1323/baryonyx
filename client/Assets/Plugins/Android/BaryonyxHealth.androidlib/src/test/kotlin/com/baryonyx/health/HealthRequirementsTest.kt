package com.baryonyx.health

import androidx.health.connect.client.permission.HealthPermission
import androidx.health.connect.client.records.WeightRecord
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.runBlocking
import org.junit.Assert.*
import org.junit.Test

class HealthRequirementsTest {
    private val allowed = setOf(HealthRequirements.stepsPermission)

    @Test fun weightPermissionDoesNotPermitStepQuery() = runBlocking {
        val weight = setOf(HealthPermission.getReadPermission(WeightRecord::class))
        assertEquals("permission_required", HealthRequirements.read(weight,
            { error("Must not query steps") }, { error("Must not query again") }))
    }

    @Test fun zeroIsDataAndNullIsEmpty() = runBlocking {
        assertEquals("steps_available", HealthRequirements.read(allowed, { 0L }, { allowed }))
        assertEquals("steps_empty", HealthRequirements.read(allowed, { null }, { allowed }))
    }

    @Test fun revocationDiscardsAggregatedValue() = runBlocking {
        assertEquals("permission_required", HealthRequirements.read(allowed, { 100L }, { emptySet() }))
        assertEquals("permission_required", HealthRequirements.read(allowed, { throw SecurityException() }, { allowed }))
    }

    @Test fun failuresStayDistinctFromEmptyAndCancellationPropagates() = runBlocking {
        assertEquals("steps_failed", HealthRequirements.read(allowed, { throw IllegalStateException() }, { allowed }))
        assertEquals("steps_failed", HealthRequirements.read(allowed, { 10L }, { throw IllegalStateException() }))
        try {
            HealthRequirements.read(allowed, { throw CancellationException() }, { allowed })
            fail("Cancellation must propagate")
        } catch (_: CancellationException) { }
    }
}
