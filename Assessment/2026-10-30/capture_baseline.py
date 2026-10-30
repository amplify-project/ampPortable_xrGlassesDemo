"""Copy and fingerprint the assessment inputs without modifying the Unity project."""

import hashlib
import json
import re
import shutil
import subprocess
import sys
import tempfile
from datetime import datetime, timezone
from pathlib import Path


ROOT = Path(__file__).resolve().parents[2]
EVIDENCE = Path(__file__).resolve().parent
RUN = ROOT / "Temp" / "PreMergeAssessment-20261030"
PROJECT = RUN / "project"
SHORT_RECORD = EVIDENCE / "short-path-project.json"
SCOPES = ("Assets", "Packages", "ProjectSettings")


def sha256(path):
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def git(*arguments):
    return subprocess.check_output(
        ["git", *arguments], cwd=ROOT, text=True, encoding="utf-8"
    ).strip()


def files():
    for scope in SCOPES:
        yield from sorted(p for p in (ROOT / scope).rglob("*") if p.is_file())


def capture():
    if PROJECT.exists():
        raise SystemExit("Snapshot already exists; refusing to overwrite it.")
    RUN.mkdir(parents=True, exist_ok=True)
    PROJECT.mkdir()
    records = []
    for source in files():
        if source.is_symlink():
            raise SystemExit(f"Symlink requires explicit handling: {source}")
        relative = source.relative_to(ROOT)
        destination = PROJECT / relative
        destination.parent.mkdir(parents=True, exist_ok=True)
        before = sha256(source)
        shutil.copy2(source, destination)
        if sha256(destination) != before or sha256(source) != before:
            raise SystemExit(f"Copy mismatch or changing source: {relative}")
        records.append({"path": relative.as_posix(), "size": source.stat().st_size,
                        "sha256": before})
    manifest = RUN / "source-manifest.json"
    manifest.write_text(json.dumps(records, indent=2) + "\n", encoding="utf-8")
    (EVIDENCE / "initial-working-tree.patch").write_bytes(subprocess.check_output(
        ["git", "diff", "--binary", "--no-ext-diff"], cwd=ROOT))
    baseline = {
        "captured_utc": datetime.now(timezone.utc).isoformat(),
        "branch": git("branch", "--show-current"),
        "commit": git("rev-parse", "HEAD"),
        "main_commit": git("rev-parse", "main"),
        "local_main_vs_branch_counts": git("rev-list", "--left-right", "--count", "main...HEAD"),
        "initial_tracked_changes": subprocess.check_output(
            ["git", "status", "--short", "--untracked-files=no"], cwd=ROOT,
            text=True, encoding="utf-8").splitlines(),
        "snapshot": PROJECT.relative_to(ROOT).as_posix(),
        "manifest": manifest.relative_to(ROOT).as_posix(),
        "manifest_sha256": sha256(manifest),
        "file_count": len(records),
        "total_bytes": sum(r["size"] for r in records),
        "scope_counts": {s: sum(r["path"].startswith(s + "/") for r in records) for s in SCOPES},
        "version": (ROOT / "ProjectSettings/ProjectVersion.txt").read_text().strip(),
        "snapshot_includes_existing_URP_changes": True,
        "note": "Working-tree baseline, not a clean-commit result. No branch switch, fetch, staging, or source edits.",
    }
    (EVIDENCE / "baseline.json").write_text(json.dumps(baseline, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(baseline, indent=2))


def verify():
    records = json.loads((RUN / "source-manifest.json").read_text(encoding="utf-8"))
    expected = {r["path"]: r["sha256"] for r in records}
    changed = [name for name, digest in expected.items()
               if not (ROOT / name).is_file() or sha256(ROOT / name) != digest]
    added = sorted(set(p.relative_to(ROOT).as_posix() for p in files()) - set(expected))
    result = {"verified_utc": datetime.now(timezone.utc).isoformat(),
              "checked_files": len(records), "changed_or_missing": changed,
              "added": added, "unchanged": not changed and not added}
    (EVIDENCE / "source-integrity.json").write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(result, indent=2))
    return 0 if result["unchanged"] else 1


def verify_snapshot(label):
    if not re.fullmatch(r"[a-zA-Z0-9-]+", label):
        raise SystemExit("Invalid evidence label.")
    project = Path(json.loads(SHORT_RECORD.read_text())["project"]) if SHORT_RECORD.exists() else PROJECT
    records = json.loads((RUN / "source-manifest.json").read_text(encoding="utf-8"))
    changed = []
    for record in records:
        path = project / record["path"]
        actual = sha256(path) if path.is_file() else None
        if actual != record["sha256"]:
            changed.append({"path": record["path"], "before": record["sha256"], "after": actual})
    original = {r["path"] for r in records}
    current = {p.relative_to(project).as_posix() for scope in SCOPES
               for p in (project / scope).rglob("*") if p.is_file()}
    result = {"checked_utc": datetime.now(timezone.utc).isoformat(), "label": label,
              "changed_or_missing": changed, "added": sorted(current - original),
              "note": "Unity-generated changes and validation instrumentation are recorded, not copied back to source."}
    (EVIDENCE / f"snapshot-drift-{label}.json").write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"label": label, "changed_or_missing": len(changed),
                      "added": len(result["added"])}))


