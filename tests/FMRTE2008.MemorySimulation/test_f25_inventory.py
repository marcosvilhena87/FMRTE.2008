"""F25.1 deterministic negative tests; all archives synthetic and offline."""
import copy, io, pathlib, stat, sys, tempfile, unittest, zipfile
sys.path.insert(0,str(pathlib.Path(__file__).resolve().parents[2]/"tools"))
from f25_inventory import inspect, validate_manifest, InventoryError, safe_path

class F25InventoryTests(unittest.TestCase):
    def setUp(self):
        self.tmp=tempfile.TemporaryDirectory()
        self.root=pathlib.Path(self.tmp.name)
        self.sequence=0
        self.good=self.make_zip([("sample/",""),("sample/config.xml","<x/>")])
        self.expected=inspect(self.good)
    def tearDown(self): self.tmp.cleanup()
    def make_zip(self,items):
        self.sequence+=1
        buf=io.BytesIO()
        with zipfile.ZipFile(buf,"w",compression=zipfile.ZIP_DEFLATED) as archive:
            for entry in items:
                name,data=entry[:2]
                if len(entry)==3 and entry[2]=="symlink":
                    info=zipfile.ZipInfo(name);info.create_system=3
                    info.external_attr=(stat.S_IFLNK|0o777)<<16
                    archive.writestr(info,data)
                else: archive.writestr(name,data)
        output=self.root/f"case-{self.sequence}.zip"
        output.write_bytes(buf.getvalue())
        return output
    def reject_manifest(self,change):
        x=copy.deepcopy(self.expected);change(x)
        with self.assertRaises(InventoryError):validate_manifest(x,self.expected)
    def reject_archive(self,entries):
        with self.assertRaises(InventoryError):inspect(self.make_zip(entries))
    def test_canonical(self):self.assertTrue(validate_manifest(self.expected,inspect(self.good)))
    def test_archive_modified(self):
        with self.assertRaises(InventoryError):
            validate_manifest(self.expected,inspect(self.make_zip([("sample/",""),("sample/config.xml","<y/>")])))
    def test_zip_hash(self):self.reject_manifest(lambda x:x["artifact"].update(sha256="0"*64))
    def test_file_hash(self):self.reject_manifest(lambda x:x["entries"][1].update(sha256="0"*64))
    def test_size(self):self.reject_manifest(lambda x:x["entries"][1].update(bytes=0))
    def test_crc(self):self.reject_manifest(lambda x:x["entries"][1].update(crc32="00000000"))
    def test_entry_count(self):self.reject_manifest(lambda x:x["artifact"].update(entries=1))
    def test_missing_member(self):self.reject_manifest(lambda x:x["entries"].pop())
    def test_duplicate_member_in_manifest(self):self.reject_manifest(lambda x:x["entries"].append(copy.deepcopy(x["entries"][1])))
    def test_false_approval(self):self.reject_manifest(lambda x:x["security"].update(approved=True))
    def test_false_operational(self):self.reject_manifest(lambda x:x["security"].update(operational=True))
    def test_false_human_review(self):self.reject_manifest(lambda x:x["security"].update(humanDecision="APPROVED"))
    def test_unexpected_field(self):self.reject_manifest(lambda x:x.update(humanAuthority="CI"))
    def test_traversal(self):self.reject_archive([("../outside.txt","x")])
    def test_nested_traversal(self):self.reject_archive([("a/../../outside.txt","x")])
    def test_absolute(self):self.reject_archive([("/tmp/file","x")])
    def test_drive(self):self.reject_archive([("C:/Windows/file","x")])
    def test_backslash(self):self.reject_archive([("..\\outside","x")])
    def test_duplicate_path(self):self.reject_archive([("a.txt","1"),("a.txt","2")])
    def test_case_collision(self):self.reject_archive([("A.txt","1"),("a.txt","2")])
    def test_symlink(self):self.reject_archive([("link","target","symlink")])
    def test_dot_component(self):
        with self.assertRaises(InventoryError):safe_path("a/./b")
    def test_empty_component(self):
        with self.assertRaises(InventoryError):safe_path("a//b")
    def test_directory_type(self):self.reject_manifest(lambda x:x["entries"][0].update(type="other-untrusted"))

if __name__=="__main__":unittest.main(verbosity=2)
