"""F23.4: offline cross-stage integrity + structured negative evidence; no approvals."""
from __future__ import annotations
import copy
import hashlib
import json
import os
import re
from pathlib import Path
from jsonschema import Draft202012Validator

ROOT = Path(__file__).resolve().parents[2]
FOLDER = Path(__file__).resolve().parent
EVIDENCE = Path(os.environ["RUNNER_TEMP"]) / "f20-evidence"
MANIFEST_PATH = FOLDER / "F23ConsolidatedEvidence.json"
SCHEMA_PATH = FOLDER / "F23ConsolidatedEvidence.schema.json"
MANIFEST_BYTES = MANIFEST_PATH.read_bytes()
SCHEMA_BYTES = SCHEMA_PATH.read_bytes()
MANIFEST = json.loads(MANIFEST_BYTES)
SCHEMA = json.loads(SCHEMA_BYTES)
Draft202012Validator.check_schema(SCHEMA)
VALIDATOR = Draft202012Validator(SCHEMA)

class IntegrityViolation(ValueError):
    pass

def require(condition, reason):
    if not condition:
        raise IntegrityViolation(reason)

def digest(content):
    return hashlib.sha256(content).hexdigest()

def load_sources(m):
    result = {}
    for source in m["sources"]:
        path = ROOT / source["path"]
        require(path.is_file(), "missing checkout file " + source["stage"])
        raw = path.read_bytes()
        require(digest(raw) == source["sha256"], "sha256 drift: " + source["stage"])
        result[source["stage"]] = json.loads(raw)
    return result

def validate(m, src, *, schema=True):
    if schema:
        require(not list(VALIDATOR.iter_errors(m)), "closed schema mismatch")
    require(set(src) == {"F21","F22","F23.1","F23.2","F23.3"}, "source set mismatch")
    f21, f22, b03, rr, human = (src[k] for k in ("F21","F22","F23.1","F23.2","F23.3"))
    require(f21["sourceRepo"] == m["sourceRepository"] and
            f21["sourceCommit"] == m["sourceCommit"], "F21 source identity")
    require(f22["mappingSource"] == {"repository":m["sourceRepository"],"commit":m["sourceCommit"]}, "F22 source identity")
    require(b03["source"]["repository"] == m["sourceRepository"] and
            b03["source"]["commit"] == m["sourceCommit"], "B03 source identity")
    require(rr["source"]["mappingRepo"] == m["sourceRepository"] and
            rr["source"]["mappingCommit"] == m["sourceCommit"], "risk source identity")
    require(human["source"]["mappingRepository"] == m["sourceRepository"] and
            human["source"]["mappingCommit"] == m["sourceCommit"], "human source identity")
    ids = m["traceability"]
    require([x["observationId"] for x in f21["observations"]] == ids["observationIds"], "F21 observation IDs")
    require([x["observationId"] for x in b03["observations"]] == ids["observationIds"], "B03 observation IDs")
    missing_fields = ("executor","observedAt","environment","collectionMethod","externalCorroboration")
    for old, new in zip(f21["observations"], b03["observations"]):
        for key in ("observationId","sourcePath","sourceBlobSha","reportedResult","sourceEvidenceStatus","provenanceStatus"):
            require(old[key] == new[key], "F21/B03 mismatch " + key)
        require(old["provenanceStatus"] == "PENDING-DECLARED" and
                all(old[k] is None and new[k] is None for k in missing_fields), "historical provenance promoted")
    require([g["gateId"] for g in f22["gates"]] == ids["gateIds"], "F22 gate IDs")
    require(sorted({c for g in f22["gates"] for c in g["criteria"]}) == sorted(ids["criteriaIds"]), "F22 criteria")
    require([x["riskId"] for x in rr["risks"]] == ids["riskIds"], "risk IDs")
    require([x["riskId"] for x in human["riskDecisions"]] == ids["riskIds"], "human risk IDs")
    for risk, human_risk in zip(rr["risks"], human["riskDecisions"]):
        gate = next((x for x in f22["gates"] if risk["riskId"] in x["risks"]), None)
        require(gate is not None and risk["gateId"] == gate["gateId"], "risk/gate mismatch")
        require(risk["criteria"] == gate["criteria"], "risk/criteria mismatch")
        require(risk["observationIds"] == [e["observationId"] for e in gate["evidenceReferences"]], "risk/observations mismatch")
        for evidence in gate["evidenceReferences"]:
            observation = next((x for x in b03["observations"] if x["observationId"] == evidence["observationId"]), None)
            require(observation is not None and observation["sourcePath"] == evidence["sourcePath"] and
                    observation["sourceBlobSha"] == evidence["sourceBlobSha"], "evidence path/blob mismatch")
        require(risk["documentaryStatus"] == "PENDING" and risk["riskAccepted"] is False and
                risk["riskAcceptanceReference"] is None and human_risk["accepted"] is False and
                human_risk["acceptanceReference"] is None, "risk accepted")
    g5 = f22["gates"][4]
    require(g5["gateId"] == ids["decisionGate"] == human["gate"]["gateId"] and
            g5["criteria"] == [ids["decisionCriterion"]] and
            human["gate"]["criterion"] == ids["decisionCriterion"], "G5/A12 linkage")
    require(g5["approvalDecision"] == "NOT_APPROVED" and
            human["gate"]["humanSignoff"] is False and
            human["gate"]["status"] == "GATE-BLOQUEADO", "G5 approval violation")
    require(human["humanReview"] == {
        "reviewer":None, "reviewedAt":None,"authorityReference":None,
        "decision":"NOT_MADE", "decisionReference":None}, "fabricated human decision")
    require(human["evidence"]["b03"] == "PENDING-DECLARED" and
            all(human["evidence"][k] is False for k in
                ("historicalProvenanceAttested","ciIsHumanApproval","prReadyIsHumanApproval")), "false attestation")
    require(human["outcome"] == {"approved":False,"operational":False,"approvedPlayerMaps":0}, "approved human outcome")
    require(m["approval"] == {"approved":False,"operational":False,"humanApproval":None,
        "gateA12":"GATE-BLOQUEADO","b03":"PENDING-DECLARED","decision":"NOT_MADE",
        "acceptedRiskCount":0,"approvedMapCount":0}, "consolidated approval altered")
    require(m["provenance"] == {
        "observationsIndependentlyAttested":False,"ciIsHistoricalEvidence":False,
        "ciIsHumanApproval":False,"prReadyIsHumanApproval":False}, "consolidated provenance altered")
    for decision in (f22["decision"], b03["decision"], rr["decision"]):
        require(decision["approved"] is False and decision["operational"] is False, "source approved")
        require(decision["b03"] == "PENDING-DECLARED" and decision["gateA12"] == "GATE-BLOQUEADO", "source gate promoted")
    require(rr["decision"]["decisionStatus"] == "NOT_MADE", "risk decision status")
    require(all(x is False for x in (f21["approval"]["approved"],f21["approval"]["operational"])), "F21 approved")

