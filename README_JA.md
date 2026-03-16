# HS2VoiceReplace

HS2VoiceReplace は、Honey Select 2 のボイス差し替え package を作成して配備するための Windows GUI ツールです。

元のゲーム音声を抽出し、用意したお手本音声に寄せて Seed-VC で変換し、ゲームで使える差し替えデータとしてまとめます。対象は `クール` や `ヤンデレ` のような既存性格です。新しい性格枠を追加するものではありません。

生成物は base game のファイルを直接書き換えず、`mods` と `BepInEx\plugins` を使って追加配置する前提です。GUI から性格ごとの配備と配備解除を行えます。

## 前提環境

- `HS2VoiceReplaceGui.exe` を実行できる Windows 環境
- `.NET 8 Desktop Runtime`
- 抽出元として参照できる Honey Select 2 環境
- `mods` と `BepInEx\plugins` を使う配備先 HS2 環境
- 寄せたい声のお手本音声

変換側の依存は、GUI のワークフロー内でセットアップできるものがあります。

## クイックスタート

- `HS2VoiceReplaceGui.exe`
  - ワークフロー全体を操作するメイン GUI
- 生成される `HS2_VoiceReplace.dll`
  - 配備時に使う runtime DLL
- 生成される `HS2VoiceReplace_cXX_*.zipmod`
  - 性格ごとの zipmod

1. `HS2VoiceReplaceGui.exe` を起動する
2. HS2 フォルダと対象性格を選ぶ
3. お手本音声を用意し、必要なら依存セットアップを実行する
4. 抽出、試聴、全量変換を進める
5. GUI から配備するか、生成物を手動配置する
   - `HS2_VoiceReplace.dll` は `BepInEx\plugins`
   - `HS2VoiceReplace_cXX_*.zipmod` は `mods`

## Seed-VC 設定

- `v1`
  - 元のしゃべり方に寄りやすい
  - 既存ボイス差し替えではまずこれが無難
- `v2`
  - 変化を強めに出したいとき向き

よく触る項目:

- `DiffusionSteps`
  - 上げるほど遅くなるが、結果が安定しやすい
- `LengthAdjust`
  - セリフの長さを調整する
- `IntelligibilityCfgRate`
  - 発音の分かりやすさを強める
- `SimilarityCfgRate`
  - お手本音声への寄せを強める
- `Temperature` / `TopP`
  - 結果の揺れ方を調整する

## 補足

- このリポジトリから実行した場合、作業データの既定先は repo-local の `.hs2voicereplace` フォルダです
- 作業データの保存先は GUI の基本設定から変更できます
- GitHub Actions の artifact では `HS2VoiceReplaceGui.exe/.dll` と `UabAudioClipPatcher.exe/.dll` を取得できます
- `HS2_VoiceReplace.dll` は有効な HS2 `GameRoot` が必要なため、ローカル環境でビルドします

## 開発向け情報

開発専用の情報はこの README には入れていません。

- ソース構成とビルド前提
  - `DEVELOPMENT.md`
  - `DEVELOPMENT_JA.md`
- 自動テスト
  - `TESTING.md`
  - `TESTING_JA.md`
- ツール別メモ
  - `tools/HS2VoiceReplaceGui/README.md`
  - `tools/HS2VoiceReplaceGui/README_JA.md`
  - `tools/UabAudioClipPatcher/README.md`
  - `tools/UabAudioClipPatcher/README_JA.md`
  - `runtime/HS2VoiceReplace.Runtime/README.md`
  - `runtime/HS2VoiceReplace.Runtime/README_JA.md`
