#!/usr/bin/env python3
"""Offline-only, fail-closed F30 evidence manifest verification (stdlib)."""
from __future__ import annotations

import argparse
import copy
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
MANIFEST = ROOT / "evidence/f30/f30_consolidated_manifest.json"
PINNED_CHECKOUT = "c2c379b3cbec38f393eea41ffdbd431a1e0a021c"
PINNED_MAPPING = "df5be0493a1ac7be57208ff0e91c2d7193dfec73"
PINNED_SOURCES = {
    "tests/FMRTE2008.OfflineRobustness.Windows/F30Stress.cs": "730d2f62da91ab754453e2666864e7dec1f87832",
    "tests/FMRTE2008.OfflineLifecycle.Windows/Program.cs": "749e7d6165a9858c68358993f8ac22fff91b6186",
    "tests/FMRTE2008.OfflineNativeWindows/Program.cs": "12a87d2f78f096645c95657609d878b1c5c0f09f",
    "docs/F30_1_OFFLINE_WPF_STRESS.md": "24cc1447bb781d8c4c4d99dc1b8ea092ffc6779b",
    "docs/F30_2_WPF_LIFECYCLE.md": "35aaa769e377a3068716e134eef411a316178fc9",
    "docs/F30_3_VISIBLE_WPF_RESOURCES.md": "cc4019fcb5a61b4c8b867dfbda92a68b5df478f8",
    ".github/workflows/f29-offline-robustness.yml": "f1d9a9e0d599dd9e109651d620772e3c5290eccc",
    ".github/workflows/f30-2-offline-wpf-lifecycle.yml": "915c443bc0a83a9cf849482083e22a027da05ad1",
    ".github/workflows/f30-3-offline-native-wpf.yml": "77b98e5b5487ddb233c26302f59588644cca7014",
}
STAGES = [
    ("F30.1", 32, "288aefb66aadd23842355f1ea8601c96eba48201",
     44926965311, 136915058846, 120, 5532,
     "F30.1 WPF PASS: 120 deterministic cycles, 5532 assertions, 3 schedules and 12 persistent failures"),
    ("F30.2", 33, "923fb99cea1b37526a78124dd8fb54f237160346",
     44926965354, 136915058961, 96, 986,
     "F30.2 WPF PASS: 48 isolated + 48 shared created/closed, 986 assertions; synthetic STA only"),
    ("F30.3", 34, PINNED_CHECKOUT,
     44926965330, 136915058988, 96, 1307,
     "F30.3 WPF PASS: 96 visible native HWND cycles, 1307 assertions, warmupWindows=6, peakRetained=0, finalRetained=0, rawHandles=265, baselineHandles=441, finalHandles=455, rawGdi=0, baselineGdi=22, finalGdi=22, rawUser=3, baselineUser=23, finalUser=28, peakManagedGrowthBytes=663320; synthetic current process only"),
]
RUNS = {
    44926965354: "F30.2 offline WPF lifecycle",
    44926965339: "Compilar diagnóstico somente leitura",
    44926965342: "Testes do leitor x86",
    44926965373: "Compilar protótipo Windows",
    44926965311: "F29 offline robustness",
    44926965330: "F30.3 offline displayed WPF native resources",
    44926965363: "Simulacao de memoria x86",
}
GOV_KEYS = {
    "mapping_unchanged_commit", "approval_state", "approved", "operational",
    "approvedPlayerMaps", "B03", "A12", "decision", "risk_acceptances",
    "human_reviewer", "human_review_timestamp", "human_authority", "human_approval",
}
STAGE_KEYS = {
    "stage", "merged_pr", "merge_commit", "main_revalidation_run",
    "windows_job", "expected_line", "cycles", "assertions", "synthetic",
}
TOP_KEYS = {
    "schema", "repository", "evidence_checkout_commit", "source_files",
    "stages", "ci_runs", "governance", "evidence_limitations",
}
class EvidenceError(ValueError):
    pass


def require(cond: bool, message: str) -> None:
    if not cond:
        raise EvidenceError(message)


def exact_keys(value: object, keys: set[str], name: str) -> None:
    require(type(value) is dict and set(value) == keys, f"{name}: schema not closed")


def exact_int(value: object, expected: int, name: str) -> None:
    require(type(value) is int and value == expected, f"{name}: integer differs")


def git_blob_sha1(data: bytes) -> str:
    return hashlib.sha1(b"blob " + str(len(data)).encode("ascii") + b"\x00" + data).hexdigest()


