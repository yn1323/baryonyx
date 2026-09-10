package com.baryonyx.health

import android.app.Activity
import android.content.Intent
import android.net.Uri
import android.os.Build
import androidx.health.connect.client.HealthConnectClient
import androidx.health.connect.client.permission.HealthPermission
import androidx.health.connect.client.records.StepsRecord
import androidx.health.connect.client.request.AggregateRequest
import androidx.health.connect.client.time.TimeRangeFilter
import kotlinx.coroutines.*
import org.json.JSONArray
import org.json.JSONObject
import java.time.Instant
import java.time.ZoneId
import java.time.temporal.ChronoUnit
import java.util.concurrent.ConcurrentHashMap

interface HealthCallback { fun complete(json: String) }

object HealthBridge {
    private val scope = CoroutineScope(SupervisorJob() + Dispatchers.Main)
    private val jobs = ConcurrentHashMap<String, Job>()
    private val callbacks = ConcurrentHashMap<String, HealthCallback>()
    val permissions = setOf(HealthPermission.getReadPermission(StepsRecord::class))

    fun finish(id: String, status: String, days: JSONArray? = null) {
        val reply = JSONObject().put("status", status)
        if (days != null) reply.put("days", days)
        callbacks.remove(id)?.complete(reply.toString())
    }
    @JvmStatic fun execute(activity: Activity, operation: String, id: String, callback: HealthCallback) {
        callbacks[id] = callback
        val job = scope.launch(start = CoroutineStart.LAZY) {
            try {
                val status = if (Build.VERSION.SDK_INT < 28) HealthConnectClient.SDK_UNAVAILABLE else HealthConnectClient.getSdkStatus(activity)
                if (status != HealthConnectClient.SDK_AVAILABLE) {
                    finish(id, if (status == HealthConnectClient.SDK_UNAVAILABLE_PROVIDER_UPDATE_REQUIRED) "update_required" else "unavailable")
                    return@launch
                }
                if (operation == "availability") { finish(id, "available"); return@launch }
                val client = HealthConnectClient.getOrCreate(activity.applicationContext)
                val granted = client.permissionController.getGrantedPermissions().containsAll(permissions)
                when (operation) {
                    "permission" -> finish(id, if (granted) "granted" else "not_granted")
                    "requestPermission" -> {
                        if (granted) finish(id, "granted")
                        else activity.startActivity(Intent(activity, HealthPermissionActivity::class.java).putExtra("requestId", id))
                    }
                    "read" -> {
                        if (!granted) { finish(id, "permission_required"); return@launch }
                        val now = Instant.now().truncatedTo(ChronoUnit.MILLIS)
                        val zone = ZoneId.systemDefault()
                        val today = now.atZone(zone).toLocalDate()
                        val days = JSONArray()
                        for (offset in 6 downTo 0) {
                            ensureActive()
                            val date = today.minusDays(offset.toLong())
                            val start = date.atStartOfDay(zone).toInstant()
                            val end = if (offset == 0) now else date.plusDays(1).atStartOfDay(zone).toInstant()
                            val value = client.aggregate(AggregateRequest(setOf(StepsRecord.COUNT_TOTAL), TimeRangeFilter.between(start, end)))[StepsRecord.COUNT_TOTAL]
                            days.put(JSONObject().put("day", date.toString()).put("zone", zone.id)
                                .put("startAt", start.toString()).put("endAt", end.toString())
                                .put("hasValue", value != null).put("steps", value ?: 0L).put("observedAt", now.toString()))
                        }
                        ensureActive()
                        finish(id, "success", days)
                    }
                    else -> finish(id, "failed")
                }
            } catch (_: CancellationException) { callbacks.remove(id) }
            catch (_: SecurityException) { finish(id, "permission_required") }
            catch (_: Exception) { finish(id, "failed") }
            finally { jobs.remove(id) }
        }
        jobs[id] = job
        job.start()
    }
    @JvmStatic fun cancel(id: String) { callbacks.remove(id); jobs.remove(id)?.cancel() }
    @JvmStatic fun openSettings(activity: Activity) {
        activity.runOnUiThread {
            try {
                val intent = if (Build.VERSION.SDK_INT >= 34) Intent("android.health.connect.action.HEALTH_HOME_SETTINGS")
                    else Intent("androidx.health.ACTION_HEALTH_CONNECT_SETTINGS")
                activity.startActivity(intent)
            } catch (_: Exception) {
                activity.startActivity(Intent(Intent.ACTION_VIEW, Uri.parse("https://play.google.com/store/apps/details?id=com.google.android.apps.healthdata")))
            }
        }
    }
}
