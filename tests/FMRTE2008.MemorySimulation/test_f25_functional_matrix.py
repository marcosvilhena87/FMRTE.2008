"""F25.4 fail-closed offline-only reconstruction matrix verification."""
import copy,json,pathlib,unittest
ROOT=pathlib.Path(__file__).resolve().parents[2]
MATRIX=ROOT/"tests/FMRTE2008.MemorySimulation/F25FunctionalReconstructionMatrix.json"
TARGETS={"Domain":"src/FMRTE2008.Domain/","Application":"src/FMRTE2008.Application/",
"Desktop":"src/FMRTE2008.Desktop/","Infrastructure.Windows":"src/FMRTE2008.Infrastructure.Windows/"}
CANONICAL_TYPES={
  "0x02000008": "FMRTE.frmMain",
  "0x02000005": "FM_Editor.frmOptions",
  "0x0200002f": "FMRTE.GameClasses.Player",
  "0x02000032": "FMRTE.GameClasses.PlayerAttributes",
  "0x02000014": "FMRTE.GameClasses.Contract",
  "0x02000012": "FMRTE.GameClasses.League",
  "0x02000030": "FMRTE.GameClasses.Players",
  "0x0200003c": "FMRTE.GameClasses.PlayersList",
  "0x02000017": "FM_Editor.UserControls.playersPositions",
  "0x02000042": "FM_Editor.UserControls.playersAttributes",
  "0x0200000d": "FMRTE.UserControls.leagueStandings",
  "0x0200000f": "FM_Editor.UserControls.contractsInformation",
  "0x0200002e": "FMRTE.GameClasses.PlayerInfo",
  "0x02000037": "FMRTE.GameClasses.ClubFinances",
  "0x02000038": "FMRTE.GameClasses.Club",
  "0x0200001f": "FMRTE.GameClasses.Nation"
}
class InvalidMatrix(ValueError): pass
def require(ok,why):
    if not ok:raise InvalidMatrix(why)
def verify(doc):
    require(set(doc)=={"schemaVersion","kind","source","architecture","rows","excludedRuntime","security"},"root keys")
    require(doc["schemaVersion"]==1 and doc["kind"]=="f25-functional-reconstruction-offline-matrix","kind")
    src=doc["source"]
    require(set(src)=={"historicalExecutableSha256","catalogSha256","catalogTypeCount","catalogMethodCount","observedArtifacts"},"source keys")
    require(src["historicalExecutableSha256"]=="e7ba25394eb1f69fc73814a4cbe198a45a5681670f7472a07d9ba9f2a0b3fb07","historical identity")
    require(src["catalogSha256"]=="ced806e946e3d08fefd2a828ad8281769f425906393bc78dfd8c9cf70d4348d9","catalog identity")
    require(src["catalogTypeCount"]==100 and src["catalogMethodCount"]==1421,"catalog count")
    arch=doc["architecture"]
    require(set(arch)=={"modules","infrastructureRule","functionalityEquivalenceVerified"},"arch keys")
    require(set(arch["modules"])==set(TARGETS) and arch["functionalityEquivalenceVerified"] is False,"architecture separation")
    require("No approved player maps" in arch["infrastructureRule"],"infrastructure guard")
    require(len(doc["rows"])==len(CANONICAL_TYPES),"fixed audited candidate set")
    ids=set();tokens=set()
    for row in doc["rows"]:
        require(set(row)=={"id","historicalToken","historicalFullName","targetModule","targetFile",
                "rationale","equivalenceStatus","deliveryStatus","operational","approved"},"row keys")
        require(row["id"].startswith("F25.4-") and row["id"] not in ids,"duplicate row")
        ids.add(row["id"])
        require(row["historicalToken"].startswith("0x020000") and row["historicalToken"] not in tokens,"TypeDef token")
        tokens.add(row["historicalToken"])
        require(CANONICAL_TYPES.get(row["historicalToken"])==row["historicalFullName"],"historical token/name mismatch")
        require(row["targetModule"] in TARGETS,"unknown module")
        require(row["targetFile"].startswith(TARGETS[row["targetModule"]]) and
                (ROOT/row["targetFile"]).is_file(),"invalid target")
        require(row["equivalenceStatus"] in ("OBSERVED","INFERRED"),"unverified equivalence")
        require(row["deliveryStatus"] in ("PROPOSED","OFFLINE_ONLY"),"unapproved delivery")
        require(row["operational"] is False and row["approved"] is False,"operational promotion")
        require(row["historicalFullName"] and len(row["rationale"])>15,"missing evidence/rationale")
    require(tokens==set(CANONICAL_TYPES),"missing canonical types")
    runtime=doc["excludedRuntime"]
    require(set(runtime)=={"module","reason","operational"} and
            runtime["module"]=="Infrastructure.Windows" and runtime["operational"] is False,"runtime exclusion")
    require(doc["security"]=={"NoApprovedPlayerMaps":True,"approved":False,"operational":False,
            "approvedPlayerMaps":0,"b03":"PENDING-DECLARED","gateA12":"GATE-BLOQUEADO",
            "decision":"NOT_MADE","acceptedRisks":0,"humanReviewed":False},"approval state")
    return True
class MatrixTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.good=json.loads(MATRIX.read_text(encoding="utf-8"))
    def reject(self,func):
        doc=copy.deepcopy(self.good);func(doc)
        with self.assertRaises(InvalidMatrix):verify(doc)
    def test_canonical(self):self.assertTrue(verify(self.good))
    def test_false_approval(self):self.reject(lambda d:d["security"].update(approved=True))
    def test_false_operational(self):self.reject(lambda d:d["security"].update(operational=True))
    def test_fake_human_decision(self):self.reject(lambda d:d["security"].update(decision="APPROVED"))
    def test_fake_human_review(self):self.reject(lambda d:d["security"].update(humanReviewed=True))
    def test_accepted_risks(self):self.reject(lambda d:d["security"].update(acceptedRisks=1))
    def test_false_map_count(self):self.reject(lambda d:d["security"].update(approvedPlayerMaps=1))
    def test_wrong_catalog_hash(self):self.reject(lambda d:d["source"].update(catalogSha256="0"*64))
    def test_wrong_exe_hash(self):self.reject(lambda d:d["source"].update(historicalExecutableSha256="0"*64))
    def test_omit_row(self):self.reject(lambda d:d["rows"].pop())
    def test_duplicate_row(self):self.reject(lambda d:d["rows"].append(copy.deepcopy(d["rows"][0])))
    def test_unknown_module(self):self.reject(lambda d:d["rows"][0].update(targetModule="NativeEditor"))
    def test_bad_target_file(self):self.reject(lambda d:d["rows"][0].update(targetFile="../../fm.exe"))
    def test_wrong_target_module(self):self.reject(lambda d:d["rows"][0].update(targetModule="Domain"))
    def test_false_equivalence(self):self.reject(lambda d:d["architecture"].update(functionalityEquivalenceVerified=True))
    def test_runtime_enabled(self):self.reject(lambda d:d["excludedRuntime"].update(operational=True))
    def test_unapproved_delivery(self):self.reject(lambda d:d["rows"][0].update(deliveryStatus="LIVE"))
    def test_unapproved_row(self):self.reject(lambda d:d["rows"][0].update(approved=True))
    def test_new_field(self):self.reject(lambda d:d["rows"][0].update(memoryOffset=1234))
    def test_token_name_swap(self):self.reject(lambda d:d["rows"][0].update(historicalFullName="FMRTE.GameClasses.Player"))
    def test_missing_provenance(self):self.reject(lambda d:d["rows"][0].update(rationale=""))
if __name__=="__main__":unittest.main(verbosity=2)
