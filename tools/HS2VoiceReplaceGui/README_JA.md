# HS2VoiceReplaceGui

このディレクトリには、HS2VoiceReplace のメイン WinForms GUI アプリケーションが含まれています。

GUI は、音声差し替えの一連の処理を管理します。

1. 選択した Honey Select 2 環境から音声バンドルを抽出
2. `.fsb` 音声を `.wav` にデコード
3. スタイル用サンプル音声を準備
4. Seed-VC による音声変換を実行
5. `unity3d` 音声バンドルを再構築
6. split zipmod を生成
7. 必要に応じて、生成物を選択した HS2 環境へ配備

## バンドル番号の扱い

- Base game の性格では `30.unity3d` が使われる場合があります
- DX 追加分の性格では `50.unity3d` が使われる場合があります
- このツールは `50.unity3d` の存在を前提にしません
- 対象フォルダ内で、性格に適したファイル名を優先し、無い場合は利用可能な最大の数値バンドルへフォールバックします

## 依存解決の優先順位

1. GUI で選択した外部ツールルート
2. 同梱 runtime assets のフォールバック

## 既定のデータ保存先

- GUI は、リポジトリ ルートを検出できる場合、既定でダウンロードしてきたこのフォルダ内の `.\.hs2voicereplace\` 配下に生成データを保存します。
- リポジトリ ルートを検出できない場合は、実行ファイルの横に `.hs2voicereplace` フォルダを作成して使います。
- 出力ルートは基本設定ダイアログから変更できます。
- 依存セットアップや変換では、repo-local Python として `.\_tools\python310\python.exe` を再利用できます。

## 初回セットアップ

1. GUI 上で外部ツールルートを確認
2. リポジトリ内にビルド済み `UabAudioClipPatcher.exe` が無い場合は、先に `AssetsTools.NET.dll` を用意
   - source build の既定配置先: `.\_tools\uabea\v8\AssetsTools.NET.dll`
   - 任意の補助スクリプト: `powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\setup_assetstools.ps1`
   - または GUI 起動前に `HS2VR_ASSETSTOOLS_NET_PATH=C:\path\to\AssetsTools.NET.dll` を設定
3. `依存セットアップ` を実行
4. 完了後、必要に応じて抽出、試聴、変換、配備を進めます

## セットアップで取得する依存

- Python embeddable package
- `get-pip.py` / pip
- Seed-VC リポジトリアーカイブ
- `classdata.tpk` 用の UABEA リリースアーカイブ
- `vgmstream-cli.exe` 用の vgmstream リリースアーカイブ
- Seed-VC に必要な Python パッケージ
- `noisereduce`

`.\_tools\python310\python.exe` が既にある場合、依存セットアップは外部ツールルートへ別の embedded Python を展開せず、この repo-local Python を再利用します。

`依存セットアップ` は `AssetsTools.NET.dll` を取得も同梱もしません。必要な source build では別途用意してください。

## プロジェクト側が供給する補助ファイル

リポジトリ側には、次の source-side asset が含まれています。

- `tools/seed_vc_v1_inprocess_batch.py`
- `tools/seed_vc_v2_inprocess_batch.py`
- `tools/select_voice_style_segment.py`
- `tools/UabAudioClipPatcher/*`
- `mods_src/HS2VoiceReplaceRuntime/*`
- `runtime/HS2VoiceReplace.Runtime/*`

依存セットアップ時やローカル開発時には、これらを必要に応じて選択した外部ツールルートへコピーまたはビルドします。

選択した外部ツールルートに必要ファイルが無い場合、アプリケーションは次の順で補完します。

1. リポジトリ内のローカルソース
2. 同梱 runtime assets
3. 必要であれば `UabAudioClipPatcher` または runtime plugin を `dotnet build`

`UabAudioClipPatcher` の source build にフォールバックした場合は、上記既定パスまたは `HS2VR_ASSETSTOOLS_NET_PATH` の `AssetsTools.NET.dll` を使います。

## 実行済み工程のスキップ

`実行済み工程をスキップ` を有効にすると:

- 依存セットアップの完了マーカーは `external_tools\\_state\\*.done`
- ワークフローの完了マーカーは `...gui_runs\\resume_cXX\\_done\\*.done`

に保存されます。

## URL 上書き

- `HS2VR_UABEA_ZIP_URL`
- `HS2VR_VGMSTREAM_ZIP_URL`

## ビルド

```powershell
dotnet build .\tools\HS2VoiceReplaceGui\HS2VoiceReplaceGui.csproj -c Release
dotnet publish .\tools\HS2VoiceReplaceGui\HS2VoiceReplaceGui.csproj -c Release -r win-x64 --self-contained false -o .\tools\HS2VoiceReplaceGui\publish\win-x64
```

既定パスに `AssetsTools.NET.dll` が無い場合は、次のように指定してビルドしてください。

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\setup_assetstools.ps1
dotnet build .\tools\HS2VoiceReplaceGui\HS2VoiceReplaceGui.csproj -c Release -p:AssetsToolsNetPath=C:\path\to\AssetsTools.NET.dll
```

## 注意

- 配備先は、ユーザーが明示的に選んだ HS2 フォルダのみです
- 外部依存の取得元とライセンス条件は別途確認してください
