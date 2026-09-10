package com.baryonyx.health

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.health.connect.client.PermissionController

class HealthPermissionActivity : ComponentActivity() {
    private val launcher = registerForActivityResult(PermissionController.createRequestPermissionResultContract()) { granted ->
        HealthBridge.finish(intent.getStringExtra("requestId") ?: "", if (granted.containsAll(HealthBridge.permissions)) "granted" else "not_granted")
        finish()
    }
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        if (savedInstanceState == null) {
            try { launcher.launch(HealthBridge.permissions) }
            catch (_: Exception) { HealthBridge.finish(intent.getStringExtra("requestId") ?: "", "failed"); finish() }
        }
    }
    override fun onDestroy() {
        if (!isChangingConfigurations) HealthBridge.finish(intent.getStringExtra("requestId") ?: "", "not_granted")
        super.onDestroy()
    }
}
