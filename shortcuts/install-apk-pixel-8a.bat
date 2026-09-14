@echo off
setlocal EnableExtensions DisableDelayedExpansion
chcp 65001 >nul
set "AVD_NAME=Pixel_8a_API_36"
set "RESULT=1"

if not "%~2"=="" (
    echo [エラー] APKは1つだけ指定してください。
    goto finish
)
set "APK_PATH=%~dp0..\client\Builds\Android\baryonyx.apk"
if not "%~1"=="" set "APK_PATH=%~f1"
if not exist "%APK_PATH%" (
    echo [エラー] APKが見つかりません: "%APK_PATH%"
    echo UnityでAPKをビルドするか、このbatファイルへAPKをドラッグ＆ドロップしてください。
    goto finish
)
for %%F in ("%APK_PATH%") do if /i not "%%~xF"==".apk" (
    echo [エラー] 拡張子が.apkのファイルを指定してください。
    goto finish
)
if exist "%APK_PATH%\" (
    echo [エラー] フォルダーではなくAPKファイルを指定してください。
    goto finish
)

set "SDK_ROOT=%ANDROID_HOME%"
if not exist "%SDK_ROOT%\platform-tools\adb.exe" set "SDK_ROOT=%ANDROID_SDK_ROOT%"
if not exist "%SDK_ROOT%\platform-tools\adb.exe" set "SDK_ROOT=%LOCALAPPDATA%\Android\Sdk"
set "ADB=%SDK_ROOT%\platform-tools\adb.exe"
if not exist "%ADB%" (
    echo [エラー] adbが見つかりません。
    echo Android StudioでPlatform-Toolsをインストールし、ANDROID_HOMEにSDKの場所を設定してください。
    goto finish
)

rem ポート番号を固定せず、AVD名が一致する起動済みエミュレーターだけを選ぶ。
set "DEVICE_LIST=%TEMP%\baryonyx-adb-%RANDOM%-%RANDOM%.txt"
"%ADB%" devices >"%DEVICE_LIST%"
set "RESULT=%ERRORLEVEL%"
if not "%RESULT%"=="0" (
    del /q "%DEVICE_LIST%" >nul 2>&1
    echo [エラー] 接続先の一覧を取得できませんでした。
    goto finish
)
set "RESULT=1"
set "TARGET_SERIAL="
set "TARGET_COUNT=0"
for /f "usebackq tokens=1,2" %%D in ("%DEVICE_LIST%") do if "%%E"=="device" call :check_emulator "%%D"
del /q "%DEVICE_LIST%" >nul 2>&1
if "%TARGET_COUNT%"=="0" (
    echo [エラー] 起動済みの "%AVD_NAME%" が見つかりません。
    echo start-pixel-8a.batを手動で実行し、起動完了後にやり直してください。
    goto finish
)
if not "%TARGET_COUNT%"=="1" (
    echo [エラー] "%AVD_NAME%" が複数起動しています。1台だけ起動した状態でやり直してください。
    goto finish
)

"%ADB%" -s "%TARGET_SERIAL%" shell getprop sys.boot_completed | "%SystemRoot%\System32\findstr.exe" /x /l /c:"1" >nul
if errorlevel 1 (
    echo [エラー] エミュレーターの起動が完了していません。起動完了後にやり直してください。
    goto finish
)

echo "%APK_PATH%" を "%AVD_NAME%" [%TARGET_SERIAL%] にインストールします。
"%ADB%" -s "%TARGET_SERIAL%" install -r "%APK_PATH%"
set "RESULT=%ERRORLEVEL%"
if not "%RESULT%"=="0" (
    echo [エラー] インストールに失敗しました。上のadbの表示を確認してください。
) else (
    echo インストールが完了しました。
)
goto finish

:check_emulator
set "SERIAL=%~1"
if not "%SERIAL:~0,9%"=="emulator-" exit /b 0
"%ADB%" -s "%SERIAL%" shell getprop ro.boot.qemu.avd_name 2>nul | "%SystemRoot%\System32\findstr.exe" /x /l /c:"%AVD_NAME%" >nul
if errorlevel 1 exit /b 0
set /a TARGET_COUNT+=1 >nul
set "TARGET_SERIAL=%SERIAL%"
exit /b 0

:finish
echo.
echo キーを押すとこのウィンドウを閉じます。
pause >nul
exit /b %RESULT%
