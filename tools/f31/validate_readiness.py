#!/usr/bin/env python3
"""F31-R: offline-only, fail-closed semantic and source provenance validation."""
from __future__ import annotations
import argparse
import copy
import hashlib
import json
import pathlib
import re
import subprocess

ROOT = pathlib.Path(__file__).resolve().parents[2]
MANIFEST = ROOT / "evidence/f31/minimal_map_readiness.json"
MAPPING_COMMIT = "df5be0493a1ac7be57208ff0e91c2d7193dfec73"
SOURCE_PINS = {
  "MATRIX": {
    "path": "research/builds/fm2008-8.0.2v1-minimal-map-approval-matrix.md",
    "gitBlobSha1": "b29d4e21d97209a52fc009ab3d4eac03b4956f49"
  },
  "RISKS": {
    "path": "research/builds/fm2008-8.0.2v1-residual-risk-review-after-r07.md",
    "gitBlobSha1": "071368d325fc9770a6c436eb67bf520bd5344207"
  },
  "AUDIT": {
    "path": "research/builds/fm2008-8.0.2v1-a01-a12-code-audit-2026-11-19.md",
    "gitBlobSha1": "59e0513a23272ed8e569043edcee762b1b4ad06f"
  },
  "SECOND_SAVE": {
    "path": "research/builds/fm2008-8.0.2v1-second-save-distributed-r07.md",
    "gitBlobSha1": "9489499c3e91d4c7b7ad2099c6e1c2425249dcfd"
  },
  "PROTOCOL": {
    "path": "research/builds/fm2008-8.0.2v1-independent-verification-protocol.md",
    "gitBlobSha1": "96b5504062384229f631ffefabcd9e8e8f22cff8"
  }
}
EXPECTED_CRITERIA = {
  "A01": {
    "status": "PARTIAL",
    "gap": "Revisar corrida de identidade de PID, executável e hash; fixtures de troca concorrente",
    "risks": [
      "RA-01",
      "RA-06"
    ],
    "sourceIds": [
      "MATRIX",
      "AUDIT"
    ]
  },
  "A02": {
    "status": "PARTIAL",
    "gap": "Registrar base por instância; realocação real não observada, sem forçar cenário",
    "risks": [
      "RA-01"
    ],
    "sourceIds": [
      "MATRIX",
      "AUDIT"
    ]
  },
  "A03": {
    "status": "SAMPLED",
    "gap": "Cabeçalhos e contagens amostrais; sem validação exaustiva da tabela",
    "risks": [
      "RA-02",
      "RA-03"
    ],
    "sourceIds": [
      "MATRIX",
      "AUDIT"
    ]
  },
  "A04": {
    "status": "USER_DECLARED",
    "gap": "9/9 pares em amostras distribuídas de dois saves segundo conferência declarada; confirmar nome comum e proveniência",
    "risks": [
      "RA-05"
    ],
    "sourceIds": [
      "MATRIX",
      "RISKS",
      "SECOND_SAVE",
      "PROTOCOL"
    ]
  },
  "A05": {
    "status": "SIMULATED",
    "gap": "Falhas adversas de ponteiros e strings simuladas; TOCTOU real não demonstrado",
    "risks": [
      "RA-04"
    ],
    "sourceIds": [
      "MATRIX",
      "AUDIT"
    ]
  },
  "A06": {
    "status": "PARTIAL",
    "gap": "Invariante best-effort; ABA e alteração in-place não eliminados",
    "risks": [
      "RA-03"
    ],
    "sourceIds": [
      "MATRIX",
      "AUDIT"
    ]
  },
  "A07": {
    "status": "PARTIAL",
    "gap": "Reinício normal demonstrado; interrupção durante leitura real não comprovada",
    "risks": [
      "RA-02",
      "RA-06"
    ],
    "sourceIds": [
      "MATRIX",
      "RISKS",
      "AUDIT"
    ]
  },
  "A08": {
    "status": "SIMULATED",
    "gap": "Consulta incremental offline; política de invalidação sob mudanças estruturais deve ser revalidada",
    "risks": [
      "RA-02",
      "RA-03"
    ],
    "sourceIds": [
      "MATRIX",
      "AUDIT"
    ]
  },
  "A09": {
    "status": "BLOCKED_SAFE",
    "gap": "UI offline e política somente leitura; nenhuma conexão operacional",
    "risks": [
      "RA-08"
    ],
    "sourceIds": [
      "MATRIX",
      "AUDIT"
    ]
  },
  "A10": {
    "status": "SAMPLED",
    "gap": "Nove índices distribuídos em ambos os saves com conferência declarada; não extrapolar para tabela inteira",
    "risks": [
      "RA-05"
    ],
    "sourceIds": [
      "MATRIX",
      "RISKS",
      "SECOND_SAVE",
      "PROTOCOL"
    ]
  },
  "A11": {
    "status": "PARTIAL",
    "gap": "Hash/variante identificados; ensaios negativos de compatibilidade e corrida pendentes",
    "risks": [
      "RA-06"
    ],
    "sourceIds": [
      "MATRIX",
      "AUDIT"
    ]
  },
  "A12": {
    "status": "GATE_BLOCKED",
    "gap": "Revisão independente e decisão humana formal ausentes; não derivar de CI ou PR",
    "risks": [
      "RA-01",
      "RA-02",
      "RA-03",
      "RA-04",
      "RA-05",
      "RA-06",
      "RA-07",
      "RA-08"
    ],
    "sourceIds": [
      "MATRIX",
      "RISKS",
      "PROTOCOL"
    ]
  }
}
EXPECTED_RISKS = {
  "RA-01": {
    "description": "Base/módulo incorreto; identidade do processo",
    "sourceIds": [
      "MATRIX",
      "RISKS"
    ]
  },
  "RA-02": {
    "description": "Ponteiros e índices transitórios entre saves/sessões",
    "sourceIds": [
      "MATRIX",
      "RISKS"
    ]
  },
  "RA-03": {
    "description": "Leituras rasgadas e efeito ABA",
    "sourceIds": [
      "MATRIX",
      "RISKS"
    ]
  },
  "RA-04": {
    "description": "Ponteiros, strings e leituras parciais",
    "sourceIds": [
      "MATRIX",
      "RISKS"
    ]
  },
  "RA-05": {
    "description": "Identificação incorreta fora da amostra",
    "sourceIds": [
      "MATRIX",
      "RISKS"
    ]
  },
  "RA-06": {
    "description": "Mudança de build/processo/módulo",
    "sourceIds": [
      "MATRIX",
      "RISKS"
    ]
  },
  "RA-07": {
    "description": "Extrapolação indevida dos offsets legados",
    "sourceIds": [
      "MATRIX",
      "RISKS"
    ]
  },
  "RA-08": {
    "description": "Escrita acidental/permissão excessiva",
    "sourceIds": [
      "MATRIX",
      "RISKS"
    ]
  }
}
CRITERIA = set(EXPECTED_CRITERIA)
RISKS = set(EXPECTED_RISKS)
class Invalid(ValueError):
    pass

