# WindowsでのUnityとAndroidエミュレーター

このプロジェクトでは、IL2CPP・ARM64のAPKをAndroid 16のPixel 8a仮想端末で確認する。
2026年9月13日に、以下の構成でユーザーがAPKのインストールとアプリ起動を確認した。

| 項目 | 起動確認済みの構成 |
|---|---|
| ホスト | Windows・x64、NVIDIA GeForce RTX 4060 |
| Unity | `6000.6.0f1` |
| APK | `client/Builds/Android/baryonyx.apk`、IL2CPP・ARM64のDevelopment Build |
| Android Emulator本体 | `37.1.11` |
| Androidシステムイメージ | Android 16（API 36）・Google APIs・x86_64 |
| SDKパッケージ名 | `system-images;android-36;google_apis;x86_64` |
| 仮想端末名 | `Pixel_8a_API_36`、端末プロファイルは `pixel_8a` |
| 起動引数 | `-gpu host -feature -Vulkan -no-snapshot` |
| 描画 | PCのGPUを使うOpenGL ES 3.1 |

これは上記環境での起動確認であり、他のGPUやエミュレーターの版まで動作を保証するものではない。
Unity公式ではAndroidエミュレーター上の動作はサポート対象外であるため、このプロジェクトの確認環境として扱う。[Unityの動作要件](https://docs.unity3d.com/6000.6/Documentation/Manual/system-requirements.html)
Google認証、Health Connectの権限・歩数取得などの確認結果は、アプリ起動と分けて記録する。

## Unityとエミュレーターの設定

UnityのAndroid Player Settingsは、Scripting Backendを `IL2CPP`、Target Architecturesを `ARM64` にする。
Graphics APIsには `OpenGLES3` を含める。
現行設定にはVulkanとOpenGLES3が入っており、エミュレーター側でVulkanを無効にするとOpenGL ESを使う。
Unity 6.6はOpenGL ES 3.1以上を必要とするため、エミュレーター側の対応バージョンも確認する。[Unityの動作要件](https://docs.unity3d.com/6000.6/Documentation/Manual/system-requirements.html)

APKのビルド条件は [Androidビルドと実機確認](client-android-testing.md) にまとめている。
ショートカットのビルド処理はIL2CPP・ARM64を指定するため、エミュレーターのためにARMv7やx86_64へ変更する必要はない。

仮想端末のCPUはx86_64で、ARM64 APKはAndroid内の命令変換を通して実行される。
確認したAndroid 16の対応ABIは `x86_64,arm64-v8a` である。
Pixel 8aという端末プロファイル名だけでは、Androidの版やCPUの種類は決まらない。
Google認証を使うため、Google Play servicesを含むGoogle APIsのシステムイメージを選ぶ。[Androidの仮想端末設定](https://developer.android.com/studio/run/managing-avds)

| 起動引数 | この確認環境での目的 |
|---|---|
| `-gpu host` | PCのGPUを使い、OpenGL ES 3.1の描画環境を使う。 |
| `-feature -Vulkan` | 仮想端末のVulkanを無効にし、UnityをOpenGL ESで動かす。 |
| `-no-snapshot` | 保存済みの起動状態を読み込まず、終了時にもスナップショットを保存しない。インストール済みアプリや端末データは保持する。 |

描画の選択肢は [Android Emulatorの描画設定](https://developer.android.com/studio/run/emulator-acceleration)、Vulkan無効化は [トラブルシューティング](https://developer.android.com/studio/run/emulator-troubleshooting) を参照する。
スナップショットの指定は [エミュレーターの起動オプション](https://developer.android.com/studio/run/emulator-commandline) を参照する。

## 初回準備

Unity Hubには対象Unity版のAndroid Build Support・SDK・NDK・OpenJDKを導入する。
エミュレーター用にはAndroid StudioのSDK ManagerでAndroid SDK Command-line Tools、Android Emulator、Android SDK Platform-Toolsを導入する。
Unityのビルド用SDKと、Android Studioのエミュレーター用SDKは別の配置でよい。

以下はユーザーがPowerShellで実行する手順である。
Android Studioの標準SDK配置を使う例なので、別の場所に導入した場合は `$sdk` と `$env:JAVA_HOME` をその配置に合わせる。
各コマンドでエラーが出た場合は、原因を解消してから次へ進む。

### エミュレーター本体の更新

起動中のAndroidエミュレーターをすべて閉じてから実行する。

```powershell
$sdk = "$env:LOCALAPPDATA\Android\Sdk"
$env:JAVA_HOME = "$env:ProgramFiles\Android\Android Studio\jbr"

& "$sdk\cmdline-tools\latest\bin\sdkmanager.bat" "--sdk_root=$sdk" --channel=0 "emulator"
& "$sdk\emulator\emulator.exe" -version
```

`--channel=0` は安定版を選ぶ指定であり、37.1.11への固定ではない。
更新後は表示された版とアプリの起動結果を記録する。[SDK Managerのコマンド](https://developer.android.com/tools/sdkmanager)

本体の更新だけでは、作成済みAVDのAndroid 14がAndroid 16に切り替わることはない。
次の手順でAndroid 16のイメージとAVDを追加する。

### Android 16の仮想端末の追加

同じPowerShellで実行する。
`Pixel_8a_API_36` が作成済みの場合、AVDの作成コマンドは省略する。

```powershell
& "$sdk\cmdline-tools\latest\bin\sdkmanager.bat" "--sdk_root=$sdk" --channel=0 "platform-tools" "system-images;android-36;google_apis;x86_64"
"no" | & "$sdk\cmdline-tools\latest\bin\avdmanager.bat" create avd -n "Pixel_8a_API_36" -k "system-images;android-36;google_apis;x86_64" -d "pixel_8a"
& "$sdk\emulator\emulator.exe" -list-avds
```

一覧に `Pixel_8a_API_36` が含まれることを確認する。
既存の `Pixel_8a_API_34` は削除せず、以後の確認先をAPI 36に切り替える。
SDKの追加ではライセンス確認が表示される場合があるため、内容を確認して操作する。

## 普段の起動とAPKインストール

[手動実行用ショートカット](../../AGENTS.md#手動実行用ショートカット) を使う。
各ファイルはダブルクリックでき、実行時の作業ディレクトリに依存しない。

1. APKを更新する場合は、このプロジェクトをUnityで閉じてから [build-apk.bat](../../shortcuts/build-apk.bat) を実行する。出力先は `client/Builds/Android/baryonyx.apk`。
2. ユーザーが [start-pixel-8a.bat](../../shortcuts/start-pixel-8a.bat) を実行し、Androidのホーム画面が表示されるまで待つ。起動用のウィンドウは開いたままにする。
3. 別途 [install-apk-pixel-8a.bat](../../shortcuts/install-apk-pixel-8a.bat) を実行する。別のAPKを使う場合は、ファイルを1つドラッグ＆ドロップする。
4. `Success` とインストール完了の表示を確認し、仮想端末内のアプリアイコンを押す。

ショートカットは `ANDROID_HOME`、`ANDROID_SDK_ROOT`、`%LOCALAPPDATA%\Android\Sdk` の順に必要なツールを探す。
インストール先はAVD名 `Pixel_8a_API_36` で選び、実機や別名のAVDにはインストールしない。
対象が未起動、起動途中、同名で複数起動の場合はエラーで終了する。
既存アプリは `adb install -r` でデータを保持して更新する。

### PowerShellで直接実行する場合

起動は次のコマンドに相当する。

```powershell
& "$env:LOCALAPPDATA\Android\Sdk\emulator\emulator.exe" -avd "Pixel_8a_API_36" -gpu host -feature -Vulkan -no-snapshot
```

APKのインストールは別のPowerShellをリポジトリのルートで開いて実行する。
直接実行する例の `-e` は接続中のエミュレーターを選ぶため、`Pixel_8a_API_36` だけを起動しておく。
複数のAVDを使う場合は、AVD名を確認するインストール用ショートカットを使う。

```powershell
$adb = "$env:LOCALAPPDATA\Android\Sdk\platform-tools\adb.exe"
& $adb -e shell getprop ro.boot.qemu.avd_name
```

表示されたAVD名が `Pixel_8a_API_36` であることを確認してから、インストールを実行する。

```powershell
& $adb -e install -r (Resolve-Path ".\client\Builds\Android\baryonyx.apk").Path
```

adbの接続先指定と `install -r` は [Android公式の手順](https://developer.android.com/tools/adb) を参照する。
エミュレーターの起動やログ取得・操作は、[ルートの作業ルール](../../AGENTS.md#デバッグ環境) に従ってユーザーが行う。

## 起動失敗時の確認

今回の失敗と起動成功は、CPU命令変換と描画の問題を分けて扱う。

| 症状・ログ | 今回の確認結果と対処 |
|---|---|
| `Undefined instruction 0xd50320bf`、`SIGILL` | Android 14のARM64命令変換で発生した。本体を33.1.24から37.1.11へ更新しても残ったが、Android 16のAVDへ切り替えた構成でアプリが起動した。UnityのABIを変更する前に、AVDがAPI 36か確認する。 |
| `Unable to initialize the Unity Engine Graphics API` | `-gpu software` の構成ではVulkanの初期化が失敗し、OpenGL ESも3.0までだった。`-gpu host -feature -Vulkan` でOpenGL ES 3.1を利用する。 |
| `Failed to find ColorBuffer`、ASTC関連の出力 | これらの行だけでアプリの終了原因を断定しない。Android側のクラッシュログと描画ログを確認する。 |
| インストール先が見つからない | AVD名と起動完了を確認する。Pixel 8aでも `Pixel_8a_API_34` はインストール用ショートカットの対象外。 |
| `INSTALL_FAILED_UPDATE_INCOMPATIBLE` | 既存APKとの署名不一致を確認する。入れ直し時のデータ消去は [Androidビルドと実機確認](client-android-testing.md#手動で残す確認) に従う。 |

ユーザーがログを取得する場合は、対象AVDだけを起動し、次のコマンドでAndroidの版、対応ABI、クラッシュを確認する。

```powershell
$adb = "$env:LOCALAPPDATA\Android\Sdk\platform-tools\adb.exe"
& $adb -e shell getprop ro.boot.qemu.avd_name
& $adb -e shell getprop ro.build.version.release
& $adb -e shell getprop ro.product.cpu.abilist
& $adb -e logcat -b crash -d
```

本体、システムイメージ、GPU、Unityのいずれかを変更した場合は、変更後の構成と起動結果をこの文書に追記する。
認証や端末機能の確認項目は [Androidビルドと実機確認](client-android-testing.md#手動で残す確認) を参照する。
