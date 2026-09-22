"""Conservative resolved-API comparison; this is not a behavioral parity certificate.

Only individually listed framework types are translated. Names, overloads, generic
constraints, optional values, visibility and virtual/final flags stay significant.
Reference assemblies predate nullable annotations; the v2 scanner excludes reference
nullability from keys while retaining nullableSignature and attributes for review.
Value-type Nullable<T> is never collapsed. Constructors are never inherited.
"""
from __future__ import annotations
import argparse
import collections
import hashlib
import json
from pathlib import Path
import re
import sys

ROOT = Path(__file__).resolve().parent.parent
# Preserve string/char constants verbatim; a type name inside an attribute argument
# is data, not an instruction to translate that argument.
TOKEN = re.compile(r'"(?:\\.|[^"\\])*"|\b[A-Za-z_@][\w.@+`]*')
COMPILER_ATTRIBUTES = (
    "System.Runtime.CompilerServices.NullableAttribute(",
    "System.Runtime.CompilerServices.NullableContextAttribute(",
    "System.Runtime.CompilerServices.CompilerGeneratedAttribute(",
    "System.Diagnostics.DebuggerBrowsableAttribute(",
)


def mapped(text: str, substitutions: dict[str, str]) -> str:
    # Whitespace outside literals is insignificant; whitespace inside defaults is not.
    parts, last = [], 0
    for match in TOKEN.finditer(text):
        parts.append(re.sub(r"\s+", " ", text[last:match.start()]))
        token = match.group()
        parts.append(substitutions.get(token, token))
        last = match.end()
    parts.append(re.sub(r"\s+", " ", text[last:]))
    return "".join(parts).strip()


def load_inventory(path: Path) -> dict:
    data = json.loads(path.read_text(encoding="utf-8-sig"))
    if data.get("schema", 0) < 2 or not data.get("types"):
        raise ValueError(f"{path}: a nonempty resolved PE inventory is required")
    declarations = data["declarations"]
    if declarations != sorted(set(declarations)) or len(declarations) != data["count"]:
        raise ValueError(f"{path}: unsorted/duplicate/inconsistent declarations")
    actual = hashlib.sha256(("\n".join(declarations) + "\n").encode()).hexdigest()
    if actual != data["sha256"]:
        raise ValueError(f"{path}: declaration digest mismatch")
    names = [t["name"] for t in data["types"]]
    if len(names) != len(set(names)) or len(names) != data["typeCount"]:
        raise ValueError(f"{path}: inconsistent exported type records")
    return data


def attributes(record: dict, substitutions: dict[str, str]) -> list[str]:
    result = []
    for label, values in [("attribute", record.get("attributes", [])),
                          ("return", record.get("returnAttributes", []))]:
        result += [label + ":" + mapped(v, substitutions) for v in values
                   if not v.startswith(COMPILER_ATTRIBUTES)]
    for index, parameter in enumerate(record.get("parameters", [])):
        result += [f"parameter[{index}]:" + mapped(v, substitutions)
                   for v in parameter.get("attributes", [])
                   if not v.startswith(COMPILER_ATTRIBUTES)]
    return sorted(result)


