"""Independent offline JSON Schema validation for F23.1."""
import hashlib
import json
import os
from pathlib import Path
from jsonschema import Draft202012Validator

folder=Path(__file__).resolve().parent
schema_path=folder/"F23B03ProvenanceGaps.schema.json"
manifest_path=folder/"F23B03ProvenanceGaps.json"
schema=json.loads(schema_path.read_text(encoding="utf-8"))
Draft202012Validator.check_schema(schema)
validator=Draft202012Validator(schema)
raw=manifest_path.read_bytes()
canonical=json.loads(raw)
if list(validator.iter_errors(canonical)):
    raise SystemExit("F23 schema rejected canonical provenance gaps")
temp=os.environ.get("RUNNER_TEMP")
if not temp:
    raise SystemExit("F23 RUNNER_TEMP missing")
evidence=Path(temp)/"f20-evidence"
report=json.loads((evidence/"F23B03ProvenanceValidation.json").read_text(encoding="utf-8"))
cases=report["negativeCases"]
ids=[case["caseId"] for case in cases]
if len(ids)!=22 or len(set(ids))!=22 or report["negativeMutationsRejected"]!=22:
    raise SystemExit("F23 wrong C# negative count")
if report["manifestSha256"]!=hashlib.sha256(raw).hexdigest():
    raise SystemExit("F23 canonical manifest hash mismatch")
mutation_dir=evidence/"f23-schema-cases"
if {p.stem for p in mutation_dir.glob("*.json")}!=set(ids):
    raise SystemExit("F23 mutation inventory mismatch")
results=[]
for case in cases:
    if case["result"]!="rejected" or case["actualOutcome"]!="InvalidDataException":
        raise SystemExit("F23 C# accepted "+case["caseId"])
    altered=json.loads((mutation_dir/(case["caseId"]+".json")).read_text(encoding="utf-8"))
    errors=list(validator.iter_errors(altered))
    if not errors:
        raise SystemExit("F23 JSON Schema accepted "+case["caseId"])
    results.append({"caseId":case["caseId"],"result":"rejected","schemaErrorCount":len(errors)})
(evidence/"F23IndependentSchemaValidation.json").write_text(
    json.dumps({"schemaVersion":1,"engine":"python-jsonschema","canonicalAccepted":True,
      "schemaSha256":hashlib.sha256(schema_path.read_bytes()).hexdigest(),
      "manifestSha256":hashlib.sha256(raw).hexdigest(),"negativeCasesRejected":22,
      "negativeCases":results,"approved":False,"operational":False,
      "historicalObservationAttested":False},indent=2),encoding="utf-8")
print("OK F23 independent JSON Schema: 7 pending, canonical valid, 22/22 negatives rejected; no approval")
