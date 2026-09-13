package com.baryonyx.health

import androidx.health.connect.client.permission.HealthPermission
import androidx.health.connect.client.records.*
import androidx.health.connect.client.records.metadata.DataOrigin
import androidx.health.connect.client.records.metadata.Device
import androidx.health.connect.client.records.metadata.Metadata
import androidx.health.connect.client.testing.populatedWithTestValues
import androidx.health.connect.client.units.*
import kotlinx.coroutines.CancellationException
import kotlinx.coroutines.runBlocking
import org.junit.Assert.*
import org.junit.Test
import java.time.Instant
import java.time.ZoneOffset

class HealthRecordsTest {
    private val start = Instant.parse("2026-09-12T15:00:00Z")
    private val end = start.plusSeconds(86400)
    private val metadata = Metadata.manualEntry()
    private val weightPermission = HealthPermission.getReadPermission(WeightRecord::class)
    private val stepsPermission = HealthPermission.getReadPermission(StepsRecord::class)
    private fun weight(time: Instant = start) = WeightRecord(time, ZoneOffset.ofHours(9), Mass.kilograms(62.5), metadata)

    private fun steps(source: String, count: Long, from: Instant = start, to: Instant = start.plusSeconds(60)) =
        StepsRecord(from, ZoneOffset.ofHours(9), to, ZoneOffset.ofHours(9), count,
            Metadata.autoRecorded(clientRecordId = "test-client-record", clientRecordVersion = 3,
                device = Device(Device.TYPE_PHONE, "Example", "Test phone"))
                .populatedWithTestValues(id = "test-record-$source", dataOrigin = DataOrigin(source), lastModifiedTime = end))

    @Test fun noPermissionDoesNotCallSdkAndWeightAloneIsEnough() = runBlocking {
        assertFalse(HealthRecords.hasPermission(emptySet()))
        assertTrue(HealthRecords.hasPermission(setOf(weightPermission)))
        val results = HealthRecords.readWeek(emptySet()) { _, _ -> error("Must not read without permission") }
        assertEquals(16, results.size)
        assertTrue(results.values.all { it.status == "permission_required" })
    }

    @Test fun readsAllPagesAndSkipsOtherTypes() = runBlocking {
        val tokens = mutableListOf<String?>()
        val results = HealthRecords.readWeek(setOf(weightPermission)) { type, token ->
            assertEquals(WeightRecord::class, type)
            tokens.add(token)
            if (token == null) HealthRecords.Page(listOf(weight()), "next")
            else HealthRecords.Page(listOf(weight(start.plusSeconds(60))), null)
        }
        assertEquals(listOf(null, "next"), tokens)
        val json = HealthRecords.dayJson(results, start, end)
        assertEquals(2, json.getJSONObject("weight").getJSONArray("records").length())
        assertEquals("permission_required", json.getJSONObject("sleep").getString("status"))
        assertFalse(json.getJSONObject("weight").getBoolean("truncated"))
    }

    @Test fun stepsKeepOverlappingFitAndDeviceRecordsWithOriginalMetadata() = runBlocking {
        val fit = steps("com.google.android.apps.fitness", 120)
        val phone = steps("com.android.healthconnect.phone.test", 110)
        val legacyPhone = steps("android", 100)
        val results = HealthRecords.readWeek(setOf(stepsPermission)) { type, _ ->
            assertEquals(StepsRecord::class, type)
            HealthRecords.Page(listOf(fit, phone, legacyPhone), null)
        }
        val json = HealthRecords.dayJson(results, start, end).getJSONObject("steps")
        assertEquals("success", json.getString("status"))
        val records = json.getJSONArray("records")
        assertEquals(3, records.length())
        listOf(fit, phone, legacyPhone).forEachIndexed { index, original ->
            val record = records.getJSONObject(index)
            assertEquals(original.count, record.getLong("count"))
            assertEquals(original.metadata.dataOrigin.packageName, record.getString("sourceApp"))
            assertEquals(original.metadata.id, record.getString("id"))
            assertEquals("test-client-record", record.getString("clientRecordId"))
            assertEquals(3L, record.getLong("clientRecordVersion"))
            assertEquals(end.toString(), record.getString("lastModifiedAt"))
            assertEquals(Metadata.RECORDING_METHOD_AUTOMATICALLY_RECORDED, record.getInt("recordingMethod"))
            assertEquals(original.startTime.toString(), record.getString("startAt"))
            assertEquals(original.endTime.toString(), record.getString("endAt"))
            assertEquals("+09:00", record.getString("startZoneOffset"))
            assertEquals("+09:00", record.getString("endZoneOffset"))
            assertEquals(Device.TYPE_PHONE, record.getJSONObject("device").getInt("type"))
            assertEquals("Example", record.getJSONObject("device").getString("manufacturer"))
            assertEquals("Test phone", record.getJSONObject("device").getString("model"))
        }
    }

