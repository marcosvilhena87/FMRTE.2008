#!/usr/bin/env python3
"""F32: validate a review-preparation dossier without making a human decision."""
from __future__ import annotations
import argparse
import copy
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
F31_PATH = ROOT / "evidence/f31/minimal_map_readiness.json"
F32_PATH = ROOT / "evidence/f32/human_review_dossier.json"
F31_COMMIT = "486fabe2aff423fb5544110ec992ba6b1707b317"
F31_BLOB = "adb0dc1e83d09337afc04e425a9ca73d52c8d8dc"
MAPPING_COMMIT = "df5be0493a1ac7be57208ff0e91c2d7193dfec73"
CRITERIA = [f"A{i:02d}" for i in range(1,13)]
RISKS = [f"RA-{i:02d}" for i in range(1,9)]
GATES = [f"G{i}" for i in range(1,6)]
class Invalid(ValueError): pass

def need(ok,why):
    if not ok: raise Invalid(why)

def closed(obj,ks,where):
    need(type(obj) is dict and set(obj)==set(ks),f"{where}: keys")

def pairs_no_duplicate(pairs):
    d={}
    for key,value in pairs:
        need(key not in d, "JSON duplicate key")
        d[key]=value
    return d

def parse(raw):
    return json.loads(raw,object_pairs_hook=pairs_no_duplicate,
        parse_constant=lambda _: (_ for _ in ()).throw(Invalid("non-finite JSON")))

def blob_sha(raw):
    return hashlib.sha1(b"blob "+str(len(raw)).encode("ascii")+b"\x00"+raw).hexdigest()

def source(raw_override=None):
    raw=F31_PATH.read_bytes() if raw_override is None else raw_override
    need(blob_sha(raw)==F31_BLOB,"F31 immutable source blob")
    return parse(raw)

