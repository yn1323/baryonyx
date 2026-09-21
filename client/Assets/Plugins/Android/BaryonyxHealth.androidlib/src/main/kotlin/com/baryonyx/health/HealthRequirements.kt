package com.baryonyx.health

import androidx.health.connect.client.HealthConnectClient
import androidx.health.connect.client.permission.HealthPermission
import androidx.health.connect.client.records.StepsRecord
import androidx.health.connect.client.request.AggregateRequest
import androidx.health.connect.client.time.TimeRangeFilter
import kotlinx.coroutines.CancellationException
import java.time.Instant
import java.time.ZoneId

object HealthRequirements {
    val stepsPermission = HealthPermission.getReadPermission(StepsRecord::class)

    suspend fun read(client: HealthConnectClient, granted: Set<String>): String {
        val now = Instant.now()
        val zone = ZoneId.systemDefault()
        val start = now.atZone(zone).toLocalDate().minusDays(6).atStartOfDay(zone).toInstant()
        return read(granted,
            { client.aggregate(AggregateRequest(setOf(StepsRecord.COUNT_TOTAL), TimeRangeFilter.between(start, now)))[StepsRecord.COUNT_TOTAL] },
            { client.permissionController.getGrantedPermissions() })
    }

    internal suspend fun read(granted: Set<String>, aggregate: suspend () -> Long?, latestPermissions: suspend () -> Set<String>): String {
        if (stepsPermission !in granted) return "permission_required"
        return try {
            val count = aggregate()
            if (stepsPermission !in latestPermissions()) "permission_required"
            else if (count == null) "steps_empty" else "steps_available"
        } catch (e: CancellationException) { throw e }
        catch (_: SecurityException) { "permission_required" }
        catch (_: Exception) { "steps_failed" }
    }
}