def compare(reference: dict, implementation: dict, substitutions: dict[str, str]) -> dict:
    if implementation.get("schema", 0) < 3:
        raise ValueError("Implementation inventory must include v2 resolved inheritance candidates")
    actual_types = {t["name"]: t for t in implementation["types"]}
    entries, details, attribute_differences = [], [], []
    used_types = set()
    for expected_type in reference["types"]:
        name = expected_type["name"]
        expected_name = mapped(name, substitutions)
        actual_type = actual_types.get(expected_name)
        if actual_type is None:
            entries.append({"id": "type:" + name, "status": "missingType", "reference": expected_type["key"]})
            for member in expected_type["members"]:
                entries.append({"id": name + " | " + member["key"], "status": "missingTypeMember", "reference": member["key"]})
            continue
        used_types.add(expected_name)
        type_match = mapped(expected_type["key"], substitutions) == mapped(actual_type["key"], {})
        entries.append({"id": "type:" + name, "status": "matchedType" if type_match else "typeShapeDifference",
                        "reference": expected_type["key"], "implementation": actual_type["key"]})
        if not type_match:
            details.append({"type": name, "referenceBaseChain": expected_type.get("baseClasses", []),
                            "implementationBaseChain": actual_type.get("baseClasses", []),
                            "referenceInterfaces": expected_type.get("interfaces", []),
                            "implementationInterfaces": actual_type.get("interfaces", [])})
        declared = actual_type["members"]
        # The scanner removes candidates hidden by any closer declaration, including
        # non-public members. Repeat the immediate hiding guard as defense in depth.
        hidden = set(actual_type.get("declaredNames", [])) | {m["metadataName"] for m in declared}
        inherited = [m for m in actual_type.get("inheritedMembers", [])
                     if m["metadataName"] not in hidden and m["metadataName"] not in (".ctor", ".cctor")]
        for member in expected_type["members"]:
            key = mapped(member["key"], substitutions)
            matches = [(m, "matchedDeclared") for m in declared if mapped(m["key"], {}) == key]
            if not matches and member["metadataName"] not in (".ctor", ".cctor"):
                matches = [(m, "matchedInherited") for m in inherited if mapped(m["key"], {}) == key]
            identity = name + " | " + member["key"]
            if matches:
                chosen, status = matches[0]
                entries.append({"id": identity, "status": status, "reference": member["key"],
                                "implementation": chosen["key"], "declaringType": chosen.get("declaringType", expected_name)})
                expected_attributes = attributes(member, substitutions)
                actual_attributes = attributes(chosen, {})
                if expected_attributes != actual_attributes:
                    attribute_differences.append({"id": identity, "reference": expected_attributes, "implementation": actual_attributes})
            else:
                candidates = [m["key"] for m in declared + inherited
                              if m["metadataName"] == member["metadataName"] and m["kind"] == member["kind"]]
                entries.append({"id": identity, "status": "signatureDifference" if candidates else "missingMember",
                                "reference": member["key"], "candidates": sorted(set(candidates))})
        expected_attributes = attributes(expected_type, substitutions)
        actual_attributes = attributes(actual_type, {})
        if expected_attributes != actual_attributes:
            attribute_differences.append({"id": "type:" + name, "reference": expected_attributes, "implementation": actual_attributes})
    entries.sort(key=lambda e: e["id"])
    counts = dict(sorted(collections.Counter(e["status"] for e in entries).items()))
    unresolved = sorted(e["id"] for e in entries if not e["status"].startswith("matched"))
    diagnostics = unresolved + ["attributes:" + e["id"] for e in attribute_differences]
    return {"schema": 1, "comparison": "resolved declared signatures and non-hidden inherited counterparts, with explicit type substitutions",
            "referenceSha256": reference["sha256"], "implementationSha256": implementation["sha256"],
            "referenceEntryCount": len(entries), "counts": counts,
            "mappedSignatureMatches": sum(n for k, n in counts.items() if k.startswith("matched")),
            "unresolvedSignatureCount": len(unresolved), "attributeDifferenceCount": len(attribute_differences),
            "diagnostics": sorted(diagnostics), "additionalTypes": sorted(set(actual_types) - used_types),
            "entries": entries, "typeDetails": details, "attributeDifferences": attribute_differences,
            "compilerAttributePrefixesSeparatedFromContract": COMPILER_ATTRIBUTES,
            "fullCompatibilityVerified": False}


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("implementation", type=Path)
    parser.add_argument("--reference", type=Path, default=ROOT / "contracts/avalondock-metadata-release.json")
    parser.add_argument("--mapping", type=Path, default=ROOT / "contracts/type-mappings.json")
    parser.add_argument("--output", type=Path, default=ROOT / "artifacts/metadata-diff.json")
    parser.add_argument("--baseline", type=Path, help="Fail for new diagnostics relative to a reviewed, reference-bound baseline")
    parser.add_argument("--write-baseline", type=Path, help="Write unresolved diagnostics for explicit review; never claims full parity")
    parser.add_argument("--strict", action="store_true")
    args = parser.parse_args()
    mapping_bytes = args.mapping.read_bytes()
    mapping = json.loads(mapping_bytes)
    substitutions = {item["source"]: item["target"] for item in mapping["types"]}
    if len(substitutions) != len(mapping["types"]):
        raise ValueError("Duplicate type mappings are forbidden")
    result = compare(load_inventory(args.reference), load_inventory(args.implementation), substitutions)
    result["mappingSha256"] = hashlib.sha256(mapping_bytes).hexdigest()
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(result, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    print(f"Resolved: {result['mappedSignatureMatches']}/{result['referenceEntryCount']} signatures matched; "
          f"{result['unresolvedSignatureCount']} unresolved; {result['attributeDifferenceCount']} attribute differences")
    if args.write_baseline:
        args.write_baseline.parent.mkdir(parents=True, exist_ok=True)
        baseline = {key: result[key] for key in ("referenceSha256", "mappingSha256", "diagnostics")}
        baseline.update(schema=1, fullCompatibilityVerified=False,
                        purpose="Known unresolved compatibility diagnostics, not an approval of behavioral parity")
        args.write_baseline.write_text(json.dumps(baseline, indent=2, ensure_ascii=False) + "\n", encoding="utf-8")
    if args.baseline:
        baseline = json.loads(args.baseline.read_text(encoding="utf-8"))
        for key in ("referenceSha256", "mappingSha256"):
            if baseline.get(key) != result[key]:
                raise ValueError("Baseline belongs to another contract or mapping: " + key)
        regressions = sorted(set(result["diagnostics"]) - set(baseline["diagnostics"]))
        if regressions:
            print("New unresolved API diagnostics:\n" + "\n".join(regressions), file=sys.stderr)
            return 1
    return int(args.strict and bool(result["diagnostics"]))


if __name__ == "__main__":
    try:
        sys.exit(main())
    except (ValueError, KeyError, OSError) as error:
        print("Metadata comparison failed: " + str(error), file=sys.stderr)
        sys.exit(2)
