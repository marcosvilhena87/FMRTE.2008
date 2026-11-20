"""F23.2 independent Draft 2020-12 schema contract checks; no game access."""
import hashlib
import json
import os
from pathlib import Path
from jsonschema import Draft202012Validator

folder=Path(__file__).resolve().parent
schema_path=folder/"F23ResidualRisks.schema.json"
registry_path=folder/"F23ResidualRisks.json"
schema=json.loads(schema_path.read_text(encoding="utf-8"))
Draft202012Validator.check_schema(schema)
validator=Draft202012Validator(schema)
raw=registry_path.read_bytes()
canonical=json.loads(raw)
if list(validator.iter_errors(canonical)):
    raise SystemExit("F23.2 schema rejected canonical registry")
temp=os.environ.get("RUNNER_TEMP")
if not temp:
    raise SystemExit("F23.2 RUNNER_TEMP missing")
evidence=Path(temp)/"f20-evidence"
report=json.loads((evidence/"F23ResidualRiskValidation.json").read_text(encoding="utf-8"))
cases=report["negativeCases"]
ids=[x["caseId"] for x in cases]
if len(ids)!=25 or len(set(ids))!=25 or report["negativeCasesRejected"]!=25:
    raise SystemExit("F23.2 negative inventory mismatch")
if report["registrySha256"]!=hashlib.sha256(raw).hexdigest():
    raise SystemExit("F23.2 registry digest mismatch")
files=evidence/"f23-risk-cases"
if {x.stem for x in files.glob("*.json")}!=set(ids):
    raise SystemExit("F23.2 mutation ID mismatch")
results=[]
for case in cases:
    if case["actualOutcome"]!="InvalidDataException" or case["expectedOutcome"]!="InvalidDataException" or case["result"]!="rejected":
        raise SystemExit("F23.2 C# accepted negative "+case["caseId"])
    candidate=json.loads((files/(case["caseId"]+".json")).read_text(encoding="utf-8"))
    errors=list(validator.iter_errors(candidate))
    if not errors:
        raise SystemExit("F23.2 JSON Schema accepted C# negative "+case["caseId"])
    results.append({"caseId":case["caseId"],"result":"rejected","schemaErrorCount":len(errors)})
(evidence/"F23ResidualRiskIndependentSchemaValidation.json").write_text(json.dumps({
    "schemaVersion":1,"engine":"python-jsonschema","canonicalAccepted":True,
    "schemaSha256":hashlib.sha256(schema_path.read_bytes()).hexdigest(),
    "registrySha256":hashlib.sha256(raw).hexdigest(),
    "negativeCasesRejected":len(results),"negativeCases":results,
    "approved":False,"operational":False,"humanApproval":False,"riskAccepted":False
},indent=2),encoding="utf-8")
print("OK F23.2 independent JSON Schema: 8 pending risks, 25/25 negatives rejected; no map approval")