def validate(d,upstream):
    closed(d,{"schema","scope","baseline","sourceInventory","primaryEvidence","criteria","risks","gates","reviewProtocol",
        "limitations","wpf","governance","nextAction"},"root")
    need(d["schema"]=="f32-human-review-preparation/v2"
         and d["scope"]=="OFFLINE_DOCUMENTARY_PREPARATION_ONLY","scope")
    b=d["baseline"]
    closed(b,{"implementationCommit","mappingCommit","f31ManifestPath","f31ManifestBlob",
              "build","referenceKind"},"baseline")
    need(b=={"implementationCommit":F31_COMMIT,"mappingCommit":MAPPING_COMMIT,
         "f31ManifestPath":"evidence/f31/minimal_map_readiness.json",
         "f31ManifestBlob":F31_BLOB,"build":"FM2008 8.0.2v1",
         "referenceKind":"VERSIONED_DOCUMENTS_NOT_INDEPENDENT_OBSERVATIONS"},"baseline identity")
    # R01: source inventory is inherited verbatim; these are documentary references, not primary observations.
    inventory=d["sourceInventory"]
    need(type(inventory) is list and len(inventory)==5,"source inventory count")
    upstream_sources={x["id"]:x for x in upstream["sources"]}
    need(len(upstream_sources)==5 and len(upstream["sources"])==5,"upstream source inventory")
    source_seen=set()
    for s in inventory:
        closed(s,{"id","path","gitBlobSha1","evidenceKind","independentObservationVerified"},"source record")
        sid=s["id"]
        need(type(sid) is str and sid in upstream_sources and sid not in source_seen,"source identity")
        source_seen.add(sid)
        original=upstream_sources[sid]
        need(s["path"]==original["path"] and s["gitBlobSha1"]==original["gitBlobSha1"],"source pin "+sid)
        need(s["evidenceKind"]=="VERSIONED_DOCUMENTARY_REFERENCE" and
             s["independentObservationVerified"] is False,"source cannot authenticate observations "+sid)
    slots=d["primaryEvidence"]
    need(type(slots) is list and len(slots)==12,"evidence slot count")
    slot_seen=set()
    known={c["id"]:c for c in upstream["criteria"]}
    for slot in slots:
        closed(slot,{"criterionId","sourceIds","riskIds","documentaryState","receiptStatus","evidenceType",
                     "artifactId","sha256","collectionContext","executor","observedAt",
                     "independentlyAuthenticated","reviewStatus"},"evidence slot")
        cid=slot["criterionId"]
        need(type(cid) is str and cid in known and cid not in slot_seen,"evidence slot identity")
        slot_seen.add(cid)
        upstream_item=known[cid]
        need(slot["sourceIds"]==upstream_item["sourceIds"] and slot["riskIds"]==upstream_item["risks"]
             and slot["documentaryState"]==upstream_item["status"],"evidence slot provenance "+cid)
        need(slot["receiptStatus"]=="NOT_PROVIDED" and
             slot["evidenceType"]=="UNVERIFIED_PRIMARY_EVIDENCE_SLOT" and
             all(slot[key] is None for key in ("artifactId","sha256","collectionContext","executor","observedAt"))
             and slot["independentlyAuthenticated"] is False and slot["reviewStatus"]=="PENDING",
             "evidence slot cannot promote "+cid)
    need(slot_seen==set(CRITERIA),"evidence coverage")
    cs=d["criteria"]; need(type(cs) is list and len(cs)==12,"criterion count")
    upstream_cs={c["id"]:c for c in upstream["criteria"]}
    need(set(upstream_cs)==set(CRITERIA),"upstream criteria")
    seen=set()
    for c in cs:
        closed(c,{"id","evidenceState","sourceIds","riskIds","reviewStatus",
                  "reviewerFinding","reviewedAt","blockingGap"},"criterion")
        cid=c["id"]
        need(type(cid) is str and cid in CRITERIA and cid not in seen,"criterion ID")
        seen.add(cid)
        src=upstream_cs[cid]
        need(c["evidenceState"]==src["status"] and type(c["evidenceState"]) is str,"criterion evidence "+cid)
        need(c["sourceIds"]==src["sourceIds"] and type(c["sourceIds"]) is list,"criterion sources "+cid)
        need(c["riskIds"]==src["risks"] and type(c["riskIds"]) is list,"criterion risks "+cid)
        need(c["blockingGap"]==src["gap"] and type(c["blockingGap"]) is str,"criterion gap "+cid)
        need(c["reviewStatus"]=="PENDING_INDEPENDENT_REVIEW" and
            c["reviewerFinding"] is None and c["reviewedAt"] is None,"criterion reviewer "+cid)
    rs=d["risks"]; need(type(rs) is list and len(rs)==8,"risk count")
    src_risks={r["id"]:r for r in upstream["risks"]}
    need(set(src_risks)==set(RISKS),"upstream risks")
    seen=set()
    for r in rs:
        closed(r,{"id","sourceIds","description","disposition","accepted",
                  "acceptedBy","acceptedAt","mitigationReview"},"risk")
        rid=r["id"]
        need(type(rid) is str and rid in RISKS and rid not in seen,"risk ID")
        seen.add(rid)
        need(r["sourceIds"]==src_risks[rid]["sourceIds"] and
            r["description"]==src_risks[rid]["description"],"risk provenance "+rid)
        need(r["disposition"]=="NOT_REVIEWED" and r["accepted"] is False and
            r["acceptedBy"] is None and r["acceptedAt"] is None and
            r["mitigationReview"] is None,"risk acceptance "+rid)
    gs=d["gates"]; need(type(gs) is list and len(gs)==5,"gate count")
    expected=[["A01","A02","A11"],["A03","A04","A10"],["A05","A06","A07","A08"],
              ["A09"],["A12"]]
    expected_risk_links=[
        ["RA-01","RA-06"],["RA-02","RA-03","RA-05"],
        ["RA-02","RA-03","RA-04","RA-06"],["RA-07","RA-08"],RISKS]
    closure_rules=[
    "Revisor humano deve examinar identidade da build, PID e base por instância com fontes independentes; sem fechamento por CI.",
    "Revisor humano deve contrastar IDs e nomes com fontes independentes, incluindo nome comum e representatividade; sem extrapolar amostras.",
    "Revisor humano deve avaliar limites de consistência, cancelamento e reinício sem alegar atomicidade real com base em simulação.",
    "Revisor humano deve confirmar escopo somente leitura e não utilização de offsets legados, incluindo RA-07 e RA-08; sem acesso real pelo F32.",
    "Autoridade humana identificada deve registrar avaliação dos oito riscos e decisão formal separada; F32 não contém decisão."
]
    for i,g in enumerate(gs):
        closed(g,{"id","label","criteriaIds","riskIds","closureRequirement","state","reviewer","completedAt"},"gate")
        need(g["id"]==GATES[i] and type(g["label"]) is str and len(g["label"])>15
            and g["criteriaIds"]==expected[i],"gate mapping "+GATES[i])
        need(g["riskIds"]==expected_risk_links[i],"gate risk links "+GATES[i])
        need(g["closureRequirement"]==closure_rules[i],"gate closure "+GATES[i])
        need(g["state"]=="PENDING" and g["reviewer"] is None
            and g["completedAt"] is None,"gate decision "+GATES[i])
    review=d["reviewProtocol"]
    closed(review,{"identity","authority","reviewDate","reviewedEvidenceIds",
       "independentAssessment","residualRiskDecision","decision","approval","signature"},"review")
    need(all(review[x] is None for x in ("identity","authority","reviewDate",
         "independentAssessment","residualRiskDecision","approval","signature"))
         and review["decision"]=="NOT_MADE" and review["reviewedEvidenceIds"]==[],
         "human decision")
    lim=d["limitations"]
    need(type(lim) is list and len(lim)==6 and all(type(x) is str and len(x)>60 for x in lim),
         "limitations count")
    # The limitation contract is pinned, not merely a count of arbitrary statements.
    must=("não equivalem a verificação independente","não conferem autoridade humana",
          "não observadas","nenhum dos 229 offsets","offline e sintética",
          "não autenticam logs")
    need(all(phrase in lim[i] for i,phrase in enumerate(must)),"limitations content")
    w=d["wpf"]
    need(w=={"state":"OFFLINE_ONLY","dataSource":"SYNTHETIC",
            "realAdapterEnabled":False,"writeEnabled":False,"approvedMapCount":0},
         "WPF blocked")
    gov=d["governance"]
    expected_g={"approvalState":"NoApprovedPlayerMaps","approved":False,"operational":False,
       "approvedPlayerMaps":0,"B03":"PENDING-DECLARED","A12":"GATE-BLOQUEADO",
       "decision":"NOT_MADE","humanReviewer":None,"humanReviewTimestamp":None,
       "humanAuthority":None,"humanApproval":None,
       "riskAcceptances":{r:None for r in RISKS}}
    need(type(gov) is dict and set(gov)==set(expected_g),"governance schema")
    need(all(type(gov[k]) is type(v) and gov[k]==v for k,v in expected_g.items()),
         "governance approval gate")
    need(d["nextAction"]=="HUMAN_EVIDENCE_REVIEW_NOT_STARTED","next action")
    return {"result":"PASS","criteria":len(cs),"risks":len(rs),"gates":len(gs),
            "sourceReferences":len(inventory),"primaryEvidenceSlots":len(slots),
            "humanDecision":"NOT_MADE","approved":False,"operational":False,
            "f31SourceBlob":F31_BLOB,"mappingCommit":MAPPING_COMMIT,
            "historicEvidenceIndependentlyAuthenticated":False}

