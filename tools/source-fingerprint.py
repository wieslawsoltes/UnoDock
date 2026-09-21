"""Hash the tracked source tree, excluding the attestation that records its hash.

The attestation can be committed after verification without creating a
self-referential commit-hash requirement. Untracked/working-tree changes are
not included: release verification always operates on a clean checkout.
"""
import hashlib
import subprocess


def fingerprint(ref="HEAD"):
    entries = subprocess.check_output(["git", "ls-tree", "-r", "-z", "--full-tree", ref])
    records = [record for record in entries.split(b"\0") if record]
    records = [record for record in records
               if record.partition(b"\t")[2] != b"contracts/release-attestation.json"]
    return hashlib.sha256(b"UnoDock.source-tree.v1\0" + b"\0".join(sorted(records)) + b"\0").hexdigest()


if __name__ == "__main__":
    print(fingerprint())
