package com.baryonyx.health

import androidx.health.connect.client.HealthConnectClient
import androidx.health.connect.client.permission.HealthPermission
import androidx.health.connect.client.records.*
import androidx.health.connect.client.request.ReadRecordsRequest
import androidx.health.connect.client.time.TimeRangeFilter
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.ensureActive
import org.json.JSONArray
import org.json.JSONObject
import java.time.Instant
import kotlin.coroutines.coroutineContext
import kotlin.reflect.KClass

/** Foreground, read-only inspection. These records are never used to compute step totals. */
internal object HealthRecords {
    const val RECORD_LIMIT = 200
    const val SAMPLE_LIMIT = 240
    val types = HealthRecordCatalog.types
    val permissions = HealthRecordCatalog.permissions

    fun hasPermission(granted: Set<String>) = permissions.any { it in granted }

    data class Page(val records: List<Record>, val nextToken: String?)
    data class Result(val status: String, val records: List<Record> = emptyList(), val truncated: Boolean = false)

    suspend fun readWeek(client: HealthConnectClient, granted: Set<String>, start: Instant, end: Instant) =
        readWeek(granted) { type, token ->
            val response = client.readRecords(ReadRecordsRequest(
                recordType = type,
                timeRangeFilter = TimeRangeFilter.between(start, end),
                ascendingOrder = false,
                pageSize = 100,
                pageToken = token,
            ))
            Page(response.records, response.pageToken)
        }

    // Separate SDK calls from paging and permission handling so they can be tested on the JVM.
    suspend fun readWeek(
        granted: Set<String>,
        readPage: suspend (KClass<out Record>, String?) -> Page,
    ): Map<String, Result> {
        val results = linkedMapOf<String, Result>()
        for ((name, type) in types) {
            coroutineContext.ensureActive()
            if (HealthPermission.getReadPermission(type) !in granted) {
                results[name] = Result("permission_required")
                continue
            }
            try {
                val records = mutableListOf<Record>()
                var token: String? = null
                val seenTokens = mutableSetOf<String>()
                do {
                    coroutineContext.ensureActive()
                    val page = readPage(type, token)
                    records.addAll(page.records.take(RECORD_LIMIT - records.size))
                    token = page.nextToken
                    if (token != null && !seenTokens.add(token)) error("Repeated page token")
                } while (token != null && records.size < RECORD_LIMIT)
                results[name] = Result("success", records, token != null)
            } catch (e: CancellationException) {
                throw e
            } catch (_: SecurityException) {
                // Discard partial pages when access is revoked during the read.
                results[name] = Result("permission_required")
            } catch (_: Exception) {
                results[name] = Result("failed")
            }
        }
        return results
    }

    fun dayJson(results: Map<String, Result>, start: Instant, end: Instant): JSONObject {
        val json = JSONObject()
        for ((name, result) in results) {
            val records = JSONArray()
            for (record in result.records) {
                val time = HealthRecordCatalog.timeOf(record)
                val belongs = if (time.end == null) time.start >= start && time.start < end
                    else time.start < end && time.end > start
                if (belongs) records.put(recordJson(record))
            }
            val status = when {
                result.status != "success" -> result.status
                result.truncated -> "truncated"
                records.length() == 0 -> "empty"
                else -> "success"
            }
            json.put(name, JSONObject().put("status", status)
                .put("truncated", result.truncated).put("records", records))
        }
        return json
    }

    fun recordJson(record: Record): JSONObject = HealthRecordCatalog.recordJson(record)
}
