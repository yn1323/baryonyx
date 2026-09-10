package com.baryonyx.health

import android.app.Activity
import android.os.Bundle
import android.widget.TextView

class HealthRationaleActivity : Activity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContentView(TextView(this).apply {
            text = "歩数データの利用について\n\n歩数を日別に集計し、ログインしたユーザーのデータとしてサーバーに保存します。\n\nアプリを開いている間だけ取得・送信します。Health Connectへの書き込みは行いません。\n\n許可はHealth Connectの設定から変更できます。許可の取り消しやログアウトによって、保存済みの履歴が削除されることはありません。"
            textSize = 18f
            setPadding(32, 48, 32, 32)
        })
    }
}
