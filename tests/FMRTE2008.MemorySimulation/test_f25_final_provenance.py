"""F25.5-R negative controls: no original executable in GitHub Actions."""
import copy,importlib.util,pathlib,sys,unittest
ROOT=pathlib.Path(__file__).resolve().parents[2]
sys.path.insert(0,str(ROOT/"tools"))
from f25_final_provenance import load,check_pair,ProvenanceViolation,evidence_report,TARGETS
class FinalProvenanceTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):cls.matrix,cls.proof=load()
    def reject(self,fn):
        m=copy.deepcopy(self.matrix);p=copy.deepcopy(self.proof)
        fn(m,p)
        with self.assertRaises(ProvenanceViolation):check_pair(m,p)
    def test_canonical(self):self.assertTrue(check_pair(self.matrix,self.proof))
    def test_proof_name_mutation(self):self.reject(lambda m,p:p["types"][0].update(fullName="FMRTE.Imaginary"))
    def test_proof_token_mutation(self):self.reject(lambda m,p:p["types"][0].update(token="0x020000ff"))
    def test_proof_duplicate(self):self.reject(lambda m,p:p["types"][1].update(token=p["types"][0]["token"]))
    def test_proof_omission(self):self.reject(lambda m,p:p["types"].pop())
    def test_proof_extra_property(self):self.reject(lambda m,p:p["types"][0].update(memoryOffset=1))
    def test_catalog_digest(self):self.reject(lambda m,p:p.update(sourceCatalogSha256="0"*64))
    def test_zip_digest(self):self.reject(lambda m,p:p.update(sourceZipSha256="0"*64))
    def test_exe_digest(self):self.reject(lambda m,p:p.update(sourceExecutableSha256="0"*64))
    def test_approvals(self):self.reject(lambda m,p:p["security"].update(approved=True))
    def test_inferred_promoted(self):self.reject(lambda m,p:m["rows"][0].update(equivalenceStatus="OBSERVED"))
    def test_omitted_artifact(self):self.reject(lambda m,p:m["source"]["observedArtifacts"].pop())
    def test_artifact_substitution(self):self.reject(lambda m,p:m["source"]["observedArtifacts"][0].__class__ and m["source"]["observedArtifacts"].__setitem__(0,"src/FMRTE2008.Domain/Other.cs"))
    def test_reordered_artifacts(self):self.reject(lambda m,p:m["source"]["observedArtifacts"].reverse())
    def test_source_count(self):self.reject(lambda m,p:p.update(sourceCatalogTypeCount=99))
    def test_missing_source_type(self):self.reject(lambda m,p:m["rows"][0].update(historicalToken="0x020000fe"))
    def test_false_review(self):self.reject(lambda m,p:m["security"].update(humanReviewed=True))
    def test_false_operational(self):self.reject(lambda m,p:m["security"].update(operational=True))
    def test_fake_risk_acceptance(self):self.reject(lambda m,p:m["security"].update(acceptedRisks=8))
    def test_execution_not_human(self):
        report=evidence_report(self.matrix,self.proof,{"GITHUB_RUN_ID":"123","GITHUB_SHA":"a"*40})
        self.assertFalse(report["checks"]["githubChecksAreHumanApproval"])
        self.assertFalse(report["checks"]["originalZipReverifiedInCI"])
        self.assertEqual(report["decision"]["humanDecision"],"NOT_MADE")
if __name__=="__main__":unittest.main(verbosity=2)
