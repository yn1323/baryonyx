@echo off
setlocal EnableExtensions DisableDelayedExpansion
chcp 65001 >nul
rem ユーザーの手動起動専用。AIはこのファイルを実行しない。
set "AVD_NAME=Pixel_8a_API_36"
set "RESULT=1"

set "SDK_ROOT=%ANDROID_HOME%"
if not exist "%SDK_ROOT%\emulator\emulator.exe" set "SDK_ROOT=%ANDROID_SDK_ROOT%"
if not exist "%SDK_ROOT%\emulator\emulator.exe" set "SDK_ROOT=%LOCALAPPDATA%\Android\Sdk"
set "EMULATOR=%SDK_ROOT%\emulator\emulator.exe"
if not exist "%EMULATOR%" (
    echo [エラー] Android Emulatorが見つかりません。
    echo Android Studioでインストールし、ANDROID_HOMEにSDKの場所を設定してください。
    goto finish
)

"%EMULATOR%" -list-avds | "%SystemRoot%\System32\findstr.exe" /x /l /c:"%AVD_NAME%" >nul
if errorlevel 1 (
    echo [エラー] AVD "%AVD_NAME%" が見つかりません。
    echo doc/rules/client-android-emulator.mdの初回準備に従ってAndroid 16のAVDを作成してください。
    goto finish
)

echo "%AVD_NAME%" を起動します。このウィンドウはエミュレーター終了まで開いたままにしてください。
rem 起動確認済みの描画設定。PCのGPUとOpenGL ESを使い、保存済み状態を読み込まない。
"%EMULATOR%" -avd "%AVD_NAME%" -gpu host -feature -Vulkan -no-snapshot
set "RESULT=%ERRORLEVEL%"
if not "%RESULT%"=="0" echo [エラー] エミュレーターが異常終了しました。上の表示を確認してください。

:finish
echo.
echo キーを押すとこのウィンドウを閉じます。
pause >nul
exit /b %RESULT%
