#!/usr/bin/env python3
"""F31: offline-only roadmap readiness and safety contract; never promotes a map."""
import argparse
import copy
import json
import pathlib

ROOT = pathlib.Path(__file__).resolve().parents[2]
PATH = ROOT / "evidence/f31/minimal_map_readiness.json"
CRITERIA = {f"A{i:02d}" for i in range(1, 13)}
RISKS = {f"RA-{i:02d}" for i in range(1, 9)}
STATES = {"PARTIAL", "SAMPLED", "USER_DECLARED", "SIMULATED", "BLOCKED_SAFE", "GATE_BLOCKED"}

class Invalid(ValueError):
    pass

def require(value, reason):
    if not value:
        raise Invalid(reason)

def keys(obj, expected, label):
    require(type(obj) is dict and set(obj) == set(expected), f"{label}: invalid object keys")

def no_duplicate_pairs(pairs):
    obj = {}
    for key, value in pairs:
        require(key not in obj, f"duplicate key: {key}")
        obj[key] = value
    return obj

def load(raw):
    return json.loads(raw, object_pairs_hook=no_duplicate_pairs,
        parse_constant=lambda x: (_ for _ in ()).throw(Invalid("non-finite JSON")))

def validate(m):
    keys(m, {"schema","scope","base","criteria","risks","observations","wpfReadiness","governance","nextGate"}, "root")
    require(m["schema"] == "f31-readiness/v1" and m["scope"] == "OFFLINE_PLANNING_ONLY", "scope/schema")
    b = m["base"]
    keys(b, {"repo","commit","mappingRepo","mappingCommit","build","fixtureSource"}, "base")
    require(b == {"repo":"marcosvilhena87/FMRTE.2008",
      "commit":"47d9d6f4c460490888573842d23ba571e78b6901",
      "mappingRepo":"marcosvilhena87/FM2008.mapping",
      "mappingCommit":"df5be0493a1ac7be57208ff0e91c2d7193dfec73",
      "build":"FM2008 8.0.2v1","fixtureSource":"synthetic-only"}, "source version mismatch")
    criteria=m["criteria"]; require(type(criteria) is list and len(criteria)==12, "12 criteria required")
    ids=set()
    for c in criteria:
        keys(c, {"id","status","gap","risks"}, "criterion")
        require(type(c["id"]) is str and c["id"] in CRITERIA and c["id"] not in ids, "criterion id")
        ids.add(c["id"])
        require(c["status"] in STATES and type(c["gap"]) is str and len(c["gap"])>=30, "criterion substance")
        require(type(c["risks"]) is list and len(c["risks"])>0 and len(set(c["risks"]))==len(c["risks"])
          and set(c["risks"])<=RISKS, "criterion risk links")
    require(ids==CRITERIA, "criterion coverage")
    require(next(c for c in criteria if c["id"]=="A12")["status"]=="GATE_BLOCKED","A12 must be blocked")
    require(next(c for c in criteria if c["id"]=="A09")["status"]=="BLOCKED_SAFE","A09 gate")
    risks=m["risks"]; require(type(risks) is list and len(risks)==8, "8 risks required")
    seen=set()
    for r in risks:
        keys(r, {"id","description","status","acceptedBy","acceptedAt"}, "risk")
        require(type(r["id"]) is str and r["id"] in RISKS and r["id"] not in seen, "risk id")
        seen.add(r["id"])
        require(type(r["description"]) is str and len(r["description"])>20, "risk description")
        require(r["status"]=="NO_ACCEPTANCE" and r["acceptedBy"] is None
            and r["acceptedAt"] is None, "unapproved risk acceptance")
    require(seen==RISKS, "risk coverage")
    require(type(m["observations"]) is list and len(m["observations"])>=5
        and all(type(x) is str and len(x)>35 for x in m["observations"]), "observation limits")
    w=m["wpfReadiness"]
    keys(w, {"mode","dataSource","proposedPort","allowedFields","operationalAdapterEnabled",
      "requiresExactBuildAndApprovedManifest","requiresIndependentReview",
      "requiresExplicitHumanApproval","writingEnabled"}, "wpf")
    require(w["mode"]=="OFFLINE_ONLY" and w["dataSource"]=="SYNTHETIC"
        and w["proposedPort"]=="IReadOnlyIdentityQuery (design-only)", "wpf source")
    require(w["allowedFields"]==["playerId","firstName","lastName","commonName"], "wpf field scope")
    require(w["operationalAdapterEnabled"] is False and w["writingEnabled"] is False, "operational WPF")
    require(all(w[k] is True for k in ("requiresExactBuildAndApprovedManifest",
      "requiresIndependentReview","requiresExplicitHumanApproval")), "mandatory WPF gates")
    g=m["governance"]
    expected={"approvalState":"NoApprovedPlayerMaps","approved":False,"operational":False,
       "approvedPlayerMaps":0,"B03":"PENDING-DECLARED","A12":"GATE-BLOQUEADO",
       "decision":"NOT_MADE","humanReviewer":None,"humanReviewTimestamp":None,
       "humanAuthority":None,"humanApproval":None,"ciIsHumanApproval":False,
       "prReviewIsHumanApproval":False}
    keys(g,expected,"governance")
    require(all(type(g[k]) is type(v) and g[k]==v for k,v in expected.items()), "approval was promoted")
    require(m["nextGate"]=="INDEPENDENT_EVIDENCE_AND_HUMAN_REVIEW_PENDING", "next gate")
    return {"result":"PASS","criteria":len(ids),"risks":len(seen),"operational":False,
       "humanDecision":"NOT_MADE","wpfMode":"OFFLINE_ONLY"}

