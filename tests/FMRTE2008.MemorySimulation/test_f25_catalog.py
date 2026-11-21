"""F25.3 synthetic CLI metadata tests: no historic binaries in CI."""
import pathlib,struct,sys,unittest
sys.path.insert(0,str(pathlib.Path(__file__).resolve().parents[2]/"tools"))
from f25_catalog import catalog,StaticMetadataError
from test_f25_clr_metadata import fixture
def synthetic():
    b=fixture()
    def h(p,n):struct.pack_into("<H",b,p,n)
    def i(p,n):struct.pack_into("<I",b,p,n)
    def q(p,n):struct.pack_into("<Q",b,p,n)
    md=0x300;h(md+30,3);p=md+32
    for off,size,name in [(0x90,0xa0,b"#~\0"),(0x130,0xa0,b"#Strings\0"),(0x1d0,0x10,b"#Blob\0")]:
        i(p,off);i(p+4,size);b[p+8:p+8+len(name)]=name
        p=(p+8+len(name)+3)&~3
    ts=md+0x90;b[ts+4]=2
    q(ts+8,sum(1<<n for n in (2,6,32,35,40)))
    for idx in range(5):i(ts+24+idx*4,1)
    rows=ts+44
    strings=b"\0Player\0Demo\0Run\0FMRTE\0System\0Resource.resources\0"
    b[md+0x130:md+0x130+len(strings)]=strings
    def si(name):return strings.index(name.encode())
    struct.pack_into("<IHHHHH",b,rows,0,si("Player"),si("Demo"),0,1,1);rows+=14
    struct.pack_into("<IHHHHH",b,rows,0,0,0,si("Run"),0,1);rows+=14
    struct.pack_into("<IHHHHIHHH",b,rows,0,1,0,0,0,0,0,si("FMRTE"),0);rows+=22
    struct.pack_into("<HHHHIHHHH",b,rows,2,0,0,0,0,0,si("System"),0,0);rows+=20
    struct.pack_into("<IIHH",b,rows,0,1,si("Resource.resources"),0);rows+=12
    return b
class CatalogTests(unittest.TestCase):
    def test_positive(self):
        x=catalog(synthetic())
        self.assertEqual(x["counts"],dict(types=1,methods=1,assemblies=1,assemblyReferences=1,manifestResources=1))
        self.assertEqual(x["types"][0]["name"],"Player")
        self.assertEqual(x["methods"][0]["ownerType"],"0x02000001")
        self.assertEqual(x["assemblyReferences"][0]["name"],"System")
    def reject(self,mutate):
        data=synthetic();mutate(data)
        with self.assertRaises(StaticMetadataError):catalog(data)
    def test_string_index_outside(self):self.reject(lambda b:struct.pack_into("<H",b,0x300+0x90+44+4,0xffff))
    def test_string_without_terminator(self):
        self.reject(lambda b:b.__setitem__(slice(0x300+0x130,0x300+0x1d0),b"A"*0xa0))
    def test_orphan_method(self):self.reject(lambda b:struct.pack_into("<H",b,0x300+0x90+44+12,2))
    def test_missing_strings(self):
        self.reject(lambda b:b.__setitem__(slice(0x300+32+12+8,0x300+32+12+16),b"#Wrong\0\0"))
    def test_unsupported_table(self):self.reject(lambda b:struct.pack_into("<Q",b,0x300+0x90+8,1<<47))
    def test_invalid_utf8(self):self.reject(lambda b:b.__setitem__(0x300+0x130+1,0xff))
    def test_excessive_rows(self):self.reject(lambda b:struct.pack_into("<I",b,0x300+0x90+24,0xffffffff))
    def test_stream_overlap(self):self.reject(lambda b:struct.pack_into("<I",b,0x300+32+12,0x20))
    def test_truncated(self):
        with self.assertRaises(StaticMetadataError):catalog(synthetic()[:0x400])
if __name__=="__main__":unittest.main(verbosity=2)
