package com.baryonyx.health

import androidx.health.connect.client.permission.HealthPermission
import androidx.health.connect.client.records.*
import org.json.JSONArray
import org.json.JSONObject
import java.time.Instant
import java.time.ZoneOffset
import kotlin.reflect.KClass

/** One definition per supported type: name, permission, time and JSON values stay together. */
internal object HealthRecordCatalog {
    data class RecordTime(val start: Instant, val offset: ZoneOffset?,
        val end: Instant? = null, val endOffset: ZoneOffset? = null)

    private class Definition<T : Record>(
        val name: String,
        val type: KClass<T>,
        private val time: (T) -> RecordTime,
        private val write: (JSONObject, T) -> Unit,
    ) {
        fun timeOf(record: Record) = time(checkNotNull(type.java.cast(record)))
        fun writeValues(json: JSONObject, record: Record) = write(json, checkNotNull(type.java.cast(record)))
    }

    // The SDK's instantaneous/interval interfaces are internal; each definition reads its concrete type.
    private val definitions = listOf(
        Definition("steps", StepsRecord::class,
            { record -> RecordTime(record.startTime, record.startZoneOffset, record.endTime, record.endZoneOffset) },
            { json, record ->
                val metadata = record.metadata
                json.put("count", record.count).put("id", metadata.id)
                    .put("clientRecordId", metadata.clientRecordId ?: JSONObject.NULL)
                    .put("clientRecordVersion", metadata.clientRecordVersion)
                    .put("device", metadata.device?.let {
                        JSONObject().put("type", it.type)
                            .put("manufacturer", it.manufacturer ?: JSONObject.NULL)
                            .put("model", it.model ?: JSONObject.NULL)
                    } ?: JSONObject.NULL)
            }),
        Definition("weight", WeightRecord::class,
            { record -> RecordTime(record.time, record.zoneOffset) },
            { json, record ->
                json.put("kilograms", record.weight.inKilograms)
            }),
        Definition("bodyFat", BodyFatRecord::class,
            { record -> RecordTime(record.time, record.zoneOffset) },
            { json, record ->
                json.put("percent", record.percentage.value)
            }),
        Definition("height", HeightRecord::class,
            { record -> RecordTime(record.time, record.zoneOffset) },
            { json, record ->
                json.put("meters", record.height.inMeters)
            }),
        Definition("bloodPressure", BloodPressureRecord::class,
            { record -> RecordTime(record.time, record.zoneOffset) },
            { json, record ->
                json.put("systolicMmHg", record.systolic.inMillimetersOfMercury)
                    .put("diastolicMmHg", record.diastolic.inMillimetersOfMercury)
                    .put("bodyPosition", record.bodyPosition).put("measurementLocation", record.measurementLocation)
            }),
        Definition("heartRate", HeartRateRecord::class,
            { record -> RecordTime(record.startTime, record.startZoneOffset, record.endTime, record.endZoneOffset) },
            { json, record ->
                val samples = JSONArray()
                record.samples.take(HealthRecords.SAMPLE_LIMIT).forEach {
                    samples.put(JSONObject().put("time", it.time.toString()).put("beatsPerMinute", it.beatsPerMinute))
                }
                json.put("samples", samples).put("sampleCount", record.samples.size)
                    .put("samplesTruncated", record.samples.size > HealthRecords.SAMPLE_LIMIT)
            }),
        Definition("restingHeartRate", RestingHeartRateRecord::class,
            { record -> RecordTime(record.time, record.zoneOffset) },
            { json, record ->
                json.put("beatsPerMinute", record.beatsPerMinute)
            }),
        Definition("oxygenSaturation", OxygenSaturationRecord::class,
            { record -> RecordTime(record.time, record.zoneOffset) },
            { json, record ->
                json.put("percent", record.percentage.value)
            }),
        Definition("respiratoryRate", RespiratoryRateRecord::class,
            { record -> RecordTime(record.time, record.zoneOffset) },
            { json, record ->
                json.put("ratePerMinute", record.rate)
            }),
        Definition("bodyTemperature", BodyTemperatureRecord::class,
            { record -> RecordTime(record.time, record.zoneOffset) },
            { json, record ->
                json.put("celsius", record.temperature.inCelsius)
                    .put("measurementLocation", record.measurementLocation)
            }),
        Definition("bloodGlucose", BloodGlucoseRecord::class,
            { record -> RecordTime(record.time, record.zoneOffset) },
            { json, record ->
                json.put("millimolesPerLiter", record.level.inMillimolesPerLiter)
                    .put("specimenSource", record.specimenSource).put("mealType", record.mealType)
                    .put("relationToMeal", record.relationToMeal)
            }),
        Definition("sleep", SleepSessionRecord::class,
            { record -> RecordTime(record.startTime, record.startZoneOffset, record.endTime, record.endZoneOffset) },
            { json, record ->
                val stages = JSONArray()
                record.stages.take(HealthRecords.SAMPLE_LIMIT).forEach {
                    stages.put(JSONObject().put("startAt", it.startTime.toString())
                        .put("endAt", it.endTime.toString()).put("stage", it.stage))
                }
                json.put("stages", stages).put("stageCount", record.stages.size)
                    .put("stagesTruncated", record.stages.size > HealthRecords.SAMPLE_LIMIT)
                    .put("title", record.title ?: JSONObject.NULL).put("notes", record.notes ?: JSONObject.NULL)
            }),
        Definition("distance", DistanceRecord::class,
            { record -> RecordTime(record.startTime, record.startZoneOffset, record.endTime, record.endZoneOffset) },
            { json, record ->
                json.put("meters", record.distance.inMeters)
            }),
        Definition("activeCaloriesBurned", ActiveCaloriesBurnedRecord::class,
            { record -> RecordTime(record.startTime, record.startZoneOffset, record.endTime, record.endZoneOffset) },
            { json, record ->
                json.put("kilocalories", record.energy.inKilocalories)
            }),
        Definition("totalCaloriesBurned", TotalCaloriesBurnedRecord::class,
            { record -> RecordTime(record.startTime, record.startZoneOffset, record.endTime, record.endZoneOffset) },
            { json, record ->
                json.put("kilocalories", record.energy.inKilocalories)
            }),
        Definition("exercise", ExerciseSessionRecord::class,
            { record -> RecordTime(record.startTime, record.startZoneOffset, record.endTime, record.endZoneOffset) },
            { json, record ->
                json.put("exerciseType", record.exerciseType)
                    .put("title", record.title ?: JSONObject.NULL).put("notes", record.notes ?: JSONObject.NULL)
            }),
    )
    val types: Map<String, KClass<out Record>> = definitions.associate { it.name to it.type }
    val permissions = types.values.map { HealthPermission.getReadPermission(it) }.toSet()
    private val byType = definitions.associateBy { it.type }

    private fun definition(record: Record) =
        byType[record::class] ?: error("Unsupported health record")

    fun timeOf(record: Record) = definition(record).timeOf(record)

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
        definition(record).writeValues(json, record)
        return json
    }
}
