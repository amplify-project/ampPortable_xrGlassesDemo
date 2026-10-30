"""Rebuild lexical C#/Unity reference evidence; writes only assessment CSV/JSON.

Run from any directory: python Assessment/2026-10-30/inventory_code.py
Uses Python's standard library; rg is used only to independently verify file counts.
This is not a C# parser, Unity dependency analysis, reachability test, or deletion tool.
"""

import argparse
import bisect
import csv
import hashlib
import json
import re
import subprocess
from collections import Counter, defaultdict
from datetime import datetime, timezone
from pathlib import Path


REVIEW_COMMIT = "60e2ff9d0c7ee9c7604ebf1745e7b87202cc2a19"
SCRIPT_SCOPE = "Assets/AmpPortableDataViz"
SERIALIZED_SUFFIXES = {
    ".unity", ".prefab", ".asset", ".controller", ".overridecontroller", ".anim", ".playable"
}
GUID = re.compile(r"\bguid:\s*([0-9a-fA-F]{32})\b")
META_GUID = re.compile(r"^guid:\s*([0-9a-fA-F]{32})\s*$", re.MULTILINE)
IDENTIFIER = re.compile(r"\b[A-Za-z_]\w*\b")
TYPE = re.compile(
    r"\b(?P<kind>record(?:\s+(?:class|struct))?|class|struct|interface|enum)"
    r"\s+(?P<name>@?[A-Za-z_]\w*)"
)
EXTENSION = re.compile(
    r"\bstatic\s+[\w<>,.\[\]?]+\s+(?P<name>[A-Za-z_]\w*)"
    r"(?:\s*<[^>{}]+>)?\s*\(\s*this\b"
)
NON_CODE = re.compile(
    r'//[^\r\n]*|/\*.*?\*/|(?:\$@|@\$|@)"(?:""|[^"])*"'
    r'|\$?"(?:\\.|[^"\\])*"|\'(?:\\.|[^\'\\])*\'', re.DOTALL
)
LOOKUP = re.compile(
    r"\b(?P<operation>AddComponent|GetComponent(?:s)?(?:InChildren|InParent)?|"
    r"FindObjects?OfType|FindObjectsByType|FindFirstObjectByType|FindAnyObjectByType|"
    r"Resources\s*\.\s*Load)\s*<\s*(?P<name>[A-Za-z_]\w*)"
)
NEW = re.compile(r"\bnew\s+(?P<name>[A-Za-z_]\w*)")
TYPEOF = re.compile(r"\btypeof\s*\(\s*(?P<name>[A-Za-z_]\w*)")

CAVEATS = [
    "Identifier references are lexical evidence, not semantic calls, type resolution, or runtime reachability.",
    "Comments and string/character literals are masked for C# identifiers; interpolated-string expressions may be missed. Both sides of preprocessor branches are scanned.",
    "Types include nested declarations. Common names such as Settings can refer to unrelated types; ambiguous names are reported separately. Namespaces/generic arity/accessibility are not resolved.",
    "Extension method identifiers are included separately because callers need not spell the containing type name. Their names can also be ambiguous.",
    "GUID references include disabled components, inactive roots, variants, non-build scenes and recovery assets. A reference is not proof that a component executes; missing direct GUID references do not imply absence from inherited prefabs.",
    "Persistent UnityEvent type names are listed, but object/fileID and callback-method validity are not resolved. Awake/Start/OnEnable, Inspector callbacks and SDK event listeners need no external C# caller.",
    "Runtime AddComponent/new/lookups, Resources string loading, Java UnitySendMessage callbacks, external scenes/bundles and SDK code can create dependencies. The scan excludes Packages, Library, Temp and external SDK folders.",
    "Zero references never establish safe deletion. Review classifications describe the assessed commit and require re-review after changes; no source is modified by this script.",
]