def validate(manifest: dict, root: Path = ROOT) -> dict:
    exact_keys(manifest, TOP_KEYS, "root")
    require(manifest["schema"] == "f30-offline-evidence/v1", "unknown evidence schema")
    require(manifest["repository"] == "marcosvilhena87/FMRTE.2008", "repository mismatch")
    require(manifest["evidence_checkout_commit"] == PINNED_CHECKOUT,
            "historic checkout SHA mismatch")
    files = manifest["source_files"]
    require(type(files) is list and len(files) == len(PINNED_SOURCES), "source count mismatch")
    fingerprints = []
    seen = set()
    for entry in files:
        exact_keys(entry, {"path", "git_blob_sha1"}, "source")
        path = entry["path"]
        require(type(path) is str and path in PINNED_SOURCES and path not in seen,
                "unrecognized or duplicate source path")
        seen.add(path)
        require(entry["git_blob_sha1"] == PINNED_SOURCES[path],
                f"{path}: manifest Git blob differs from immutable review anchor")
        file = root / path
        require(file.is_file() and not file.is_symlink(), f"{path}: file missing or symbolic link")
        raw = file.read_bytes()
        require(git_blob_sha1(raw) == PINNED_SOURCES[path],
                f"{path}: contents changed relative to reviewed main checkout")
        fingerprints.append({
            "path": path,
            "git_blob_sha1": git_blob_sha1(raw),
            "sha256": hashlib.sha256(raw).hexdigest(),
            "size_bytes": len(raw),
        })
    require(seen == set(PINNED_SOURCES), "not all pinned artifacts present")
    stage_data = manifest["stages"]
    require(type(stage_data) is list and len(stage_data) == 3, "stage count mismatch")
    for item, expected in zip(stage_data, STAGES):
        exact_keys(item, STAGE_KEYS, "stage")
        (label, pr, merge, run, job, cycles, asserts, marker) = expected
        for key, value in (("stage", label), ("merge_commit", merge),
                           ("expected_line", marker)):
            require(type(item[key]) is str and item[key] == value,
                    f"{label}: {key} mismatch")
        for key, value in (("merged_pr", pr), ("main_revalidation_run", run),
                           ("windows_job", job), ("cycles", cycles),
                           ("assertions", asserts)):
            exact_int(item[key], value, f"{label}.{key}")
        require(item["synthetic"] is True, f"{label}: synthetic fixture flag absent")
        require(run in RUNS, f"{label}: CI run absent")
    ci_runs = manifest["ci_runs"]
    require(type(ci_runs) is list and len(ci_runs) == len(RUNS), "CI run count mismatch")
    seen_runs = set()
    for entry in ci_runs:
        exact_keys(entry, {"run_id", "workflow", "conclusion"}, "ci run")
        run = entry["run_id"]
        require(type(run) is int and run in RUNS and run not in seen_runs,
                "duplicate or unrecognized CI run")
        seen_runs.add(run)
        require(entry["workflow"] == RUNS[run] and entry["conclusion"] == "success",
                "historical CI reference mutated")
    require(seen_runs == set(RUNS), "historical CI coverage incomplete")
    gov = manifest["governance"]
    exact_keys(gov, GOV_KEYS, "governance")
    expected_gov = {
        "mapping_unchanged_commit": PINNED_MAPPING,
        "approval_state": "NoApprovedPlayerMaps", "approved": False,
        "operational": False, "approvedPlayerMaps": 0,
        "B03": "PENDING-DECLARED", "A12": "GATE-BLOQUEADO", "decision": "NOT_MADE",
        "human_reviewer": None, "human_review_timestamp": None,
        "human_authority": None, "human_approval": None,
    }
    for key, value in expected_gov.items():
        require(type(gov[key]) is type(value) and gov[key] == value,
                f"governance.{key}: human/operational gate was modified")
    risk = gov["risk_acceptances"]
    expected_risk_keys = {f"RA-{i:02d}" for i in range(1, 9)}
    exact_keys(risk, expected_risk_keys, "risk acceptances")
    require(all(value is None for value in risk.values()), "human risk was falsely accepted")
    limits = manifest["evidence_limitations"]
    require(type(limits) is list and len(limits) >= 5 and
            all(type(line) is str and len(line) > 15 for line in limits),
            "evidence limitations removed or malformed")
    require(any("NÃO consulta" in x for x in limits) and
            any("NÃO são autorização humana" in x for x in limits),
            "historical evidence and human decision boundary not explicit")
    return {
        "schema": "f30-offline-verification/v1",
        "result": "PASS",
        "evidence_checkout_commit": PINNED_CHECKOUT,
        "source_count": len(fingerprints),
        "sources": sorted(fingerprints, key=lambda x: x["path"]),
        "stage_count": len(STAGES),
        "historical_ci_runs_referenced": len(RUNS),
        "offline_historical_log_authentication": False,
        "operational": False,
        "approved": False,
        "human_approval": None,
        "decision": "NOT_MADE",
    }


