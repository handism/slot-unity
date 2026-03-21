// Unity の Roslyn コンパイラが C# 9 の record / init をサポートするためのポリフィル
// See: https://developercommunity.visualstudio.com/t/error-cs0518-predefined-type-systemruntimecompiler/1244809
namespace System.Runtime.CompilerServices
{
    internal static class IsExternalInit { }
}