def classifications():
    result = {}

    def add(names, label, reason, report):
        for name in names.split():
            if name in result:
                raise ValueError(f"Duplicate review rule: {name}")
            result[name] = (label, reason, report)

    add("FaceUserSmooth RuntimeImageBoard URPImageBoardApplier XRealControllerQuitter",
        "likely_obsolete_candidate",
        "Reviewed no-reference candidate; confirm external/manual workflows before any removal.",
        "legacy-code-review.md")
    add("VisualizationCycler ToggleButtonColor WristMenuFollower HudSquareBuilder EmotionTelemetryLogger ColourChangingHudSide EngagementHudBinding",
        "recovery_scene_retained",
        "Retained recovery-scene attachments; coordinated scene/subsystem decision required.",
        "legacy-code-review.md")
    add("AudienceDataManualDriver ParticleMeshManualDriver GraphManualDriver EmotionPlasmaManualDriver EmotionRayPlasmaManualDriver",
        "development_manual_retained",
        "Explicit manual/preview workflow and typed or serialized dependencies remain.",
        "legacy-code-review.md")
    add("AnchoredVisualization SimpleVisualizer GraphBinding EmotionPlasmaBinding EmotionPlasmaVisualizer EmotionRayPlasmaBinding EmotionRayPlasmaVisualizer FrameSmoother",
        "runtime_alternate_retained",
        "Retained alternate/fallback runtime or prefab path; retire only as a coherent subsystem.",
        "legacy-code-review.md")
    add("ActiveSensorHudBinding ArtistVizVisualizer AudienceSignalBinding AudienceSignalVisualizerBinding BillboardLabel GraphVisualizer ParticleMeshCornerIndices ParticleMeshVisualizer RuntimePngLoader SensorStreamCoordinator QuitApp RayNeoTempleSensorStreamCycler RayNeoTempleVisualizationScaler RayNeoTempleVisualizationToggle RayNeoTempleVisualizationVerticalMover",
        "runtime_active_reviewed",
        "Reviewed current scene/prefab or dynamic runtime dependency; retain during readiness work.",
        "legacy-code-review.md")
    add("Interfaces AudienceSignalContracts ParticleMeshContracts ArtistVizContracts VisualizationContracts SensorEngagementContracts RedisEndpointDiscoveryContracts RedisEndpointStartupResolver SensorStreamSelectionController SelectedSensorDataSource SensorEngagementRouter RedisSubscriber RedisDeviceDiscovery AndroidRedisEndpointDiscovery SensorEngagementJsonDecoder UnityClock RedisEndpointPrompt RedisRuntimeSettings RedisPhysioMetricsPump RedisSensorEngagementPump SensorEngagementCoordinator RedisDataPump AudienceSignalToParticleMeshMapper AudienceSignalToArtistVizMapper GraphSeriesToGraphParamsMapper DynamicGraphYRangeTracker GraphTemporalSmoother HeartRateVisualResponseMapper Bootstrapper",
        "runtime_active_reviewed",
        "Reviewed current composition/data/endpoint/mapping dependency; retain.",
        "runtime-code-review.md")
    add("DomainModels Ports VisualizationSessionController VisualizationSessionOptions VisualizationMessages MockAnchorService LocalLoopbackNetworkSync UnityVisualizationFactory FloatToSimpleParams AnchorRegistry",
        "runtime_fallback_retained",
        "Typed session/fallback or unconditional runtime construction remains.",
        "runtime-code-review.md")
    add("SineWaveSource", "runtime_development_retained",
        "Bootstrapper creates and selects this development/fallback source.", "runtime-code-review.md")
    add("AffectContracts ArousalValenceToEmotionPlasmaMapper ArousalValenceToRaymarchPlasmaMapper PlasmaAffectMapper RaymarchPlasmaParamsMapper",
        "optional_plasma_subsystem_candidate",
        "Retained plasma callers/assets require coordinated support decision; no standalone deletion.",
        "runtime-code-review.md")
    add("LiveKitTelemetryService LiveKitTelemetryReceiver", "optional_livekit_subsystem_candidate",
        "Retained bootstrap/package/test paths; decide whether LiveKit remains supported.", "runtime-code-review.md")
    add("IHandPoseProvider", "wrist_subsystem_candidate",
        "Retained wrist follower and recovery/external provider check required.", "runtime-code-review.md")
    add("RedisManager", "legacy_wrapper_candidate",
        "Recovery-only scene wrapper still has typed bootstrap lifecycle/configuration references.", "runtime-code-review.md")
    add("EngagementToHudSideMapper", "optional_hud_subsystem_candidate",
        "Retained HUD binding/bootstrap/recovery paths require coordinated support decision.", "runtime-code-review.md")
    add("ArtisticElementController ArtisticElementsControlPanel ArtisticElementsPanelLayout ArtisticElementsSelectionController ArtisticElementsTestSetup",
        "development_test_feature_retained",
        "ArtisticElementsTest scene, dynamic component creation and dedicated tests remain.", "runtime-code-review.md")
    add("AssemblyInfo", "assembly_test_support_retained",
        "AssemblyInfo.cs:3 grants InternalsVisibleTo(Tests); no type declaration expected.", "direct source review")
    add("TaskExtensions", "runtime_fallback_retained",
        "Bootstrapper.cs:440 invokes AsIEnumerator extension; a containing-type name is not required at call sites.", "direct source review")
    return result


