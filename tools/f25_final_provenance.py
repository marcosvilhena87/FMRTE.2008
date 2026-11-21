"""F25.5-R independent, fail-closed documentary provenance checks; no executable in CI."""
from __future__ import annotations
import argparse, copy, hashlib, json, os, pathlib, sys
ROOT=pathlib.Path(__file__).resolve().parents[2]
FOLDER=ROOT/"tests/FMRTE2008.MemorySimulation"
MATRIX=FOLDER/"F25FunctionalReconstructionMatrix.json"
PROOF=FOLDER/"F25CatalogTypeProvenance.json"
CAT_SHA="ced806e946e3d08fefd2a828ad8281769f425906393bc78dfd8c9cf70d4348d9"
ZIP_SHA="491f15b1255fe135dfd5983520cb1a090b8edbd14511205a6f3b576b06a9c9cd"
EXE_SHA="e7ba25394eb1f69fc73814a4cbe198a45a5681670f7472a07d9ba9f2a0b3fb07"
TARGETS=("src/FMRTE2008.Domain/Contracts.cs",
"src/FMRTE2008.Application/PlayerQuery.cs",
"src/FMRTE2008.Desktop/MainWindow.xaml",
"src/FMRTE2008.Infrastructure.Windows/VerifiedWindowsProcessOpener.cs")
class ProvenanceViolation(ValueError): pass
def require(ok,msg):
    if not ok:raise ProvenanceViolation(msg)
def sha(data):return hashlib.sha256(data).hexdigest()
def load():
    return json.loads(MATRIX.read_text(encoding="utf-8")),json.loads(PROOF.read_text(encoding="utf-8"))
def check_pair(matrix,proof):
    require(set(proof)=={"schemaVersion","kind","sourceCatalogSha256","sourceExecutableSha256",
        "sourceZipSha256","sourceCatalogTypeCount","derivation","types","security"},"closed proof")
    require(proof["schemaVersion"]==1 and proof["kind"]=="f25-catalog-type-provenance-offline","proof kind")
    require(proof["sourceCatalogSha256"]==matrix["source"]["catalogSha256"]==CAT_SHA,"catalog commitment")
    require(proof["sourceExecutableSha256"]==matrix["source"]["historicalExecutableSha256"]==EXE_SHA,"exe commitment")
    require(proof["sourceZipSha256"]==ZIP_SHA and proof["sourceCatalogTypeCount"]==matrix["source"]["catalogTypeCount"]==100,"ZIP/type commitment")
    require(proof["security"]=={"approved":False,"operational":False,"humanDecision":"NOT_MADE"},"proof approval")
    require(len(proof["types"])==len(matrix["rows"])==16,"source/row count")
    symbols={}
    for t in proof["types"]:
        require(set(t)=={"token","fullName"} and t["token"] not in symbols,"duplicate/extra type")
        require(isinstance(t["fullName"],str) and "." in t["fullName"],"invalid type")
        symbols[t["token"]]=t["fullName"]
    require(len(symbols)==16,"symbol uniqueness")
    require(all(r["historicalToken"] in symbols and
       r["historicalFullName"]==symbols[r["historicalToken"]] for r in matrix["rows"]),"symbol provenance mismatch")
    require(matrix["source"]["observedArtifacts"]==list(TARGETS),"source artifacts changed/omitted")
    require(all((ROOT/p).is_file() for p in TARGETS),"source artifact missing")
    require(matrix["architecture"]["functionalityEquivalenceVerified"] is False,"false equivalence")
    require(all(r["equivalenceStatus"]=="INFERRED" and r["operational"] is False and
       r["approved"] is False for r in matrix["rows"]),"architectural inference promoted")
    require(matrix["security"]=={"NoApprovedPlayerMaps":True,"approved":False,
       "operational":False,"approvedPlayerMaps":0,"b03":"PENDING-DECLARED",
       "gateA12":"GATE-BLOQUEADO","decision":"NOT_MADE",
       "acceptedRisks":0,"humanReviewed":False},"matrix security")
    return True