def negatives(baseline):
    cases=[]
    def case(name, edit):
        trial=copy.deepcopy(baseline)
        edit(trial)
        try:
            validate(trial)
        except (Invalid, TypeError, KeyError, StopIteration, ValueError):
            cases.append(name)
            return
        raise AssertionError(f"negative mutation incorrectly accepted: {name}")
    case("operational",lambda x:x["governance"].update(operational=True))
    case("approved",lambda x:x["governance"].update(approved=True))
    case("count",lambda x:x["governance"].update(approvedPlayerMaps=1))
    case("decision",lambda x:x["governance"].update(decision="APPROVED"))
    case("reviewer",lambda x:x["governance"].update(humanReviewer="CI"))
    case("human approval",lambda x:x["governance"].update(humanApproval=True))
    case("CI promotion",lambda x:x["governance"].update(ciIsHumanApproval=True))
    case("PR promotion",lambda x:x["governance"].update(prReviewIsHumanApproval=True))
    case("A12 promotion",lambda x:x["criteria"][-1].update(status="SAMPLED"))
    case("missing A01",lambda x:x["criteria"].pop(0))
    case("duplicate A01",lambda x:x["criteria"].__setitem__(1,x["criteria"][0]))
    case("mapping commit",lambda x:x["base"].update(mappingCommit="0"*40))
    case("extra root key",lambda x:x.update(activate=True))
    case("live WPF",lambda x:x["wpfReadiness"].update(mode="LIVE"))
    case("real source WPF",lambda x:x["wpfReadiness"].update(dataSource="FM.EXE"))
    case("enable adapter",lambda x:x["wpfReadiness"].update(operationalAdapterEnabled=True))
    case("enable writes",lambda x:x["wpfReadiness"].update(writingEnabled=True))
    case("expand fields",lambda x:x["wpfReadiness"]["allowedFields"].append("currentAbility"))
    case("remove review",lambda x:x["wpfReadiness"].update(requiresIndependentReview=False))
    case("remove exact build",lambda x:x["wpfReadiness"].update(requiresExactBuildAndApprovedManifest=False))
    for i in range(8):
        case(f"risk acceptance {i+1}",lambda x,i=i:x["risks"][i].update(status="ACCEPTED"))
    return cases

def main():
    p=argparse.ArgumentParser()
    p.add_argument("--self-test",action="store_true")
    p.add_argument("--report",type=pathlib.Path)
    args=p.parse_args()
    m=load(PATH.read_text(encoding="utf-8"))
    result=validate(m)
    if args.self_test:
        tests=negatives(m)
        require(len(tests)==28 and len(set(tests))==28, "incomplete negative suite")
        for raw in ('{"a":1,"a":2}','{"g":{"approved":false,"approved":true}}'):
            try: load(raw)
            except Invalid: pass
            else: raise AssertionError("duplicate JSON key accepted")
        result["negativeTests"]=30
        result["negativeNames"]=tests+["duplicate root JSON","duplicate nested JSON"]
    output=json.dumps(result,ensure_ascii=False,sort_keys=True,indent=2)+"\n"
    if args.report: args.report.write_text(output,encoding="utf-8")
    print(output,end="")
if __name__=="__main__":
    main()
