---
name: IsExternalInit ポリフィル追加
description: Unity で C# 9 の record / init を使う際に必要なポリフィルの対処法
type: project
---

## 症状
`Assets/Scripts/Model/SpinResult.cs` で `sealed record` を使用した際に以下のエラーが発生。

```
error CS0518: Predefined type 'System.Runtime.CompilerServices.IsExternalInit' is not defined or imported
```

## 原因
Unity の Roslyn コンパイラは C# 9 の `record` / `init` アクセサに必要な
`System.Runtime.CompilerServices.IsExternalInit` 型を内包していない。

## 対処
`Assets/Scripts/IsExternalInit.cs` にポリフィルを追加。

```csharp
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}
```

**Why:** Unity 側の制約であり、コード側では回避できない。ポリフィル追加が標準的な解決策。
**How to apply:** 新しい Unity プロジェクトで `record` / `init` を使う場合は最初からこのファイルを用意する。
