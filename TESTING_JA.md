# テスト

このリポジトリには、C# アプリケーションコードと保守対象の Python スクリプトに対する自動テストが含まれています。

## 対象

- `tests/HS2VoiceReplace.Tests`
  - localization catalog
  - localized attributes
  - `SeedVcUiSettings` の clone / summary
  - signatures / freshness / report parsing / target resolution / grid helpers
- `tests/python`
  - `python_cli_common.py`
  - `seed_vc_batch_common.py`
  - `select_voice_style_segment.py` の pure helper

現状のテスト層は、依存が軽く安定したロジックを優先しています。GUI 描画、長時間外部プロセス、実機 HS2 資産を使うフローはこのレイヤでは扱いません。

## すべて実行

```powershell
.\tools\run_tests.ps1
.\tools\run_tests.cmd
```

マシン全体の Python に依存せず、repo 内専用の Python を使ってテストしたい場合:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\setup_local_python.ps1
```

## 片側だけ実行

```powershell
.\tools\run_tests.ps1 -SkipPython
.\tools\run_tests.ps1 -SkipDotNet
```

## 最小ビルド確認

### HS2VoiceReplaceGui

`AssetsTools.NET.dll` を解決できて、GUI プロジェクトがビルドできることを確認します。

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\setup_assetstools.ps1
dotnet build .\tools\HS2VoiceReplaceGui\HS2VoiceReplaceGui.csproj -c Release
```

既定パスに `AssetsTools.NET.dll` が無い場合は、明示的に指定してください。

```powershell
dotnet build .\tools\HS2VoiceReplaceGui\HS2VoiceReplaceGui.csproj -c Release -p:AssetsToolsNetPath=C:\path\to\AssetsTools.NET.dll
```

### UabAudioClipPatcher

`AssetsTools.NET.dll` を解決できて、patcher プロジェクトがビルドできることを確認します。

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\setup_assetstools.ps1
dotnet build .\tools\UabAudioClipPatcher\UabAudioClipPatcher.csproj -c Release
```

既定パスに `AssetsTools.NET.dll` が無い場合は、明示的に指定してください。

```powershell
dotnet build .\tools\UabAudioClipPatcher\UabAudioClipPatcher.csproj -c Release -p:AssetsToolsNetPath=C:\path\to\AssetsTools.NET.dll
```

### HS2VoiceReplace.Runtime

game-side DLL を解決できて、runtime plugin プロジェクトから `HS2_VoiceReplace.dll` を出力できることを確認します。

```powershell
dotnet build .\runtime\HS2VoiceReplace.Runtime\HS2VoiceReplace.Runtime.csproj -c Release -p:GameRoot=C:\path\to\HoneySelect2
```

この確認には、期待される BepInEx / Unity DLL を含む有効な `GameRoot` が必要です。

## 補足

- Python テストは `._tools\python310\python.exe` があればそれを優先します。
- `tools/setup_local_python.ps1` は、`tools/python_runtime_manifest.json` で定義した公式の embeddable Python と現在の Python テスト依存を、同 manifest で定義された repo-local パスにセットアップします。
- repo-local Python を最初から作り直したい場合は `powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\setup_local_python.ps1 -Force` を使ってください。
- PowerShell の実行制限がある場合は `.\tools\run_tests.cmd` を使ってください。
- C# テストは `xUnit` と `dotnet test` を使用します。
- ユーザー向け文言や localization metadata を変更した場合は、関連する C# テストも更新してください。