def negative_tests(doc,base):
    out=[]
    def reject(name,fn,reason):
        changed=copy.deepcopy(doc); fn(changed)
        try: validate(changed,base)
        except Invalid as e:
            need(reason in str(e),f"{name}: incorrect rejection reason: {e}")
        else: raise AssertionError(f"{name}: mutation accepted")
        out.append({"id":name,"result":"REJECTED","expectedReason":reason})
    for i in range(12):
        cid=CRITERIA[i]
        reject("review-criterion-"+cid,lambda x,i=i:x["criteria"][i].update(reviewStatus="APPROVED"),
               "criterion reviewer "+cid)
    for i in range(8):
        rid=RISKS[i]
        reject("accept-risk-"+rid,lambda x,i=i:x["risks"][i].update(accepted=True),
               "risk acceptance "+rid)
    reject("source-criterion",lambda x:x["criteria"][3].update(sourceIds=["MATRIX"]),
           "criterion sources A04")
    reject("risk-link",lambda x:x["criteria"][0].update(riskIds=["RA-08"]),
           "criterion risks A01")
    reject("evidence-status",lambda x:x["criteria"][0].update(evidenceState="SAMPLED"),
           "criterion evidence A01")
    reject("gap-mutation",lambda x:x["criteria"][0].update(blockingGap="resolved"),
           "criterion gap A01")
    reject("risk-provenance",lambda x:x["risks"][0].update(description="changed"),
           "risk provenance RA-01")
    reject("mapping-commit",lambda x:x["baseline"].update(mappingCommit="0"*40),
           "baseline identity")
    reject("f31-blob",lambda x:x["baseline"].update(f31ManifestBlob="0"*40),
           "baseline identity")
    reject("gate-5",lambda x:x["gates"][4].update(state="COMPLETE"),
           "gate decision G5")
    reject("fake-reviewer",lambda x:x["reviewProtocol"].update(identity="CI"),
           "human decision")
    reject("fake-authority",lambda x:x["reviewProtocol"].update(authority="maintainer"),
           "human decision")
    reject("fake-date",lambda x:x["reviewProtocol"].update(reviewDate="2026-11-21"),
           "human decision")
    reject("fake-evidence-review",lambda x:x["reviewProtocol"].update(reviewedEvidenceIds=["MATRIX"]),
           "human decision")
    reject("fake-approval",lambda x:x["reviewProtocol"].update(approval=True),
           "human decision")
    reject("fake-decision",lambda x:x["reviewProtocol"].update(decision="APPROVED"),
           "human decision")
    reject("wpf-live",lambda x:x["wpf"].update(realAdapterEnabled=True),"WPF blocked")
    reject("writing",lambda x:x["wpf"].update(writeEnabled=True),"WPF blocked")
    reject("approved-map",lambda x:x["wpf"].update(approvedMapCount=1),"WPF blocked")
    reject("approve-governance",lambda x:x["governance"].update(approved=True),
           "governance approval gate")
    reject("operate-governance",lambda x:x["governance"].update(operational=True),
           "governance approval gate")
    reject("approve-A12",lambda x:x["governance"].update(A12="APPROVED"),
           "governance approval gate")
    reject("risk-accepted-governance",lambda x:x["governance"]["riskAcceptances"].update({"RA-01":"CI"}),
           "governance approval gate")
    reject("false-limitations",lambda x:x["limitations"].__setitem__(0,"A"*80),
           "limitations content")
    reject("extra-schema-key",lambda x:x.update(humanAuthorized=True),"root: keys")
    for raw,name in (('{"x":1,"x":2}',"duplicate-json-root"),
                     ('{"review":{"decision":"NOT_MADE","decision":"APPROVED"}}',"duplicate-json-nested")):
        try: parse(raw)
        except Invalid as e: need("JSON duplicate key" in str(e),"wrong duplicate error")
        else: raise AssertionError(name+" was accepted")
        out.append({"id":name,"result":"REJECTED","expectedReason":"JSON duplicate key"})
    # F32-R: primary evidence must not be invented or silently promoted.
    for i in range(12):
        cid=CRITERIA[i]
        reject("primary-receipt-"+cid,
               lambda x,i=i:x["primaryEvidence"][i].update(receiptStatus="RECEIVED"),
               "evidence slot cannot promote "+cid)
    for i in range(8):
        cid=CRITERIA[i]
        reject("primary-fake-hash-"+cid,
               lambda x,i=i:x["primaryEvidence"][i].update(sha256="a"*64),
               "evidence slot cannot promote "+cid)
    for i in range(5):
        gid=GATES[i]
        reject("gate-risk-"+gid,lambda x,i=i:x["gates"][i].update(riskIds=["RA-08"]),
               "gate risk links "+gid)
        reject("gate-closure-"+gid,lambda x,i=i:x["gates"][i].update(closureRequirement="approved by CI"),
               "gate closure "+gid)
    reject("source-claim-independent",
           lambda x:x["sourceInventory"][0].update(independentObservationVerified=True),
           "source cannot authenticate observations MATRIX")
    reject("fake-signature",lambda x:x["reviewProtocol"].update(signature="GITHUB"),"human decision")
    reject("fake-assessment",lambda x:x["reviewProtocol"].update(independentAssessment="PASS"),"human decision")
    reject("fake-risk-decision",lambda x:x["reviewProtocol"].update(residualRiskDecision="ACCEPT"),"human decision")
    reject("fake-mitigation",lambda x:x["risks"][0].update(mitigationReview="done"),"risk acceptance RA-01")
    reject("risk-accepted-by",lambda x:x["risks"][1].update(acceptedBy="CI"),"risk acceptance RA-02")
    reject("risk-accepted-at",lambda x:x["risks"][2].update(acceptedAt="2026-11-21"),"risk acceptance RA-03")
    reject("gate-closed-at",lambda x:x["gates"][0].update(completedAt="2026-11-21"),"gate decision G1")
    reject("gate-named-reviewer",lambda x:x["gates"][1].update(reviewer="BOT"),"gate decision G2")
    reject("promote-next-action",lambda x:x.update(nextAction="APPROVED"),"next action")
    # Exercise real upstream source() with mutated F31 bytes, not merely a mutated dossier pointer.
    corrupted=F31_PATH.read_bytes()+b" "
    try: source(corrupted)
    except Invalid as e:
        need("F31 immutable source blob" in str(e),"F31 mutation wrong rejection")
    else:
        raise AssertionError("F31 source mutation accepted")
    out.append({"id":"alter-F31-content","result":"REJECTED","expectedReason":"F31 immutable source blob"})
    need(len(out)==86 and len({x["id"] for x in out})==86,"negative inventory incomplete")
    return out

def main():
    ap=argparse.ArgumentParser()
    ap.add_argument("--self-test",action="store_true")
    ap.add_argument("--report",type=Path)
    args=ap.parse_args()
    upstream=source()
    dossier=parse(F32_PATH.read_text(encoding="utf-8"))
    report=validate(dossier,upstream)
    if args.self_test:
        cases=negative_tests(dossier,upstream)
        report["negativeCases"]=cases
        report["negativeCount"]=len(cases)
    raw=json.dumps(report,sort_keys=True,ensure_ascii=False,indent=2)+"\n"
    if args.report: args.report.write_text(raw,encoding="utf-8")
    print(raw,end="")
if __name__=="__main__": main()