def masked_code(text):
    return NON_CODE.sub(lambda match: re.sub(r"[^\r\n]", " ", match.group()), text)


def sha256(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def git(root, *args):
    command = subprocess.run(["git", *args], cwd=root, text=True, encoding="utf-8", capture_output=True)
    return command.stdout.strip() if command.returncode == 0 else None


def run(root, output):
    assets = root / "Assets"
    targets = sorted((root / SCRIPT_SCOPE).rglob("*.cs"))
    if not targets:
        raise SystemExit(f"No first-party C# files under {root / SCRIPT_SCOPE}")
    all_cs = sorted(assets.rglob("*.cs"))
    all_meta = sorted(assets.rglob("*.meta"))
    serialized = sorted(path for path in assets.rglob("*") if path.is_file() and path.suffix.lower() in SERIALIZED_SUFFIXES)
    warnings = []
    skipped_symlinks = []

    def relative(path):
        return path.relative_to(root).as_posix()

    def read(path):
        if path.is_symlink():
            skipped_symlinks.append(relative(path))
            return ""
        try:
            return path.read_text(encoding="utf-8-sig")
        except UnicodeDecodeError:
            warnings.append(f"Non-UTF8 content read with replacement: {relative(path)}")
            return path.read_text(encoding="utf-8-sig", errors="replace")

    code = {}
    token_refs = defaultdict(set)
    construction_refs = defaultdict(set)
    owners = defaultdict(set)
    for path in all_cs:
        name = relative(path)
        text = read(path)
        masked = masked_code(text)
        newlines = [match.start() for match in re.finditer("\n", masked)]
        line_at = lambda position: bisect.bisect_left(newlines, position) + 1
        declarations = [dict(kind=m.group("kind"), name=m.group("name").lstrip("@"), line=line_at(m.start()))
                        for m in TYPE.finditer(masked)]
        extensions = sorted(set(match.group("name") for match in EXTENSION.finditer(masked)))
        declaration_starts = {match.start("name") for match in TYPE.finditer(masked)}
        for match in IDENTIFIER.finditer(masked):
            if match.start() not in declaration_starts:
                token_refs[match.group()].add((name, line_at(match.start())))
        for pattern, operation in ((LOOKUP, None), (NEW, "new"), (TYPEOF, "typeof/attribute-or-reflection")):
            for match in pattern.finditer(masked):
                op = operation or re.sub(r"\s+", "", match.group("operation"))
                construction_refs[match.group("name")].add((name, line_at(match.start()), op))
        code[name] = {"types": declarations, "extensions": extensions, "sha256": sha256(path)}
        if path in targets:
            for declaration in declarations:
                owners[declaration["name"]].add(name)

    meta_by_guid = defaultdict(list)
    meta_guid = {}
    invalid_meta = []
    for path in all_meta:
        matches = META_GUID.findall(read(path))
        if len(matches) != 1:
            invalid_meta.append(relative(path))
            continue
        guid = matches[0].lower()
        meta_guid[relative(path)] = guid
        meta_by_guid[guid].append(relative(path))

    target_guids = {meta_guid.get(relative(path) + ".meta") for path in targets} - {None}
    guid_refs = defaultdict(set)
    unityevent_types = defaultdict(set)
    for path in serialized:
        name = relative(path)
        for number, line in enumerate(read(path).splitlines(), 1):
            for match in GUID.finditer(line):
                guid = match.group(1).lower()
                if guid in target_guids:
                    kind = "script_attachment" if "m_Script:" in line else "serialized_guid"
                    guid_refs[guid].add((name, number, kind))
            if "m_TargetAssemblyTypeName:" in line:
                for token in IDENTIFIER.findall(line):
                    if token in owners:
                        unityevent_types[token].add((name, number))

    rules = classifications()
    rows = []
    missing_meta = []
    invalid_target_meta = []
    for path in targets:
        name = relative(path)
        info = code[name]
        guid = meta_guid.get(name + ".meta", "")
        if not Path(str(path) + ".meta").is_file():
            missing_meta.append(name)
        elif not guid:
            invalid_target_meta.append(name)
        symbols = {declaration["name"] for declaration in info["types"]} | set(info["extensions"])
        references = defaultdict(set)
        constructors = defaultdict(set)
        events = defaultdict(set)
        for symbol in symbols:
            for other, line in token_refs[symbol]:
                if other != name:
                    references[(other, line)].add(symbol)
            for other, line, op in construction_refs[symbol]:
                if other != name:
                    constructors[(other, line, op)].add(symbol)
            for other, line in unityevent_types[symbol]:
                events[(other, line)].add(symbol)

        def is_test(other):
            return other.startswith("Assets/Tests/") or "/Tests/" in other or "/Editor/Tests/" in other

        def csharp_locations(test):
            return [{"location": f"{other}:{line}", "identifiers": sorted(names)}
                    for (other, line), names in sorted(references.items()) if is_test(other) == test]

        runtime = csharp_locations(False)
        tests = csharp_locations(True)
        serialized_refs = [{"location": f"{other}:{line}", "kind": kind}
                           for other, line, kind in sorted(guid_refs[guid])]
        serialized_paths = sorted({other for other, _, _ in guid_refs[guid]})
        recovery_paths = [other for other in serialized_paths if other.startswith("Assets/_Recovery/")]
        label, basis, report = rules.get(path.stem, (
            "unreviewed_static_evidence", "No hand-reviewed classification rule; inspect evidence.", "none"))
        if label == "likely_obsolete_candidate" and (runtime or tests or serialized_refs or events):
            label = "previous_candidate_requires_recheck"
            basis += " New lexical references are present; previous zero-reference finding no longer holds."
        rows.append({
            "path": name,
            "source_sha256": info["sha256"],
            "defined_types_lexical": info["types"],
            "extension_method_identifiers": info["extensions"],
            "ambiguous_type_names": sorted(symbol for symbol in symbols if len(owners[symbol]) > 1),
            "script_guid": guid,
            "serialized_asset_count": len(serialized_paths),
            "recovery_asset_count": len(recovery_paths),
            "serialized_guid_ref_count": len(serialized_refs),
            "serialized_guid_refs": serialized_refs,
            "non_test_csharp_ref_count": len(runtime),
            "non_test_csharp_refs_lexical": runtime,
            "test_csharp_ref_count": len(tests),
            "test_csharp_refs_lexical": tests,
            "construction_and_lookup_refs_lexical": [
                {"location": f"{other}:{line}", "operation": op, "identifiers": sorted(names)}
                for (other, line, op), names in sorted(constructors.items())],
            "unityevent_type_name_refs_lexical": [
                {"location": f"{other}:{line}", "identifiers": sorted(names)}
                for (other, line), names in sorted(events.items())],
            "review_classification": label,
            "classification_basis": basis,
            "classification_source": report,
            "evidence_level": "lexical scan plus snapshot-specific human-reviewed classification; not semantic/reachability proof",
        })

    try:
        rg = subprocess.run(["rg", "--files", SCRIPT_SCOPE, "-g", "*.cs"], cwd=root,
                            text=True, encoding="utf-8", capture_output=True)
        rg_paths = sorted(line.replace("\\", "/") for line in rg.stdout.splitlines())
        python_paths = [row["path"] for row in rows]
        rg_check = {"command": "rg --files Assets/AmpPortableDataViz -g *.cs",
                    "exit_code": rg.returncode, "rg_count": len(rg_paths),
                    "python_count": len(rows), "paths_match": rg.returncode == 0 and rg_paths == python_paths,
                    "python_only": sorted(set(python_paths) - set(rg_paths)),
                    "rg_only": sorted(set(rg_paths) - set(python_paths))}
    except FileNotFoundError:
        rg_check = {"paths_match": False, "reason": "rg unavailable; independent verification not performed"}

    output.mkdir(parents=True, exist_ok=True)
    csv_path = output / "code-inventory.csv"
    with csv_path.open("w", encoding="utf-8", newline="") as stream:
        writer = csv.DictWriter(stream, fieldnames=list(rows[0]))
        writer.writeheader()
        for row in rows:
            writer.writerow({key: json.dumps(value, ensure_ascii=False, separators=(",", ":"))
                             if isinstance(value, (list, dict)) else value for key, value in row.items()})
    commit = git(root, "rev-parse", "HEAD")
    report_hashes = {report: sha256(Path(__file__).parent / report)
                     for report in ("legacy-code-review.md", "runtime-code-review.md")
                     if (Path(__file__).parent / report).is_file()}
    summary = {
        "generated_utc": datetime.now(timezone.utc).isoformat(),
        "branch": git(root, "branch", "--show-current"), "commit": commit,
        "review_classification_commit": REVIEW_COMMIT,
        "review_commit_matches_HEAD": commit == REVIEW_COMMIT,
        "script_scope": SCRIPT_SCOPE,
        "caller_scope": "All .cs files under Assets, including Assets/Tests; excludes Packages/Library/Temp/external SDK directories",
        "serialized_scope": "All matching file suffixes under Assets, including _Recovery and inactive/non-build assets",
        "serialized_suffixes": sorted(SERIALIZED_SUFFIXES),
        "script_count": len(rows), "csharp_caller_files_scanned": len(all_cs),
        "test_caller_files_scanned": sum(name.startswith("Assets/Tests/") or "/Tests/" in name for name in code),
        "serialized_files_scanned": len(serialized), "metadata_files_scanned": len(all_meta),
        "defined_type_declarations_lexical": sum(len(row["defined_types_lexical"]) for row in rows),
        "classification_counts": dict(sorted(Counter(row["review_classification"] for row in rows).items())),
        "layer_counts": dict(sorted(Counter(Path(row["path"]).parts[2] if len(Path(row["path"]).parts) > 3 else "root"
                                           for row in rows).items())),
        "missing_script_metadata": missing_meta, "invalid_script_metadata": invalid_target_meta,
        "invalid_asset_metadata": invalid_meta,
        "duplicate_asset_guid_groups": {guid: names for guid, names in sorted(meta_by_guid.items()) if len(names) > 1},
        "duplicate_inventory_script_guid_groups": {guid: [row["path"] for row in rows if row["script_guid"] == guid]
                                                    for guid in sorted(target_guids)
                                                    if sum(row["script_guid"] == guid for row in rows) > 1},
        "ambiguous_type_names": {name: sorted(paths) for name, paths in sorted(owners.items()) if len(paths) > 1},
        "no_declared_types": [row["path"] for row in rows if not row["defined_types_lexical"]],
        "unreviewed_rows": [row["path"] for row in rows if row["review_classification"] == "unreviewed_static_evidence"],
        "independent_rg_verification": rg_check,
        "csv_sha256": sha256(csv_path), "review_report_sha256": report_hashes,
        "read_warnings": sorted(set(warnings)), "skipped_symlinks": sorted(set(skipped_symlinks)),
        "caveats": CAVEATS,
    }
    (output / "scan-summary.json").write_text(json.dumps(summary, indent=2) + "\n", encoding="utf-8")
    print(json.dumps({"script_count": len(rows), "classification_counts": summary["classification_counts"],
                      "missing_script_metadata": missing_meta, "invalid_script_metadata": invalid_target_meta,
                      "duplicate_asset_guid_groups": len(summary["duplicate_asset_guid_groups"]),
                      "unreviewed_rows": summary["unreviewed_rows"], "rg_verification": rg_check,
                      "read_warnings": summary["read_warnings"]}, indent=2))
    return 0 if rg_check["paths_match"] and not missing_meta and not invalid_target_meta else 1


if __name__ == "__main__":
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=Path(__file__).resolve().parents[2])
    parser.add_argument("--output-dir", type=Path, default=Path(__file__).resolve().parent)
    args = parser.parse_args()
    raise SystemExit(run(args.root.resolve(), args.output_dir.resolve()))
