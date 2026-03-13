# UabAudioClipPatcher

`UabAudioClipPatcher` は、HS2VoiceReplace の bundle 再構築時に使う小さなコマンドラインツールです。

このツールは Unity asset bundle を開き、`AudioClip` の payload フィールドを見つけて、事前に変換しておいた音声ファイルで差し替えます。

## 目的

- 大きな editor ワークフローに依存せずに voice bundle を patch する
- `AudioClip` の payload byte だけを差し替える
- 可能な限り元の bundle 構造と object layout を維持する

## ビルド前提

次の条件がすべて必要です。

- .NET 8 SDK
- `AssetsTools.NET.dll`
- `dotnet build` の出力先に書き込めること

このリポジトリには `AssetsTools.NET.dll` を同梱していません。upstream 配布物や手元のツール環境から別途用意してください。
任意の補助スクリプトとして `.\tools\setup_assetstools.ps1` も用意しています。

既定では、プロジェクトは次の場所に `AssetsTools.NET.dll` があることを想定します。

- `..\..\_tools\uabea\v8\AssetsTools.NET.dll`

そのファイルが存在しない場合は、ビルド時に明示的なパスを指定してください。

```powershell
dotnet build .\tools\UabAudioClipPatcher\UabAudioClipPatcher.csproj -c Release -p:AssetsToolsNetPath=C:\path\to\AssetsTools.NET.dll
```

正常にビルドするには、その DLL が実在し、`Program.cs` で使っている `AssetsTools.NET` namespace と互換である必要があります。

## ビルド

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\setup_assetstools.ps1
dotnet build .\tools\UabAudioClipPatcher\UabAudioClipPatcher.csproj -c Release
```

## 使い方

```text
UabAudioClipPatcher <bundlePath> <classdata.tpk> <payloadDir> <outputBundlePath> <payloadExt=.wav>
```

## 引数

- `bundlePath`
  - patch 対象の source Unity asset bundle
- `classdata.tpk`
  - AssetsTools.NET が使う UABEA の class database
- `payloadDir`
  - 差し替え音声 payload を置いたディレクトリ
- `outputBundlePath`
  - patch 後 bundle の出力先
- `payloadExt`
  - 差し替え payload の拡張子。既定値は `.wav`

## 補足

- 基本的には GUI ワークフローから呼ばれる想定ですが、デバッグ目的で単独利用もできます。
- payload ファイル名は bundle 側が期待する clip 名と一致している必要があります。
- `classdata.tpk` 自体はこのリポジトリに同梱していません。依存セットアップ経由、または upstream の UABEA 配布物から別途取得してください。
