# HS2VoiceReplace

HS2VoiceReplace は、Honey Select 2 の音声差し替え package を生成して配備するための Windows GUI ツールです。

このツールは、既存の HS2 環境から元のボイスデータを取り出し、寄せたい声のお手本音声を使って Seed-VC で変換し、ゲームで使える差し替えデータとして組み直して配備するためのものです。

- `HS2VoiceReplaceGui.exe`
  - ワークフロー全体を制御するメイン GUI
- 生成された `HS2_VoiceReplace.dll`
  - 配備物が使用する動作用 DLL
- 生成された `HS2VoiceReplace_cXX_*.zipmod`
  - GUI が性格ごとに出力する zipmod

対象は `クール` や `ヤンデレ` のような既存性格です。
このツールは新しい性格枠を追加するのではなく、既存性格に対する差し替え用 asset を作る前提です。

生成物は、元のゲームデータを直接書き換えるのではなく、`mods` と `BepInEx\plugins` を使って追加配置する形を前提にしています。
GUI からは性格単位で配備と配備解除を行えます。

## 前提環境

- `HS2VoiceReplaceGui.exe` を実行できる Windows 環境
- `.NET 8 Desktop Runtime`
- 元音声を読み出すための Honey Select 2 環境
- `mods` と `BepInEx\plugins` を使う配備先 HS2 環境
- 寄せたい声のお手本音声

変換側の依存物は、GUI のワークフロー内でセットアップできるものがあります。

## クイックスタート

1. `HS2VoiceReplaceGui.exe` を起動する
2. 元になる HS2 フォルダと対象性格を選ぶ
3. お手本音声を用意し、必要なら依存セットアップを行う
4. 抽出、試聴、全量変換を進める
5. GUI から配備するか、生成物を手動配置する
   - `HS2_VoiceReplace.dll` を `BepInEx\plugins` に置く
   - `HS2VoiceReplace_cXX_*.zipmod` を `mods` に置く

## Seed-VC 設定

- `v1`
  - 元のしゃべり方に寄りやすいです
  - 既存ボイス差し替えなら、まずこれで十分です
- `v2`
  - 変化を強めに出したいとき向きです
  - お手本の声にもっと寄せたいときに使います

よく触る項目:

- `DiffusionSteps`
  - 高いほど重いけどきれいめ、低いほど速めです
- `LengthAdjust`
  - セリフの長さ感を調整します
- `IntelligibilityCfgRate`
  - 発音の分かりやすさ寄りにします
- `SimilarityCfgRate`
  - お手本の声らしさを強めます
- `Temperature` / `TopP`
  - 安定寄りにするか、変化を出すかの傾向です

## 補足

- このリポジトリ内で実行した場合、作業データの既定保存先はリポジトリ内の `.hs2voicereplace` フォルダです
- 作業データの保存先は GUI の基本設定から変更できます
- runtime 側は小さく保っており、主な制御は GUI 側で行います

## 開発者向け文書

開発向けの情報はこの README には置かず、次の文書に分けています。

- ソース構成とビルド前提
  - `DEVELOPMENT.md`
  - `DEVELOPMENT_JA.md`
- 自動テスト
  - `TESTING.md`
  - `TESTING_JA.md`
- ツール別の開発メモ
  - `tools/HS2VoiceReplaceGui/README.md`
  - `tools/HS2VoiceReplaceGui/README_JA.md`
  - `tools/UabAudioClipPatcher/README.md`
  - `tools/UabAudioClipPatcher/README_JA.md`
  - `runtime/HS2VoiceReplace.Runtime/README.md`
  - `runtime/HS2VoiceReplace.Runtime/README_JA.md`
