package com.baryonyx.health

import android.app.Activity
import android.os.Bundle
import android.widget.TextView
import android.widget.ScrollView

class HealthRationaleActivity : Activity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        val explanation = TextView(this).apply {
            text = "健康データの利用について\n\nHealth Connectから、今日を含む7日分の歩数だけを読み取ります。体重・血圧・心拍・睡眠など、歩数のほかの項目は読み取りません。\n\n読み取った日ごとの歩数は、ゲームのサーバーへ送って保存します。保存した歩数は、ゲームの中で使うルーンへの換算と、日々の歩数の表示に使います。\n\nHealth Connectへの書き込みは行いません。\n\n読み取りの許可は、Health Connectの設定からいつでも取り消せます。取り消しても、Health Connectの中のデータは削除されません。"
            textSize = 18f
            setPadding(32, 48, 32, 32)
        }
        setContentView(ScrollView(this).apply { addView(explanation) })
    }
}
