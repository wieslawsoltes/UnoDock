# Identity-only source bookkeeping

SourceEntry is an internal record. Removing a record with List.Remove can compare its
Value field against unrelated entries, invoking application-defined Equals. Source
reconciliation now locates the exact record by ReferenceEquals and removes its index.
Payload matching, deduplication and lookups also use ReferenceEqualityComparer. Tests
use payloads whose Equals and GetHashCode throw, verifying that source bookkeeping does
not call either method. Explicit title fallback still calls ToString by design.

A direct layout model and its payload can alias one already attached model. Removing
one alias retains the model while the other still names it; removing the final alias
releases it. This does not merge distinct application objects with equal values.

If BeforeInsert invalidates the default destination without placing the content or
claiming the insertion, the pending unattached candidate is released and a fresh pass
selects a valid destination. This uses the same 64-pass cap, not recursive insertion.
A strategy replacement cannot receive the prior strategy's AfterInsert callback.

The source-identity suite covers both document and anchorable sources. It executes in
normal Linux and selected Win32 acceptance and through UNODOCK_TEST_SUITE=source-identity.
These tests add to, rather than replace, the source-ownership and existing suites.
They exercise public model/source APIs; no original private implementation is imported.
See source-ownership.md for transaction, threading and compatibility boundaries.
