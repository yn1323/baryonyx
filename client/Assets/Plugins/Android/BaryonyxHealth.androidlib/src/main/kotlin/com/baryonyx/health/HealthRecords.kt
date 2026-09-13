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
import java.time.ZoneOffset
import kotlin.coroutines.coroutineContext
import kotlin.reflect.KClass

/** Foreground, read-only inspection. These records are never used to compute step totals. */
internal object HealthRecords {
    const val RECORD_LIMIT = 200
    const val SAMPLE_LIMIT = 240
    val types: Map<String, KClass<out Record>> = linkedMapOf(
        "steps" to StepsRecord::class,
        "weight" to WeightRecord::class,
        "bodyFat" to BodyFatRecord::class,
        "height" to HeightRecord::class,
        "bloodPressure" to BloodPressureRecord::class,
        "heartRate" to HeartRateRecord::class,
        "restingHeartRate" to RestingHeartRateRecord::class,
        "oxygenSaturation" to OxygenSaturationRecord::class,
        "respiratoryRate" to RespiratoryRateRecord::class,
        "bodyTemperature" to BodyTemperatureRecord::class,
        "bloodGlucose" to BloodGlucoseRecord::class,
        "sleep" to SleepSessionRecord::class,
        "distance" to DistanceRecord::class,
        "activeCaloriesBurned" to ActiveCaloriesBurnedRecord::class,
        "totalCaloriesBurned" to TotalCaloriesBurnedRecord::class,
        "exercise" to ExerciseSessionRecord::class,
    )
    val permissions = types.values
        .map { HealthPermission.getReadPermission(it) }.toSet()

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
                val time = timeOf(record)
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

    fun recordJson(record: Record): JSONObject {
        val json = JSONObject().put("sourceApp", record.metadata.dataOrigin.packageName)
            .put("lastModifiedAt", record.metadata.lastModifiedTime.toString())
            .put("recordingMethod", record.metadata.recordingMethod)
        val time = timeOf(record)
        if (time.end == null) json.put("time", time.start.toString())
            .put("zoneOffset", time.offset?.toString() ?: JSONObject.NULL)
        else json.put("startAt", time.start.toString()).put("endAt", time.end.toString())
            .put("startZoneOffset", time.offset?.toString() ?: JSONObject.NULL)
            .put("endZoneOffset", time.endOffset?.toString() ?: JSONObject.NULL)
        when (record) {
            is StepsRecord -> {
                val metadata = record.metadata
                json.put("count", record.count).put("id", metadata.id)
                    .put("clientRecordId", metadata.clientRecordId ?: JSONObject.NULL)
                    .put("clientRecordVersion", metadata.clientRecordVersion)
                    .put("device", metadata.device?.let {
                        JSONObject().put("type", it.type)
                            .put("manufacturer", it.manufacturer ?: JSONObject.NULL)
                            .put("model", it.model ?: JSONObject.NULL)
                    } ?: JSONObject.NULL)
            }
            is WeightRecord -> json.put("kilograms", record.weight.inKilograms)
            is BodyFatRecord -> json.put("percent", record.percentage.value)
            is HeightRecord -> json.put("meters", record.height.inMeters)
            is BloodPressureRecord -> json.put("systolicMmHg", record.systolic.inMillimetersOfMercury)
                .put("diastolicMmHg", record.diastolic.inMillimetersOfMercury)
                .put("bodyPosition", record.bodyPosition).put("measurementLocation", record.measurementLocation)
            is RestingHeartRateRecord -> json.put("beatsPerMinute", record.beatsPerMinute)
            is OxygenSaturationRecord -> json.put("percent", record.percentage.value)
            is RespiratoryRateRecord -> json.put("ratePerMinute", record.rate)
            is BodyTemperatureRecord -> json.put("celsius", record.temperature.inCelsius)
                .put("measurementLocation", record.measurementLocation)
            is BloodGlucoseRecord -> json.put("millimolesPerLiter", record.level.inMillimolesPerLiter)
                .put("specimenSource", record.specimenSource).put("mealType", record.mealType)
                .put("relationToMeal", record.relationToMeal)
            is DistanceRecord -> json.put("meters", record.distance.inMeters)
            is ActiveCaloriesBurnedRecord -> json.put("kilocalories", record.energy.inKilocalories)
            is TotalCaloriesBurnedRecord -> json.put("kilocalories", record.energy.inKilocalories)
            is HeartRateRecord -> {
                val samples = JSONArray()
                record.samples.take(SAMPLE_LIMIT).forEach {
                    samples.put(JSONObject().put("time", it.time.toString()).put("beatsPerMinute", it.beatsPerMinute))
                }
                json.put("samples", samples).put("sampleCount", record.samples.size)
                    .put("samplesTruncated", record.samples.size > SAMPLE_LIMIT)
            }
            is SleepSessionRecord -> {
                val stages = JSONArray()
                record.stages.take(SAMPLE_LIMIT).forEach {
                    stages.put(JSONObject().put("startAt", it.startTime.toString())
                        .put("endAt", it.endTime.toString()).put("stage", it.stage))
                }
                json.put("stages", stages).put("stageCount", record.stages.size)
                    .put("stagesTruncated", record.stages.size > SAMPLE_LIMIT)
                    .put("title", record.title ?: JSONObject.NULL).put("notes", record.notes ?: JSONObject.NULL)
            }
            is ExerciseSessionRecord -> json.put("exerciseType", record.exerciseType)
                .put("title", record.title ?: JSONObject.NULL).put("notes", record.notes ?: JSONObject.NULL)
        }
        return json
    }

    private data class RecordTime(val start: Instant, val offset: ZoneOffset?,
        val end: Instant? = null, val endOffset: ZoneOffset? = null)

    // The SDK's InstantaneousRecord/IntervalRecord interfaces are internal in 1.1.0.
    private fun timeOf(record: Record): RecordTime = when (record) {
        is StepsRecord -> RecordTime(record.startTime, record.startZoneOffset, record.endTime, record.endZoneOffset)
        is WeightRecord -> RecordTime(record.time, record.zoneOffset)
        is BodyFatRecord -> RecordTime(record.time, record.zoneOffset)
        is HeightRecord -> RecordTime(record.time, record.zoneOffset)
        is BloodPressureRecord -> RecordTime(record.time, record.zoneOffset)
        is RestingHeartRateRecord -> RecordTime(record.time, record.zoneOffset)
        is OxygenSaturationRecord -> RecordTime(record.time, record.zoneOffset)
        is RespiratoryRateRecord -> RecordTime(record.time, record.zoneOffset)
        is BodyTemperatureRecord -> RecordTime(record.time, record.zoneOffset)
        is BloodGlucoseRecord -> RecordTime(record.time, record.zoneOffset)
        is HeartRateRecord -> RecordTime(record.startTime, record.startZoneOffset, record.endTime, record.endZoneOffset)
        is SleepSessionRecord -> RecordTime(record.startTime, record.startZoneOffset, record.endTime, record.endZoneOffset)
        is DistanceRecord -> RecordTime(record.startTime, record.startZoneOffset, record.endTime, record.endZoneOffset)
        is ActiveCaloriesBurnedRecord -> RecordTime(record.startTime, record.startZoneOffset, record.endTime, record.endZoneOffset)
        is TotalCaloriesBurnedRecord -> RecordTime(record.startTime, record.startZoneOffset, record.endTime, record.endZoneOffset)
        is ExerciseSessionRecord -> RecordTime(record.startTime, record.startZoneOffset, record.endTime, record.endZoneOffset)
        else -> error("Unsupported health record")
    }
}