    @Test fun stepsSpanningMidnightKeepFullOriginalCountAndInterval() {
        val spanning = steps("android", 120, start.minusSeconds(30), start.plusSeconds(30))
        val endingBefore = steps("android", 40, start.minusSeconds(60), start)
        val startingAfter = steps("android", 50, end, end.plusSeconds(60))
        val results = mapOf("steps" to HealthRecords.Result("success", listOf(spanning, endingBefore, startingAfter)))
        val records = HealthRecords.dayJson(results, start, end).getJSONObject("steps").getJSONArray("records")
        assertEquals(1, records.length())
        assertEquals(120L, records.getJSONObject(0).getLong("count"))
        assertEquals(spanning.startTime.toString(), records.getJSONObject(0).getString("startAt"))
        val previous = HealthRecords.dayJson(mapOf("steps" to HealthRecords.Result("success", listOf(spanning))),
            start.minusSeconds(86400), start).getJSONObject("steps").getJSONArray("records")
        assertEquals(120L, previous.getJSONObject(0).getLong("count"))
    }

    @Test fun stepsWithoutDeviceOrOffsetsKeepExplicitNulls() {
        val record = StepsRecord(start, null, start.plusSeconds(60), null, 1, metadata)
        val json = HealthRecords.recordJson(record)
        for (key in listOf("device", "clientRecordId", "startZoneOffset", "endZoneOffset")) {
            assertTrue(json.has(key))
            assertTrue(json.isNull(key))
        }
        val partialDevice = Metadata.autoRecorded(Device(Device.TYPE_PHONE))
        val device = HealthRecords.recordJson(StepsRecord(start, null, start.plusSeconds(60), null, 1, partialDevice))
            .getJSONObject("device")
        assertTrue(device.isNull("manufacturer"))
        assertTrue(device.isNull("model"))
    }

    @Test fun stepsPagingLimitMarksIncompleteDaysAndPermissionLossDiscardsPages() = runBlocking {
        var calls = 0
        val results = HealthRecords.readWeek(setOf(stepsPermission)) { type, _ ->
            assertEquals(StepsRecord::class, type)
            calls++
            HealthRecords.Page(List(100) { steps("android", 10) }, "page$calls")
        }
        assertEquals(2, calls)
        val day = HealthRecords.dayJson(results, start, end).getJSONObject("steps")
        assertEquals(200, day.getJSONArray("records").length())
        assertEquals("truncated", day.getString("status"))
        assertTrue(HealthRecords.dayJson(results, end, end.plusSeconds(86400)).getJSONObject("steps").getBoolean("truncated"))
        val revoked = HealthRecords.readWeek(setOf(stepsPermission)) { _, token ->
            if (token == null) HealthRecords.Page(listOf(steps("android", 10)), "next")
            else throw SecurityException()
        }
        assertEquals("permission_required", revoked.getValue("steps").status)
        assertTrue(revoked.getValue("steps").records.isEmpty())
    }

    @Test fun boundedReadsExplicitlyMarkUnvisitedDaysIncomplete() = runBlocking {
        var calls = 0
        val results = HealthRecords.readWeek(setOf(weightPermission)) { _, _ ->
            calls++
            HealthRecords.Page(List(100) { weight() }, "page$calls")
        }
        assertEquals(2, calls)
        assertEquals(200, results.getValue("weight").records.size)
        val emptyDay = HealthRecords.dayJson(results, end, end.plusSeconds(86400)).getJSONObject("weight")
        assertEquals("truncated", emptyDay.getString("status"))
        assertTrue(emptyDay.getBoolean("truncated"))
    }