def verify_original(catalog_path,archive_path):
    """Separate local-only verification when owner supplies original bytes."""
    sys.path.insert(0,str(ROOT/"tools"))
    from f25_catalog import catalog
    import zipfile
    cbytes=pathlib.Path(catalog_path).read_bytes()
    require(sha(cbytes)==CAT_SHA,"local catalog hash does not match fixed reference")
    c=json.loads(cbytes)
    matrix,proof=load()
    require(c["identity"]["executableSha256"]==EXE_SHA and
        len(c["types"])==100 and len(c["methods"])==1421,"catalog content mismatch")
    actual={t["token"]:(t["namespace"]+"." if t["namespace"] else "")+t["name"] for t in c["types"]}
    require(all(actual.get(t["token"])==t["fullName"] for t in proof["types"]),"source TypeDef mismatch")
    zpath=pathlib.Path(archive_path)
    require(zpath.stat().st_size<=100000000 and sha(zpath.read_bytes())==ZIP_SHA,"ZIP mismatch")
    with zipfile.ZipFile(zpath) as z:
        members=[i for i in z.infolist() if i.filename=="FMRTE v1.0.0.5/FMRTE.exe"]
        require(len(members)==1 and members[0].file_size<=20000000,"member mismatch")
        payload=z.read(members[0])
    require(sha(payload)==EXE_SHA,"EXE mismatch")
    recomputed=catalog(payload)
    raw=(json.dumps(recomputed,indent=2,ensure_ascii=False)+"\n").encode("utf-8")
    require(sha(raw)==CAT_SHA,"catalog regeneration mismatch")
    return check_pair(matrix,proof)
def evidence_report(matrix,proof,run=None):
    run=run or {}
    pr_head=run.get("PR_HEAD_SHA")
    event_path=run.get("GITHUB_EVENT_PATH")
    if event_path and pathlib.Path(event_path).is_file():
        event=json.loads(pathlib.Path(event_path).read_text(encoding="utf-8"))
        pr_head=(event.get("pull_request") or {}).get("head",{}).get("sha") or pr_head
    artifacts={p:sha((ROOT/p).read_bytes()) for p in (
        "tests/FMRTE2008.MemorySimulation/F25HistoricalInventory.json",
        "tests/FMRTE2008.MemorySimulation/F25FunctionalReconstructionMatrix.json",
        "tests/FMRTE2008.MemorySimulation/F25CatalogTypeProvenance.json",
        "tools/f25_inventory.py","tools/f25_clr_metadata.py","tools/f25_catalog.py",
        "tools/f25_final_provenance.py")}
    return dict(schemaVersion=1,kind="f25-final-documentary-provenance",
        matrixSha256=sha(MATRIX.read_bytes()),proofSha256=sha(PROOF.read_bytes()),
        catalogSha256=CAT_SHA,archiveSha256=ZIP_SHA,exeSha256=EXE_SHA,
        symbolsCrossChecked=len(proof["types"]),sourceArtifactPaths=list(TARGETS),
        fileSha256=artifacts,
        execution=dict(runId=run.get("GITHUB_RUN_ID"),runAttempt=run.get("GITHUB_RUN_ATTEMPT"),
        event=run.get("GITHUB_EVENT_NAME"),checkoutSha=run.get("GITHUB_SHA"),
        prHeadSha=pr_head),
        checks=dict(offlineDocumentary=True,originalZipReverifiedInCI=False,
        githubChecksAreHumanApproval=False),decision=dict(approved=False,operational=False,
        humanDecision="NOT_MADE",acceptedRisks=0))
def main():
    ap=argparse.ArgumentParser()
    ap.add_argument("--catalog",type=pathlib.Path)
    ap.add_argument("--zip",type=pathlib.Path)
    ap.add_argument("--report",type=pathlib.Path)
    args=ap.parse_args()
    matrix,proof=load()
    check_pair(matrix,proof)
    require((args.catalog is None)==(args.zip is None),"catalog and ZIP must be supplied together")
    if args.catalog:verify_original(args.catalog,args.zip)
    result=evidence_report(matrix,proof,os.environ)
    if args.report:
        args.report.parent.mkdir(parents=True,exist_ok=True)
        args.report.write_text(json.dumps(result,indent=2)+"\n",encoding="utf-8")
    print("F25.5-R PASS: 16 source symbols / offline / NOT_MADE; historical ZIP checked:",bool(args.zip))
if __name__=="__main__":main()
