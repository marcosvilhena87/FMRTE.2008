"""F25.2 static-only tests with synthetic PE/CLR bytes (no historical binary in CI)."""
import pathlib,struct,sys,unittest
sys.path.insert(0,str(pathlib.Path(__file__).resolve().parents[2]/"tools"))
from f25_clr_metadata import read_pe,StaticMetadataError,num

def fixture():
    b=bytearray(0x1200)
    def u16(p,n):struct.pack_into("<H",b,p,n)
    def u32(p,n):struct.pack_into("<I",b,p,n)
    def u64(p,n):struct.pack_into("<Q",b,p,n)
    b[:2]=b"MZ";u32(0x3c,0x80);b[0x80:0x84]=b"PE\0\0"
    u16(0x84,0x14c);u16(0x86,1);u16(0x94,224)
    opt=0x98;u16(opt,0x10b);u32(opt+92,16)
    u32(opt+96+14*8,0x2008);u32(opt+96+14*8+4,72)
    sh=opt+224;b[sh:sh+5]=b".text";u32(sh+8,0x1000)
    u32(sh+12,0x2000);u32(sh+16,0x1000);u32(sh+20,0x200)
    clr=0x208;u32(clr,72);u16(clr+4,2);u16(clr+6,5)
    u32(clr+8,0x2100);u32(clr+12,0x200)
    md=0x300;b[md:md+4]=b"BSJB"
    u32(md+12,12);b[md+16:md+28]=b"v2.0.50727\0\0"
    u16(md+30,1)
    u32(md+32,0x60);u32(md+36,0x40);b[md+40:md+43]=b"#~\0"
    table=md+0x60;u64(table+8,1|(1<<32))
    u32(table+24,1);u32(table+28,1)
    return b

class StaticTests(unittest.TestCase):
    def test_fixture(self):
        x=read_pe(bytes(fixture()))
        self.assertEqual(x["machine"],"0x014c")
        self.assertEqual(x["metadataVersion"],"v2.0.50727")
        self.assertEqual(x["tables"]["Module"],1)
        self.assertEqual(x["tables"]["Assembly"],1)
    def test_dos(self):
        b=fixture();b[0]=0
        with self.assertRaises(StaticMetadataError):read_pe(b)
    def test_pe(self):
        b=fixture();b[0x80]=0
        with self.assertRaises(StaticMetadataError):read_pe(b)
    def test_machine_format(self):
        b=fixture();b[0x98]=0
        with self.assertRaises(StaticMetadataError):read_pe(b)
    def test_unmapped_clr(self):
        b=fixture();struct.pack_into("<I",b,0x98+96+14*8,0x900000)
        with self.assertRaises(StaticMetadataError):read_pe(b)
    def test_bad_metadata_signature(self):
        b=fixture();b[0x300]=0
        with self.assertRaises(StaticMetadataError):read_pe(b)
    def test_stream_overlap(self):
        b=fixture();struct.pack_into("<H",b,0x300+30,2)
        struct.pack_into("<II",b,0x300+44,0x60,0x40)
        b[0x300+52:0x300+56]=b"#US\0"
        with self.assertRaises(StaticMetadataError):read_pe(b)
    def test_truncated(self):
        with self.assertRaises(StaticMetadataError):read_pe(bytes(fixture())[:0x400])
    def test_wrong_version_length(self):
        b=fixture();struct.pack_into("<I",b,0x300+12,600)
        with self.assertRaises(StaticMetadataError):read_pe(b)
    def test_bounds(self):
        with self.assertRaises(StaticMetadataError):num(b"abc",1,"I")

if __name__=="__main__":unittest.main(verbosity=2)
