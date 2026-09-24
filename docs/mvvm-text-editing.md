# Exact application text and native editor text

The MVVM sample separates `WorkspaceDocument.Text` (exact application-owned UTF16
text) from `EditorText` (the native TextBox's CR-based editing representation).
File-save snapshots always use `Text`. Layout XML stores identities and placement,
not the editor buffer. Product namespaces remain `UnoDock.*`.

## No-op and edit semantics

A native no-op leaves the original string, dirty state and notifications unchanged,
even when the original uses LF, CRLF, CR or mixed delimiters. Merely realizing an
editor, moving focus, switching tabs or restoring layout must not convert a file's
line endings. The editor projection is cached until the application text changes;
the cache is invalidated before property observers execute.

A native replacement is treated as one contiguous changed UTF16 region determined
by equal prefix/suffix lengths. Unchanged application spans are retained verbatim.
New line breaks in the changed region use the first existing delimiter (LF for a
single-line/empty original). Multiple disjoint changes are treated as a single
replacement envelope, not a general-purpose minimal edit script. Unchanged delimiter
choices inside that envelope are not guaranteed to survive; outside it they are.

CRLF is a two-code-unit token in application text but one code unit in the native
projection. Joining an unchanged CR-ending prefix to an LF-starting insertion or
suffix can accidentally collapse two logical breaks into one. The projection adds
one CR at such a splice, keeping both untouched spans verbatim while preserving the
requested logical line count. For example:

```text
original:  a\nb\rc
editor:    a\rb\r\rc
result:    a\nb\r\r\nc
```

This is a text representation policy, not preservation of a file encoding/BOM,
a grapheme-based selection mapping, or a general-purpose code editor implementation.
Unicode line/paragraph separators remain distinct from the CR/LF delimiter policy.

## Regression evidence

The original compiled-editor test incorrectly required `Text == TextBox.Text`
after the projection had been introduced. It now asserts the two distinct contracts:
the CR view must match exactly, and application text must independently retain the
expected LF content. It also checks that initial native realization stays clean.
No assertion uses whitespace trimming or ignores delimiters.

Additional tests cover explicit delimiter fixtures, unchanged string identity,
notification-free no-ops, cache invalidation, exact save/revert baselines, three
splice-boundary counterexamples and 10,000 deterministic edits checked against an
independent small canonicalization transducer. Live Uno cases exercise editing,
saving, reverting, tab switching and XML restoration with exact expected strings.
They are included in the ordinary Linux and selected Windows acceptance suites.
The native Save/Revert XTEST cases and all earlier restore ownership tests remain.

CI results on the exact revision establish which tests actually passed. This document
makes no blanket Windows/macOS/browser equivalence or original MVVM pixel-parity claim.
The original public observations, metadata inventories, mappings and regression
allowlist are unchanged. See [workspace architecture](mvvm-workspace.md).