def require(ok, reason):
    if not ok:
        raise Invalid(reason)

def exact_keys(value, expected, label):
    require(type(value) is dict and set(value) == set(expected), f"{label}: closed schema violation")

def no_duplicates(pairs):
    result = {}
    for key,value in pairs:
        require(key not in result, "duplicate JSON key")
        result[key] = value
    return result

def load(raw):
    return json.loads(raw, object_pairs_hook=no_duplicates,
        parse_constant=lambda _: (_ for _ in ()).throw(Invalid("non-finite JSON constant")))

def blob_sha1(raw):
    return hashlib.sha1(b"blob " + str(len(raw)).encode("ascii") + b"\x00" + raw).hexdigest()

def verify_sources(sources, source_root):
    require(type(sources) is list and len(sources)==len(SOURCE_PINS), "source inventory")
    require(source_root is not None and source_root.is_dir(), "mapping checkout required")
    checkout = subprocess.check_output(["git","rev-parse","HEAD"],cwd=source_root,text=True).strip()
    require(checkout == MAPPING_COMMIT, "mapping checkout commit mismatch")
    names=set()
    verified=[]
    root=source_root.resolve()
    for entry in sources:
        exact_keys(entry, {"id","path","gitBlobSha1"}, "source")
        sid=entry["id"]
        require(type(sid) is str and sid in SOURCE_PINS and sid not in names, "source ID/duplicate")
        names.add(sid)
        require(entry["path"]==SOURCE_PINS[sid]["path"], f"source path {sid}")
        require(entry["gitBlobSha1"]==SOURCE_PINS[sid]["gitBlobSha1"], f"source pin {sid}")
        file=source_root / entry["path"]
        require(file.is_file() and not file.is_symlink() and file.resolve().is_relative_to(root),
            f"source file missing or unsafe {sid}")
        raw=file.read_bytes()
        require(blob_sha1(raw)==entry["gitBlobSha1"], f"source blob mismatch {sid}")
        verified.append({"id":sid,"path":entry["path"],"gitBlobSha1":entry["gitBlobSha1"],
                         "sha256":hashlib.sha256(raw).hexdigest(),"size":len(raw)})
    require(names==set(SOURCE_PINS), "source coverage")
    return sorted(verified,key=lambda x:x["id"])