def validate_existing_reports(m):
    stages = (
      ("F22","F22ApprovalGateValidation.json","matrixSha256","rejectedMutations",32),
      ("F23.1","F23B03ProvenanceValidation.json","manifestSha256","negativeMutationsRejected",22),
      ("F23.2","F23ResidualRiskValidation.json","registrySha256","negativeCasesRejected",25),
      ("F23.3","F23HumanDecisionValidation.json","decisionSha256","rejectedMutations",28)
    )
    records = []
    for stage,name,hash_key,count_key,total in stages:
        data=json.loads((EVIDENCE/name).read_text(encoding="utf-8"))
        entry=next(x for x in m["sources"] if x["stage"]==stage)
        cases=data["negativeCases"]
        ids=[x["caseId"] for x in cases]
        require(data[hash_key]==entry["sha256"], stage+" report hash")
        require(data[count_key]==len(cases)==total and len(set(ids))==total, stage+" report count/uniqueness")
        require(all(x["result"]=="rejected" and x["expectedOutcome"]==x["actualOutcome"]=="InvalidDataException"
                    for x in cases),stage+" report cases")
        require(data["approved"] is False and data["operational"] is False, stage+" CI approval")
        records.append({"stage":stage,"report":name,"sourceSha256":entry["sha256"],
                        "negativeCasesRejected":total,"uniqueCaseIds":total,"result":"verified"})
    # Verify F21 as strictly as the later stages: no default count and no missing IDs.
    f21r=json.loads((EVIDENCE/"F21B03Validation.json").read_text(encoding="utf-8"))
    f21entry=next(x for x in m["sources"] if x["stage"]=="F21")
    f21cases=f21r["negativeCases"]
    f21ids=[x["caseId"] for x in f21cases]
    require(f21r["manifestSha256"]==f21entry["sha256"], "F21 report hash")
    require(f21r["negativeMutationsRejected"]==len(f21cases)==len(set(f21ids))==29,
            "F21 negative completeness")
    require(all(c["result"]=="rejected" and
        c["actualOutcome"]==c["expectedOutcome"]=="InvalidDataException" for c in f21cases),
        "F21 negative results")
    require(f21r["approved"] is False and f21r["operational"] is False and
        f21r["historicalObservationAttested"] is False, "F21 historical approval")
    records.insert(0,{"stage":"F21","report":"F21B03Validation.json",
        "sourceSha256":f21entry["sha256"],"negativeCasesRejected":29,
        "uniqueCaseIds":29,"result":"verified"})
    independent=(
      ("F22","F22IndependentSchemaValidation.json","matrixSha256",32),
      ("F23.1","F23IndependentSchemaValidation.json","manifestSha256",22),
      ("F23.2","F23ResidualRiskIndependentSchemaValidation.json","registrySha256",25),
      ("F23.3","F23HumanDecisionIndependentSchemaValidation.json","decisionSha256",28)
    )
    for stage,name,sha_key,total in independent:
        result=json.loads((EVIDENCE/name).read_text(encoding="utf-8"))
        entry=next(x for x in m["sources"] if x["stage"]==stage)
        previous_report=next(x for x in stages if x[0]==stage)[1]
        original=json.loads((EVIDENCE/previous_report).read_text(encoding="utf-8"))
        their_ids=[x["caseId"] for x in result["negativeCases"]]
        our_ids=[x["caseId"] for x in original["negativeCases"]]
        require(result["canonicalAccepted"] is True and
                result["negativeCasesRejected"]==len(their_ids)==len(set(their_ids))==total,
                stage+" independent schema count")
        require(result[sha_key]==entry["sha256"] and
                set(their_ids)==set(our_ids),stage+" independent schema identity")
        require(all(x["result"]=="rejected" and x["schemaErrorCount"]>0
                    for x in result["negativeCases"]),stage+" independent schema outcomes")
        records.append({"stage":stage+"-schema","report":name,
           "sourceSha256":entry["sha256"],"negativeCasesRejected":total,
           "uniqueCaseIds":total,"result":"verified"})
    ex=json.loads((EVIDENCE/"F20ExecutionEvidence.json").read_text(encoding="utf-8"))
    require(ex["workflowRunId"]==os.environ["GITHUB_RUN_ID"] and
            ex["workflowAttempt"]==os.environ["GITHUB_RUN_ATTEMPT"] and
            ex["eventName"]==os.environ["GITHUB_EVENT_NAME"], "F20 run/event mismatch")
    require(ex["checkoutSha"]==os.environ["GITHUB_SHA"] and
            re.fullmatch("[0-9a-f]{40}",ex["prHeadSha"]) is not None, "checkout/PR head mismatch")
    require(ex["mappingCommit"]==m["sourceCommit"], "execution research commit mismatch")
    return records, {"runId":ex["workflowRunId"],"attempt":ex["workflowAttempt"],
                     "event":ex["eventName"],"checkoutSha":ex["checkoutSha"],
                     "prHeadSha":ex["prHeadSha"],"checkoutAndPrHeadDistinct":ex["checkoutSha"]!=ex["prHeadSha"],
                     "mappingCommit":ex["mappingCommit"],"approvalFromCI":False}

