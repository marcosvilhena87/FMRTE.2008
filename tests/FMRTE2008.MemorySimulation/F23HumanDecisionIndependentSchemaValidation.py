"""F23.3 independent Draft 2020-12 schema runner, documentary only."""
import hashlib
import json
import os
from pathlib import Path
from jsonschema import Draft202012Validator

folder=Path(__file__).resolve().parent
schema_path=folder/"F23HumanDecisionNotMade.schema.json"
decision_path=folder/"F23HumanDecisionNotMade.json"
schema=json.loads(schema_path.read_text(encoding="utf-8"))
Draft202012Validator.check_schema(schema)
validator=Draft202012Validator(schema)
raw=decision_path.read_bytes()
canonical=json.loads(raw)
if list(validator.iter_errors(canonical)):
    raise SystemExit("F23.3 canonical decision rejected")
temp=os.environ.get("RUNNER_TEMP")
if not temp:
    raise SystemExit("F23.3 RUNNER_TEMP missing")
evidence=Path(temp)/"f20-evidence"
report=json.loads((evidence/"F23HumanDecisionValidation.json").read_text(encoding="utf-8"))
cases=report["negativeCases"]
ids=[c["caseId"] for c in cases]
if len(ids)!=28 or len(set(ids))!=28 or report["rejectedMutations"]!=28:
    raise SystemExit("F23.3 negative case count mismatch")
if report["decisionSha256"]!=hashlib.sha256(raw).hexdigest():
    raise SystemExit("F23.3 decision digest mismatch")
dir=evidence/"f23-human-cases"
if {p.stem for p in dir.glob("*.json")}!=set(ids):
    raise SystemExit("F23.3 mutation documents mismatch")
out=[]
for case in cases:
    if case["expectedOutcome"]!="InvalidDataException" or case["actualOutcome"]!="InvalidDataException" or case["result"]!="rejected":
        raise SystemExit("F23.3 C# failed mutation "+case["caseId"])
    negative=json.loads((dir/(case["caseId"]+".json")).read_text(encoding="utf-8"))
    errors=list(validator.iter_errors(negative))
    if not errors:
        raise SystemExit("F23.3 schema accepted mutation "+case["caseId"])
    out.append({"caseId":case["caseId"],"result":"rejected","schemaErrorCount":len(errors)})
(evidence/"F23HumanDecisionIndependentSchemaValidation.json").write_text(json.dumps({
    "schemaVersion":1,"engine":"python-jsonschema","canonicalAccepted":True,
    "schemaSha256":hashlib.sha256(schema_path.read_bytes()).hexdigest(),
    "decisionSha256":hashlib.sha256(raw).hexdigest(),
    "negativeCasesRejected":28,"negativeCases":out,"decision":"NOT_MADE",
    "approved":False,"operational":False,"humanApproval":False,"riskAccepted":False
},indent=2),encoding="utf-8")
print("OK F23.3 independent JSON Schema: NOT_MADE, 28/28 negatives rejected; no human or operational approval")