def validate(m, source_root=None):
    exact_keys(m, {"schema","scope","base","sources","criteria","risks","observations",
                   "wpfReadiness","governance","nextGate"}, "root")
    require(m["schema"]=="f31-readiness/v1" and m["scope"]=="OFFLINE_PLANNING_ONLY", "schema/scope")
    base=m["base"]
    exact_keys(base,{"repo","commit","mappingRepo","mappingCommit","build","fixtureSource"},"base")
    require(base == {"repo":"marcosvilhena87/FMRTE.2008",
      "commit":"47d9d6f4c460490888573842d23ba571e78b6901",
      "mappingRepo":"marcosvilhena87/FM2008.mapping",
      "mappingCommit":MAPPING_COMMIT,"build":"FM2008 8.0.2v1",
      "fixtureSource":"synthetic-only"}, "base provenance identity")
    verified=verify_sources(m["sources"],source_root)
    criteria=m["criteria"]
    require(type(criteria) is list and len(criteria)==12, "criteria count")
    seen=set()
    for c in criteria:
        exact_keys(c,{"id","status","gap","risks","sourceIds"},"criterion")
        cid=c["id"]
        require(type(cid) is str and cid in CRITERIA and cid not in seen, "criterion ID")
        seen.add(cid)
        fixed=EXPECTED_CRITERIA[cid]
        require(type(c["status"]) is str and c["status"]==fixed["status"],f"criterion status {cid}")
        require(type(c["gap"]) is str and c["gap"]==fixed["gap"],f"criterion gap {cid}")
        require(type(c["risks"]) is list and c["risks"]==fixed["risks"],f"criterion risk links {cid}")
        require(type(c["sourceIds"]) is list and c["sourceIds"]==fixed["sourceIds"],f"criterion source links {cid}")
    require(seen==CRITERIA,"criteria coverage")
    risks=m["risks"]
    require(type(risks) is list and len(risks)==8,"risks count")
    seen=set()
    for r in risks:
        exact_keys(r,{"id","description","status","acceptedBy","acceptedAt","sourceIds"},"risk")
        rid=r["id"]
        require(type(rid) is str and rid in RISKS and rid not in seen,"risk ID")
        seen.add(rid)
        fixed=EXPECTED_RISKS[rid]
        require(type(r["description"]) is str and r["description"]==fixed["description"],
                f"risk description {rid}")
        require(type(r["sourceIds"]) is list and r["sourceIds"]==fixed["sourceIds"],
                f"risk source links {rid}")
        require(r["status"]=="NO_ACCEPTANCE" and r["acceptedBy"] is None
                and r["acceptedAt"] is None, f"risk unapproved {rid}")
    require(seen==RISKS,"risk coverage")
    limits=m["observations"]
    require(type(limits) is list and len(limits)>=6
            and all(type(x) is str and len(x)>35 for x in limits),"limitations")
    mandatory=("não é auditoria externa independente","realocação","ABA",
               "Amostragem","CI e PR","não autenticam externamente")
    require(all(any(phrase in text for text in limits) for phrase in mandatory),
            "required limitation removed")
    w=m["wpfReadiness"]
    exact_keys(w,{"mode","dataSource","proposedPort","allowedFields","operationalAdapterEnabled",
                  "requiresExactBuildAndApprovedManifest","requiresIndependentReview",
                  "requiresExplicitHumanApproval","writingEnabled"},"wpf")
    require(w["mode"]=="OFFLINE_ONLY" and w["dataSource"]=="SYNTHETIC"
            and w["proposedPort"]=="IReadOnlyIdentityQuery (design-only)", "wpf source")
    require(w["allowedFields"]==["playerId","firstName","lastName","commonName"],"wpf fields")
    require(w["operationalAdapterEnabled"] is False and w["writingEnabled"] is False,"wpf gate")
    for k in ("requiresExactBuildAndApprovedManifest","requiresIndependentReview",
              "requiresExplicitHumanApproval"):
        require(w[k] is True,f"wpf mandatory gate {k}")
    gov=m["governance"]
    expected={"approvalState":"NoApprovedPlayerMaps","approved":False,"operational":False,
      "approvedPlayerMaps":0,"B03":"PENDING-DECLARED","A12":"GATE-BLOQUEADO",
      "decision":"NOT_MADE","humanReviewer":None,"humanReviewTimestamp":None,
      "humanAuthority":None,"humanApproval":None,"ciIsHumanApproval":False,
      "prReviewIsHumanApproval":False}
    exact_keys(gov,expected,"governance")
    for k,value in expected.items():
        require(type(gov[k]) is type(value) and gov[k]==value,f"governance {k}")
    require(m["nextGate"]=="INDEPENDENT_EVIDENCE_AND_HUMAN_REVIEW_PENDING", "next gate")
    return {"result":"PASS","schema":"f31-verification/v2","criteria":12,"risks":8,
      "sources":verified,"mappingCommit":MAPPING_COMMIT,"operational":False,
      "approved":False,"humanDecision":"NOT_MADE","wpfMode":"OFFLINE_ONLY",
      "historicObservationsIndependentlyAuthenticated":False}

