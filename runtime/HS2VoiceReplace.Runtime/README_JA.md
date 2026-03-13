# HS2VoiceReplace Runtime

このディレクトリには、`HS2_VoiceReplace.dll` として配備される runtime plugin プロジェクトが含まれています。

この plugin は意図的に小さく保たれています。生成された voice-replacement package から使う runtime 側の設定と連携点だけを担当します。

## 目的

- 配備物が使う runtime DLL を提供する
- runtime 側の挙動を GUI や build pipeline から分離する
- GUI 専用ロジックを game-side plugin に持ち込まない

## ビルド前提

次の条件がすべて必要です。

- .NET Framework 4.7.2 Targeting Pack
- Honey Select 2 の実体、または同等の参照 DLL 配置を指す `GameRoot`
- `$(GameRoot)` 配下に次の DLL が存在すること
  - `BepInEx\core\BepInEx.dll`
  - `BepInEx\core\0Harmony.dll`
  - `HoneySelect2_Data\Managed\UnityEngine.dll`
  - `HoneySelect2_Data\Managed\UnityEngine.CoreModule.dll`
  - `HoneySelect2_Data\Managed\UnityEngine.UI.dll`

## ビルド

```powershell
dotnet build .\runtime\HS2VoiceReplace.Runtime\HS2VoiceReplace.Runtime.csproj -c Release -p:GameRoot=C:\path\to\HoneySelect2
```

## 出力

- assembly 名: `HS2_VoiceReplace.dll`
- target framework: `net472`

## 補足

- このプロジェクトは game-side DLL を同梱しません。
- plugin は、新しい性格 ID を注入するのではなく、既存性格 ID に対する音声差し替えに使う前提です。