def negative_tests(valid: dict) -> int:
    probes = [
        ("unknown schema", lambda x: x.update(schema="v2")),
        ("unknown root field", lambda x: x.update(authorized=True)),
        ("checkout mutated", lambda x: x.update(evidence_checkout_commit="0"*40)),
        ("repository changed", lambda x: x.update(repository="not-the-repo")),
        ("source missing", lambda x: x["source_files"].pop()),
        ("source duplicate", lambda x: x["source_files"].__setitem__(1, x["source_files"][0])),
        ("source path escape", lambda x: x["source_files"][0].update(path="../../fm.exe")),
        ("source blob mutated", lambda x: x["source_files"][0].update(git_blob_sha1="0"*40)),
        ("extra source property", lambda x: x["source_files"][0].update(approved=True)),
        ("stage erased", lambda x: x["stages"].pop()),
        ("stage wrong PR", lambda x: x["stages"][0].update(merged_pr=99)),
        ("stage fake count", lambda x: x["stages"][1].update(assertions=987)),
        ("stage wrong job", lambda x: x["stages"][2].update(windows_job=1)),
        ("stage fake log", lambda x: x["stages"][0].update(expected_line="CI green means approved")),
        ("stage no longer synthetic", lambda x: x["stages"][0].update(synthetic=False)),
        ("CI run missing", lambda x: x["ci_runs"].pop()),
        ("CI run duplicate", lambda x: x["ci_runs"].__setitem__(0, x["ci_runs"][1])),
        ("CI falsely passed", lambda x: x["ci_runs"][0].update(conclusion="skipped")),
        ("CI run ID replaced", lambda x: x["ci_runs"][0].update(run_id=44)),
        ("mapping changed", lambda x: x["governance"].update(mapping_unchanged_commit="0"*40)),
        ("operational activated", lambda x: x["governance"].update(operational=True)),
        ("approval activated", lambda x: x["governance"].update(approved=True)),
        ("player maps activated", lambda x: x["governance"].update(approvedPlayerMaps=1)),
        ("approval booleans wrong type", lambda x: x["governance"].update(approved=0)),
        ("B03 promoted", lambda x: x["governance"].update(B03="APPROVED")),
        ("A12 promoted", lambda x: x["governance"].update(A12="OK")),
        ("decision fabricated", lambda x: x["governance"].update(decision="APPROVED")),
        ("reviewer invented", lambda x: x["governance"].update(human_reviewer="CI")),
        ("review timestamp invented", lambda x: x["governance"].update(human_review_timestamp="2026-11-21")),
        ("human authority invented", lambda x: x["governance"].update(human_authority="PR #34")),
        ("human approval invented", lambda x: x["governance"].update(human_approval=True)),
        ("risk accepted", lambda x: x["governance"]["risk_acceptances"].update({"RA-03": "accepted"})),
        ("risk dropped", lambda x: x["governance"]["risk_acceptances"].pop("RA-07")),
        ("limitations erased", lambda x: x.update(evidence_limitations=[])),
        ("historical authenticity overstated", lambda x: x.update(evidence_limitations=["something"]*5)),
    ]
    for name, mutate in probes:
        data = copy.deepcopy(valid)
        mutate(data)
        try:
            validate(data)
        except EvidenceError:
            continue
        raise AssertionError(f"negative test bypassed: {name}")
    # Mutate the real source bytes under a temporary overlay without touching
    # checkout files, to verify that merely changing a file's contents fails.
    import tempfile
    import shutil
    with tempfile.TemporaryDirectory() as td:
        tmp = Path(td)
        for path in PINNED_SOURCES:
            target = tmp / path
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(ROOT / path, target)
        first = tmp / next(iter(PINNED_SOURCES))
        first.write_bytes(first.read_bytes() + b"\n# tampered content\n")
        try:
            validate(copy.deepcopy(valid), root=tmp)
        except EvidenceError:
            pass
        else:
            raise AssertionError("changed source bytes bypassed the manifest verifier")
    return len(probes) + 1


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--manifest", type=Path, default=MANIFEST)
    parser.add_argument("--report", type=Path)
    parser.add_argument("--self-test", action="store_true")
    args = parser.parse_args()
    data = json.loads(args.manifest.read_text(encoding="utf-8"))
    report = validate(data)
    if args.self_test:
        report["negative_cases_passed"] = negative_tests(data)
    if args.report:
        args.report.parent.mkdir(parents=True, exist_ok=True)
        args.report.write_text(
            json.dumps(report, sort_keys=True, indent=2, ensure_ascii=False) + "\n",
            encoding="utf-8",
        )
    print(f"F30.4 PASS: {report['source_count']} pinned files, "
          f"{report['stage_count']} stages, "
          f"{report['historical_ci_runs_referenced']} historical CI references, "
          f"{report.get('negative_cases_passed', 0)} negative tests; "
          "NoApprovedPlayerMaps / approved=false / operational=false / decision=NOT_MADE")


if __name__ == "__main__":
    main()