def mutate_copy(base, path, value):
    candidate=copy.deepcopy(base)
    node=candidate
    for key in path[:-1]:node=node[key]
    node[path[-1]]=value
    return candidate

def run_negative(case_id, group, altered, src):
    try:
        validate(altered,src)
    except IntegrityViolation as exc:
        return {"caseId":case_id,"group":group,"expectedOutcome":"IntegrityViolation",
                "actualOutcome":"IntegrityViolation","result":"rejected","reason":str(exc)}
    raise RuntimeError("F23.4 accepted mutation: "+case_id)

def main():
    src=load_sources(MANIFEST)
    validate(MANIFEST,src)
    previous,execution=validate_existing_reports(MANIFEST)
    cases=[]
    mutations=[
      ("approved",("approval","approved"),True),
      ("operational",("approval","operational"),True),
      ("human-signoff",("approval","humanApproval"),"CI"),
      ("unblock-A12",("approval","gateA12"),"APPROVED"),
      ("close-B03",("approval","b03"),"VERIFIED"),
      ("decision-made",("approval","decision"),"APPROVED"),
      ("accept-risk",("approval","acceptedRiskCount"),1),
      ("approve-map",("approval","approvedMapCount"),1),
      ("ci-as-human",("provenance","ciIsHumanApproval"),True),
      ("ci-as-history",("provenance","ciIsHistoricalEvidence"),True),
      ("ready-as-human",("provenance","prReadyIsHumanApproval"),True),
      ("historical-attestation",("provenance","observationsIndependentlyAttested"),True),
      ("wrong-source-hash",("sources",1,"sha256"),"0"*64),
      ("wrong-source-path",("sources",2,"path"),"unknown.json"),
      ("wrong-source-stage",("sources",3,"stage"),"F22"),
      ("wrong-risk-id",("traceability","riskIds",0),"RA-08"),
      ("wrong-observation-id",("traceability","observationIds",0),"R07"),
      ("wrong-G5",("traceability","decisionGate"),"G4"),
      ("wrong-A12",("traceability","decisionCriterion"),"A11")
    ]
    for name,path,value in mutations:
        cases.append(run_negative(name,"manifest",mutate_copy(MANIFEST,path,value),src))
    source_changes=[
      ("f21-fabricated-executor","F21",("observations",0,"executor"),"CI"),
      ("f21-wrong-source-blob","F21",("observations",0,"sourceBlobSha"),"f"*40),
      ("f22-promote-gate","F22",("decision","gateA12"),"APPROVED"),
      ("f22-change-criterion","F22",("gates",4,"criteria",0),"A11"),
      ("f23-b03-claim-audit","F23.1",("observations",0,"provenanceStatus"),"VERIFIED"),
      ("f23-b03-wrong-source","F23.1",("observations",1,"sourcePath"),"false.md"),
      ("f23-risk-accepted","F23.2",("risks",0,"riskAccepted"),True),
      ("f23-risk-wrong-gate","F23.2",("risks",0,"gateId"),"G4"),
      ("f23-human-reviewer","F23.3",("humanReview","reviewer"),"CI"),
      ("f23-human-date","F23.3",("humanReview","reviewedAt"),"2026-11-20"),
      ("f23-human-accepted-risk","F23.3",("riskDecisions",0,"accepted"),True),
      ("f23-human-CI-approval","F23.3",("evidence","ciIsHumanApproval"),True)
    ]
    for name,stage,path,value in source_changes:
        altered=copy.deepcopy(src);altered[stage]=mutate_copy(src[stage],path,value)
        cases.append(run_negative(name,"linked-source",MANIFEST,altered))
    require(len(cases)==31 and len({c["caseId"] for c in cases})==31,"F23.4 negative case count")
    report={"schemaVersion":1,"kind":"f23-consolidated-offline-ci-evidence",
       "manifestSha256":digest(MANIFEST_BYTES),"schemaSha256":digest(SCHEMA_BYTES),
       "sources":previous,"execution":execution,"sourceChecksPassed":5,
       "schemaDraft":"2020-12","canonicalAccepted":True,
       "negativeCasesRejected":len(cases),"negativeCases":cases,
       "humanApproval":False,"approved":False,"operational":False,
       "gateA12":"GATE-BLOQUEADO","b03":"PENDING-DECLARED","decision":"NOT_MADE"}
    EVIDENCE.mkdir(parents=True,exist_ok=True)
    (EVIDENCE/"F23ConsolidatedEvidence.json").write_bytes(MANIFEST_BYTES)
    (EVIDENCE/"F23ConsolidatedEvidence.schema.json").write_bytes(SCHEMA_BYTES)
    (EVIDENCE/"F23ConsolidatedValidation.json").write_text(
        json.dumps(report,indent=2),encoding="utf-8")
    print("OK F23.4: 5 source SHA-256 commitments, F21/F22/F23.1/F23.2/F23.3 linked, 31/31 negative cases rejected; no approval")
    print("OK F23.4 CI: run="+execution["runId"]+" event="+execution["event"]+
          " checkout="+execution["checkoutSha"]+" prHead="+execution["prHeadSha"]+
          " manifestSHA256="+report["manifestSha256"])

if __name__=="__main__":
    main()
