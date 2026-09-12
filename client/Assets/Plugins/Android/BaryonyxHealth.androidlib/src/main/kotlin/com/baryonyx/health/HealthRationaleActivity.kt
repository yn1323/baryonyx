package com.baryonyx.health

import android.app.Activity
import android.os.Bundle
import android.widget.TextView

class HealthRationaleActivity : Activity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContentView(TextView(this).apply {
            text = "歩数データの利用について\n\nHealth Connectから今日を含む7日分の歩数を読み取り、日別の一覧とJSON詳細を表示します。\n\n歩数データはサーバーへ送信せず、アプリ側で保存もしません。画面を開いている間だけ保持します。Health Connectへの書き込みは行いません。\n\nGoogle認証と歩数の読み取り許可は別のものです。Google認証のための通信は発生します。\n\n読み取り許可はHealth Connectの設定から変更できます。サインアウトしてもHealth Connect内のデータは削除されません。"
            textSize = 18f
            setPadding(32, 48, 32, 32)
        })
    }
}
