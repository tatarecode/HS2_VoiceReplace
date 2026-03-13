# HS2VoiceReplace Runtime Template

このディレクトリには、runtime zipmod を生成する際に使うテンプレートファイルが含まれています。

## 目的

- 既存の性格 ID に対して音声バンドルだけを差し替える
- 性格名やゲーム内挙動は元のまま維持する
- 実際のゲーム環境を直接書き換えない

## 想定される payload 構成

生成される zipmod には、たとえば次のような bundle 置換が含まれます。

- `abdata/sound/data/pcm/cXX/adv/30.unity3d`
- `abdata/sound/data/pcm/cXX/adv/50.unity3d`
- `abdata/sound/data/pcm/cXX/etc/30.unity3d`
- `abdata/sound/data/pcm/cXX/etc/50.unity3d`
- `abdata/sound/data/pcm/cXX/h/bre/30.unity3d`
- `abdata/sound/data/pcm/cXX/h/bre/50.unity3d`

実際に使われる bundle 番号は、対象性格と利用可能なゲームデータによって決まります。ツールは性格に適した bundle を優先し、必要に応じて利用可能な source bundle へフォールバックします。

## 補足

- `cXX` は対象性格のディレクトリを表します
- runtime plugin DLL 自体は `runtime/HS2VoiceReplace.Runtime` からビルドされます
- このフォルダは配布用の完成 mod ではなく、packaging 用のテンプレート入力です
