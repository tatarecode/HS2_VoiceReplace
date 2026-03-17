# 開発ガイド

この文書は、この export のみを前提に新しく開発を始める人向けの導線です。

## 最初に読むもの

1. `README.md` または `README_JA.md`
2. `tools/HS2VoiceReplaceGui/README.md` または `tools/HS2VoiceReplaceGui/README_JA.md`
3. 必要なら `tools/setup_local_python.ps1` による repo-local Python の準備
4. 自動テストの実行結果
5. GUI と runtime plugin のビルド前提

## プロジェクトの境界

- 主製品は GUI アプリケーションです。
- runtime plugin と AudioClip patcher は補助コンポーネントです。
- Python スクリプトは GUI ワークフローの一部として保守対象に含まれます。

## 構成の見方

- UI 実装は `tools/HS2VoiceReplaceGui` 配下にあります。
- コードは大きく次の単位に分かれています。
  - UI partial
  - application services
  - pipeline helpers
  - pure utility classes

## テスト方針

- ワークフロー全体を直接変更する前に、pure helper のテストを追加する方針を優先します。
- C# テストでは次を扱います。
  - localization
  - signatures
  - freshness checks
  - report parsing
  - target resolution
  - grid data helpers
- Python テストでは次を扱います。
  - shared CLI helpers
  - Seed-VC batch helpers
  - style-segment selection helpers

## 外部依存

- GUI と `UabAudioClipPatcher` の source build には、別途用意した `AssetsTools.NET.dll` が必要です。
- このリポジトリには `AssetsTools.NET.dll` を同梱しません。
- runtime plugin のビルドには Unity / BepInEx の game-side DLL が必要です。
- `HS2_VoiceReplace.dll` は `GameRoot` 依存のため、ローカルビルド対象として扱います。
- 実行時には依存セットアップで外部ツールを取得する場合があります。
- Python テストは `.\_tools\python310\python.exe` の repo-local Python を利用できます。
- `tools/setup_local_python.ps1` は、`tools/python_runtime_manifest.json` で定義した公式の embeddable Python からその repo-local Python をセットアップします。

## 保守上の注意

- ユーザー向け文言は `UiTextCatalog.cs` に集約します。
- 新しいロジックは、可能な限り pure helper に切り出してテスト可能にします。
- マシン固有パスや個人識別子を、ソース・テンプレート・既定値に入れないでください。

## Release Packaging

GUI、patcher、ローカルでビルドした runtime DLL をまとめた release 用 bundle が必要な場合は、ローカルで次を実行します。

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\package_release.ps1 -GameRoot=C:\path\to\HoneySelect2
```

この flow では、アプリ側のファイルだけを package します。`external_tools` のような依存インストール先は同梱しません。

その bundle をローカル環境から GitHub Releases に上げる場合は、`GITHUB_TOKEN` または `GH_TOKEN` を設定したうえで次を実行します。

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\tools\publish_github_release.ps1 -GameRoot=C:\path\to\HoneySelect2 -Tag=v1.0.0
```