def negatives(base,source_root):
    cases=[]
    def mutation(name,mutate,reason):
        trial=copy.deepcopy(base)
        mutate(trial)
        try:
            validate(trial,source_root)
        except Invalid as exc:
            require(reason in str(exc),f"{name}: rejected for wrong reason: {exc}")
        else:
            raise AssertionError(f"{name}: accepted invalid mutation")
        cases.append({"id":name,"result":"REJECTED","reason":reason})
    mutation("A01-status",lambda x:x["criteria"][0].update(status="SAMPLED"),"criterion status A01")
    mutation("A02-gap",lambda x:x["criteria"][1].update(gap="Incorrect provenance but sufficiently long text"),"criterion gap A02")
    mutation("A03-links",lambda x:x["criteria"][2].update(risks=["RA-08"]),"criterion risk links A03")
    mutation("A04-sources",lambda x:x["criteria"][3].update(sourceIds=["MATRIX"]),"criterion source links A04")
    mutation("A05-status",lambda x:x["criteria"][4].update(status="SAMPLED"),"criterion status A05")
    mutation("A06-status",lambda x:x["criteria"][5].update(status="SAMPLED"),"criterion status A06")
    mutation("A07-gap",lambda x:x["criteria"][6].update(gap="Incorrect long claim about completed live tests"),"criterion gap A07")
    mutation("A08-status",lambda x:x["criteria"][7].update(status="SAMPLED"),"criterion status A08")
    mutation("A09-status",lambda x:x["criteria"][8].update(status="SAMPLED"),"criterion status A09")
    mutation("A10-status",lambda x:x["criteria"][9].update(status="SAMPLED"),"criterion status A10")
    mutation("A11-status",lambda x:x["criteria"][10].update(status="SAMPLED"),"criterion status A11")
    mutation("A12-status",lambda x:x["criteria"][11].update(status="PARTIAL"),"criterion status A12")
    mutation("missing-criterion",lambda x:x["criteria"].pop(),"criteria count")
    mutation("duplicate-criterion",lambda x:x["criteria"][1].update(id="A01"),"criterion ID")
    for i in range(8):
        rid=f"RA-{i+1:02d}"
        mutation(f"{rid}-acceptance",lambda x,i=i:x["risks"][i].update(status="ACCEPTED"),f"risk unapproved {rid}")
    mutation("risk-description",lambda x:x["risks"][0].update(description="Unverified changed meaning"),"risk description RA-01")
    mutation("risk-source-link",lambda x:x["risks"][1].update(sourceIds=["AUDIT"]),"risk source links RA-02")
    mutation("risk-acceptance-author",lambda x:x["risks"][2].update(acceptedBy="CI"),"risk unapproved RA-03")
    mutation("risk-acceptance-time",lambda x:x["risks"][3].update(acceptedAt="2026-11-21"),"risk unapproved RA-04")
    mutation("source-path",lambda x:x["sources"][0].update(path="../elsewhere"),"source path MATRIX")
    mutation("source-hash",lambda x:x["sources"][0].update(gitBlobSha1="0"*40),"source pin MATRIX")
    mutation("source-duplicate",lambda x:x["sources"][1].update(id="MATRIX"),"source ID/duplicate")
    mutation("mapping-commit",lambda x:x["base"].update(mappingCommit="0"*40),"base provenance identity")
    mutation("extra-root",lambda x:x.update(authorized=True),"root: closed schema")
    mutation("observation-delete",lambda x:x["observations"].pop(),"required limitation removed")
    mutation("WPF-live",lambda x:x["wpfReadiness"].update(mode="LIVE"),"wpf source")
    mutation("WPF-real-source",lambda x:x["wpfReadiness"].update(dataSource="FM.EXE"),"wpf source")
    mutation("WPF-adapter",lambda x:x["wpfReadiness"].update(operationalAdapterEnabled=True),"wpf gate")
    mutation("WPF-write",lambda x:x["wpfReadiness"].update(writingEnabled=True),"wpf gate")
    mutation("WPF-extra-field",lambda x:x["wpfReadiness"]["allowedFields"].append("currentAbility"),"wpf fields")
    for key in ("requiresExactBuildAndApprovedManifest","requiresIndependentReview","requiresExplicitHumanApproval"):
        mutation("WPF-"+key,lambda x,key=key:x["wpfReadiness"].update({key:False}),f"wpf mandatory gate {key}")
    for key,value in (("approved",True),("operational",True),("approvedPlayerMaps",1),
                      ("A12","APPROVED"),("B03","COMPLETE"),("decision","APPROVED"),
                      ("humanReviewer","CI"),("humanAuthority","CI"),("humanReviewTimestamp","2026-11-21"),
                      ("humanApproval",True),("ciIsHumanApproval",True),("prReviewIsHumanApproval",True)):
        mutation("governance-"+key,lambda x,key=key,value=value:x["governance"].update({key:value}),f"governance {key}")
    for raw,label in (('{"a":1,"a":2}',"duplicate-root"),('{"g":{"a":1,"a":2}}',"duplicate-nested")):
        try:
            load(raw)
        except Invalid as exc:
            require("duplicate JSON key" in str(exc), f"{label}: wrong JSON error")
        else:
            raise AssertionError(f"{label}: duplicate accepted")
        cases.append({"id":label,"result":"REJECTED","reason":"duplicate JSON key"})
    require(len(cases)==54 and len({x["id"] for x in cases})==54, "negative case coverage")
    return cases

def main():
    parser=argparse.ArgumentParser()
    parser.add_argument("--source-root",type=pathlib.Path,required=True)
    parser.add_argument("--self-test",action="store_true")
    parser.add_argument("--report",type=pathlib.Path)
    args=parser.parse_args()
    data=load(MANIFEST.read_text(encoding="utf-8"))
    result=validate(data,args.source_root)
    if args.self_test:
        cases=negatives(data,args.source_root)
        result["negativeTests"]=len(cases)
        result["negativeCases"]=cases
    output=json.dumps(result,sort_keys=True,ensure_ascii=False,indent=2)+"\n"
    if args.report:
        args.report.write_text(output,encoding="utf-8")
    print(output,end="")
if __name__=="__main__":
    main()
