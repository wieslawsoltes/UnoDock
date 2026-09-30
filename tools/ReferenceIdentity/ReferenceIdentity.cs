// Neutral naming for reference probe output. Namespace and Neutral are generated at
// build time from contracts/reference.json, so recorded fixtures never carry the
// original root namespace and probe sources never spell it.
using System;

internal static partial class ReferenceIdentity
{
    // Rewrites a type or namespace name from the pinned reference build to the neutral token.
    internal static string Normalize(string name) => name != null && (name == Namespace || name.StartsWith(Namespace + ".", StringComparison.Ordinal) || name.StartsWith(Namespace + "+", StringComparison.Ordinal)) ? Neutral + name.Substring(Namespace.Length) : name;
    // True when a member is declared by the pinned reference build rather than the framework.
    internal static bool Declares(Type type) => (type?.Namespace ?? "").StartsWith(Namespace, StringComparison.Ordinal);
    // Resolves a public reference type by its name relative to the root namespace.
    internal static Type Resolve(string relativeName) => typeof(ReferenceApi::DockingManager).Assembly.GetType(Namespace + "." + relativeName, true);
}
