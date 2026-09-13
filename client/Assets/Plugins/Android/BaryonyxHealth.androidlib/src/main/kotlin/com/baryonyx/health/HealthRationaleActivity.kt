package com.baryonyx.health

import android.app.Activity
import android.os.Bundle
import android.widget.TextView
import android.widget.ScrollView

class HealthRationaleActivity : Activity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        val explanation = TextView(this).apply {
            text = "健康データの利用について\n\nHealth Connectから今日を含む7日分の歩数と、体重・体脂肪・身長・血圧・心拍・安静時心拍・酸素飽和度・呼吸数・体温・血糖値・睡眠・距離・消費カロリー・運動記録を読み取ります。歩数は一覧に、ほかの項目は測定時刻や記録元アプリとともに日別のJSON詳細に表示します。項目は個別に許可でき、歩数を許可しなくてもほかの項目を確認できます。\n\n健康データはサーバーへ送信せず、アプリ側で保存もしません。画面を開いている間だけ保持します。Health Connectへの書き込みは行いません。\n\nGoogle認証と健康データの読み取り許可は別のものです。Google認証のための通信は発生します。\n\n読み取り許可はHealth Connectの設定から変更できます。Google接続を解除してもHealth Connect内のデータは削除されません。"
            textSize = 18f
            setPadding(32, 48, 32, 32)
        }
        setContentView(ScrollView(this).apply { addView(explanation) })
    }
}
