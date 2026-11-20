"""F22 independent Draft 2020-12 JSON Schema contract check."""
import hashlib
import json
import os
from pathlib import Path
from jsonschema import Draft202012Validator

root = Path(__file__).resolve().parents[2]
folder = root / "tests" / "FMRTE2008.MemorySimulation"
schema_path = folder / "F22ApprovalGates.schema.json"
matrix_path = folder / "F22ApprovalGates.json"
schema = json.loads(schema_path.read_text(encoding="utf-8"))
Draft202012Validator.check_schema(schema)
validator = Draft202012Validator(schema)
matrix_bytes = matrix_path.read_bytes()
matrix = json.loads(matrix_bytes)
if list(validator.iter_errors(matrix)):
    raise SystemExit("F22 JSON Schema rejected the canonical matrix")
temp = os.environ.get("RUNNER_TEMP")
if not temp:
    raise SystemExit("F22 RUNNER_TEMP missing")
evidence = Path(temp) / "f20-evidence"
report = json.loads((evidence / "F22ApprovalGateValidation.json").read_text(encoding="utf-8"))
if report["matrixSha256"] != hashlib.sha256(matrix_bytes).hexdigest():
    raise SystemExit("F22 matrix digest differs from C# evidence")
cases = report["negativeCases"]
ids = [c["caseId"] for c in cases]
if len(ids) != 32 or len(set(ids)) != 32 or report["rejectedMutations"] != 32:
    raise SystemExit("F22 mutation count/uniqueness failed")
case_dir = evidence / "f22-schema-cases"
disk_ids = {p.stem for p in case_dir.glob("*.json")}
if disk_ids != set(ids):
    raise SystemExit("F22 C# and JSON Schema tested different mutation IDs")
results = []
for case in cases:
    if case["expectedOutcome"] != "InvalidDataException" or case["actualOutcome"] != "InvalidDataException" or case["result"] != "rejected":
        raise SystemExit("F22 C# failed to reject " + case["caseId"])
    candidate = json.loads((case_dir / (case["caseId"] + ".json")).read_text(encoding="utf-8"))
    errors = list(validator.iter_errors(candidate))
    if not errors:
        raise SystemExit("F22 JSON Schema accepted C# negative " + case["caseId"])
    results.append({"caseId":case["caseId"],"expected":"rejected",
                    "actual":"rejected","schemaErrorCount":len(errors)})
output = {"schemaVersion":1,"schemaDraft":"2020-12","engine":"python-jsonschema",
          "schemaSha256":hashlib.sha256(schema_path.read_bytes()).hexdigest(),
          "matrixSha256":hashlib.sha256(matrix_bytes).hexdigest(),
          "f21Sha256":report["f21Sha256"],
          "canonicalAccepted":True,"negativeCases":results,
          "negativeCasesRejected":len(results),"approved":False,
          "operational":False,"humanApproval":False}
(evidence / "F22IndependentSchemaValidation.json").write_text(
    json.dumps(output,indent=2),encoding="utf-8")
print("OK F22 independent JSON Schema: positive accepted, 32/32 negatives rejected; no map approval")