    @Test fun permissionRevocationDiscardsPagesAndOtherTypesStillRead() = runBlocking {
        val bpPermission = HealthPermission.getReadPermission(BloodPressureRecord::class)
        val results = HealthRecords.readWeek(setOf(weightPermission, bpPermission)) { type, token ->
            when {
                type == BloodPressureRecord::class -> HealthRecords.Page(emptyList(), null)
                token == null -> HealthRecords.Page(listOf(weight()), "next")
                else -> throw SecurityException()
            }
        }
        assertEquals("permission_required", results.getValue("weight").status)
        assertTrue(results.getValue("weight").records.isEmpty())
        assertEquals("empty", HealthRecords.dayJson(results, start, end).getJSONObject("bloodPressure").getString("status"))
    }

    @Test fun readFailuresAreNotReportedAsEmptyAndCancellationPropagates() = runBlocking {
        val results = HealthRecords.readWeek(setOf(weightPermission)) { _, _ -> throw IllegalStateException() }
        assertEquals("failed", HealthRecords.dayJson(results, start, end).getJSONObject("weight").getString("status"))
        try {
            HealthRecords.readWeek(setOf(weightPermission)) { _, _ -> throw CancellationException() }
            fail("Cancellation must propagate")
        } catch (_: CancellationException) { }
    }

    @Test fun repeatedPageTokensFailWithoutReturningPartialData() = runBlocking {
        val results = HealthRecords.readWeek(setOf(weightPermission)) { _, _ -> HealthRecords.Page(listOf(weight()), "same") }
        assertEquals("failed", results.getValue("weight").status)
        assertTrue(results.getValue("weight").records.isEmpty())
    }

    @Test fun dayBoundaryUsesHalfOpenIntervalsAndSleepCanSpanMidnight() {
        val sleep = SleepSessionRecord(start.minusSeconds(3600), null, start.plusSeconds(7200), null, metadata)
        val results = mapOf(
            "weight" to HealthRecords.Result("success", listOf(weight(start.minusNanos(1)), weight(start), weight(end))),
            "sleep" to HealthRecords.Result("success", listOf(sleep)),
        )
        val json = HealthRecords.dayJson(results, start, end)
        assertEquals(1, json.getJSONObject("weight").getJSONArray("records").length())
        val sleepJson = json.getJSONObject("sleep").getJSONArray("records").getJSONObject(0)
        assertEquals(sleep.startTime.toString(), sleepJson.getString("startAt"))
        assertEquals(1, HealthRecords.dayJson(results, start.minusSeconds(86400), start)
            .getJSONObject("sleep").getJSONArray("records").length())
    }

    @Test fun valuesUseExplicitUnitsAndPreserveNullOffsetsAndSource() {
        val bp = BloodPressureRecord(start, null, metadata, Pressure.millimetersOfMercury(118.0), Pressure.millimetersOfMercury(76.0))
        val json = HealthRecords.recordJson(bp)
        assertEquals(118.0, json.getDouble("systolicMmHg"), 0.001)
        assertEquals(76.0, json.getDouble("diastolicMmHg"), 0.001)
        assertTrue(json.isNull("zoneOffset"))
        assertEquals(metadata.dataOrigin.packageName, json.getString("sourceApp"))
        assertEquals(62.5, HealthRecords.recordJson(weight()).getDouble("kilograms"), 0.001)
    }

    @Test fun heartSamplesAreBoundedAndMarked() {
        val samples = List(300) { HeartRateRecord.Sample(start.plusSeconds(it.toLong()), 72L) }
        val json = HealthRecords.recordJson(HeartRateRecord(start, null, start.plusSeconds(300), null, samples, metadata))
        assertEquals(240, json.getJSONArray("samples").length())
        assertEquals(300, json.getInt("sampleCount"))
        assertTrue(json.getBoolean("samplesTruncated"))
    }
}
