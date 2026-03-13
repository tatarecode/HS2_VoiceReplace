# HS2VoiceReplace

HS2VoiceReplace は、Honey Select 2 の音声差し替えを行うための C# GUI ツールです。
実ゲーム環境を直接書き換えずに、音声差し替え用の生成・配備を行うことを目的としています。

この export フォルダには、別リポジトリとして継続開発できる最小限のソース一式が含まれています。

## リポジトリ構成

- `tools/HS2VoiceReplace`
  - メインの WinForms GUI アプリケーション
- `runtime/HS2VoiceReplace.Runtime`
  - 配備物で使用する runtime plugin プロジェクト
- `tools/UabAudioClipPatcher`
  - bundle 再構築時に使用する AudioClip patcher
- `tools/*.py`
  - style 選択や Seed-VC 一括変換で使用する Python スクリプト
- `mods_src/HS2VoiceReplaceRuntime`
  - zipmod 生成に使う最小テンプレート
- `tests/HS2VoiceReplace.Tests`
  - C# 自動テスト
- `tests/python`
  - Python 自動テスト

## この export に含まれるもの

- アプリケーションのソースコード
- runtime plugin のソースコード
- 必要な補助スクリプト
- テストコード
- 最小限の packaging template

## この export に含まれないもの

- ローカルのビルド成果物
- ローカルの publish 出力
- マシン固有の仮想環境
- 元ワークスペース内の無関係な旧ツール群

## ビルド前提

### GUI アプリケーション

- .NET 8 SDK
- 別途用意した `AssetsTools.NET.dll`
  - このリポジトリには `AssetsTools.NET.dll` を同梱していません
  - source build の既定配置先:
    - `.\_tools\uabea\v8\AssetsTools.NET.dll`
  - 任意の補助スクリプト:
    - `powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\setup_assetstools.ps1`
  - コマンドライン ビルドでは次も利用できます:
    - `-p:AssetsToolsNetPath=C:\path\to\AssetsTools.NET.dll`

### Runtime plugin

- .NET Framework 4.7.2 Targeting Pack
- 以下の game-side 参照
  - `BepInEx.dll`
  - `0Harmony.dll`
  - `UnityEngine.dll`
  - `UnityEngine.CoreModule.dll`
  - `UnityEngine.UI.dll`

runtime plugin プロジェクトは、これらの game-side DLL を同梱しません。

## ビルド

```powershell
dotnet build .\tools\HS2VoiceReplace\HS2VoiceReplace.csproj -c Release
dotnet build .\tools\UabAudioClipPatcher\UabAudioClipPatcher.csproj -c Release
```

既定パスに `AssetsTools.NET.dll` が無い場合は、明示的に指定してください。

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\setup_assetstools.ps1
dotnet build .\tools\HS2VoiceReplace\HS2VoiceReplace.csproj -c Release -p:AssetsToolsNetPath=C:\path\to\AssetsTools.NET.dll
dotnet build .\tools\UabAudioClipPatcher\UabAudioClipPatcher.csproj -c Release -p:AssetsToolsNetPath=C:\path\to\AssetsTools.NET.dll
```

## テスト

C# と Python のテストをまとめて実行する場合:

```powershell
.\tools\run_tests.cmd
```

または:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\run_tests.ps1
```

## 開発の入口

- GUI アプリケーション:
  - `tools/HS2VoiceReplace/MainForm.cs`
- パイプライン制御:
  - `tools/HS2VoiceReplace/VoiceReplacePipeline.cs`
- アプリケーションサービス層:
  - `tools/HS2VoiceReplace/ApplicationServices.cs`
- UI 文言管理:
  - `tools/HS2VoiceReplace/UiTextCatalog.cs`

## 補足

- 開発導線の補足は `DEVELOPMENT.md` と `DEVELOPMENT_JA.md` にあります。
- テスト実行の補足は `TESTING.md` と `TESTING_JA.md` にあります。
- ツール固有の詳細は `tools/HS2VoiceReplace/README.md` と `tools/HS2VoiceReplace/README_JA.md` を参照してください。
- `UabAudioClipPatcher` の詳細は `tools/UabAudioClipPatcher/README.md` と `tools/UabAudioClipPatcher/README_JA.md` を参照してください。
- runtime plugin の詳細は `runtime/HS2VoiceReplace.Runtime/README.md` と `runtime/HS2VoiceReplace.Runtime/README_JA.md` を参照してください。

