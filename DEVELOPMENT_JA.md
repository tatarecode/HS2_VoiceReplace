# 開発ガイド

この文書は、この export のみを前提に新しく開発を始める人向けの導線です。

## 最初に読むもの

1. `README.md` または `README_JA.md`
2. `tools/HS2VoiceReplace/README.md` または `tools/HS2VoiceReplace/README_JA.md`
3. 自動テストの実行結果
4. GUI と runtime plugin のビルド前提

## プロジェクトの境界

- 主製品は GUI アプリケーションです。
- runtime plugin と AudioClip patcher は補助コンポーネントです。
- Python スクリプトは GUI ワークフローの一部として保守対象に含まれます。

## 構成の見方

- UI 実装は `tools/HS2VoiceReplace` 配下にあります。
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
- 実行時には依存セットアップで外部ツールを取得する場合があります。

## 保守上の注意

- ユーザー向け文言は `UiTextCatalog.cs` に集約します。
- 新しいロジックは、可能な限り pure helper に切り出してテスト可能にします。
- マシン固有パスや個人識別子を、ソース・テンプレート・既定値に入れないでください。