def short_copy():
    # Keep the failed snapshot and logs; never delete/move an existing directory.
    destination = Path(tempfile.gettempdir()) / "rnp1030" / "project"
    if destination.parent.exists() or SHORT_RECORD.exists():
        raise SystemExit("Short-path snapshot already exists; refusing overwrite.")
    records = json.loads((RUN / "source-manifest.json").read_text(encoding="utf-8"))
    for record in records:
        source = ROOT / record["path"]
        if sha256(source) != record["sha256"]:
            raise SystemExit("Source changed since capture: " + record["path"])
        target = destination / record["path"]
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source, target)
        if sha256(target) != record["sha256"]:
            raise SystemExit("Copy mismatch: " + record["path"])
    result = {"copied_utc": datetime.now(timezone.utc).isoformat(),
              "project": str(destination), "verified_files": len(records),
              "manifest_sha256": sha256(RUN / "source-manifest.json"),
              "reason": "Reduce Windows path lengths for nested LiveKit Git submodules; identical assessment inputs, fresh Library."}
    SHORT_RECORD.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(result, indent=2))


def canonical_copy():
    destination = Path(tempfile.gettempdir()).resolve() / "rnc1030" / "project"
    if destination.parent.exists():
        raise SystemExit("Canonical snapshot already exists; refusing overwrite.")
    records = json.loads((RUN / "source-manifest.json").read_text(encoding="utf-8"))
    for record in records:
        source = ROOT / record["path"]
        if sha256(source) != record["sha256"]:
            raise SystemExit("Source changed since capture: " + record["path"])
        target = destination / record["path"]
        target.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source, target)
        if sha256(target) != record["sha256"]:
            raise SystemExit("Copy mismatch: " + record["path"])
    previous = json.loads(SHORT_RECORD.read_text())
    result = {"copied_utc": datetime.now(timezone.utc).isoformat(),
              "project": str(destination), "verified_files": len(records),
              "manifest_sha256": sha256(RUN / "source-manifest.json"),
              "previous_snapshot": previous,
              "reason": "Fresh Library at canonical Windows path; eliminate DOS8.3 alias as a cause of null MonoScript classes."}
    (EVIDENCE / "canonical-path-project.json").write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    SHORT_RECORD.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"project": str(destination), "verified_files": len(records)}, indent=2))


def fingerprint_sdk():
    dependency = json.loads((ROOT / "Packages/manifest.json").read_text())["dependencies"]["com.unity.xr.rayneo.openxr"]
    if not dependency.startswith("file:"):
        raise SystemExit("Expected the existing machine-local RayNeo package.")
    package = Path(dependency[5:])
    records = [{"path": p.relative_to(package).as_posix(), "size": p.stat().st_size,
                "sha256": sha256(p)} for p in sorted(package.rglob("*"))
               if p.is_file() and ".git" not in p.relative_to(package).parts]
    target = EVIDENCE / "external-sdk-manifest.json"
    if target.exists():
        expected = json.loads(target.read_text())
        if expected != records:
            raise SystemExit("External SDK content changed since fingerprinting.")
        print("External SDK unchanged: " + str(len(records)) + " files")
        return
    target.write_text(json.dumps(records, indent=2) + "\n", encoding="utf-8")
    metadata = json.loads((package / "package.json").read_text(encoding="utf-8-sig"))
    summary = {"captured_utc": datetime.now(timezone.utc).isoformat(),
               "path": str(package), "name": metadata["name"], "version": metadata["version"],
               "files": len(records), "total_bytes": sum(r["size"] for r in records),
               "manifest_sha256": sha256(target),
               "note": "Read-only fingerprint of the external package; not vendored, updated or independently device-certified."}
    (EVIDENCE / "external-sdk.json").write_text(json.dumps(summary, indent=2) + "\n", encoding="utf-8")
    print(json.dumps(summary, indent=2))


if __name__ == "__main__":
    if len(sys.argv) == 2 and sys.argv[1] == "capture":
        capture()
    elif len(sys.argv) == 2 and sys.argv[1] == "verify":
        sys.exit(verify())
    elif len(sys.argv) == 3 and sys.argv[1] == "verify-snapshot":
        verify_snapshot(sys.argv[2])
    elif len(sys.argv) == 2 and sys.argv[1] == "short-copy":
        short_copy()
    elif len(sys.argv) == 2 and sys.argv[1] == "canonical-copy":
        canonical_copy()
    elif len(sys.argv) == 2 and sys.argv[1] == "fingerprint-sdk":
        fingerprint_sdk()
    else:
        raise SystemExit("Usage: python capture_baseline.py capture|verify|short-copy|canonical-copy|fingerprint-sdk|verify-snapshot LABEL")
