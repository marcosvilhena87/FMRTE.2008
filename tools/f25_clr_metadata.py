"""F25.2 — read-only PE32/CLR metadata inventory; never executes the input."""
from __future__ import annotations
import argparse, hashlib, json, re, struct, zipfile
from pathlib import Path

class StaticMetadataError(ValueError): pass

TABLES={0:"Module",1:"TypeRef",2:"TypeDef",4:"Field",6:"MethodDef",8:"Param",
9:"InterfaceImpl",10:"MemberRef",11:"Constant",12:"CustomAttribute",
17:"StandAloneSig",20:"Event",21:"PropertyMap",23:"Property",
24:"MethodSemantics",26:"ModuleRef",27:"TypeSpec",28:"ImplMap",
32:"Assembly",35:"AssemblyRef",40:"ManifestResource",41:"NestedClass"}

def check(value, reason):
    if not value: raise StaticMetadataError(reason)

def num(data, pos, fmt):
    width=struct.calcsize(fmt)
    check(isinstance(pos,int) and 0<=pos and pos+width<=len(data),"out of bounds")
    return struct.unpack_from("<"+fmt,data,pos)[0]

def read_pe(data):
    check(len(data)>=0x100 and data[:2]==b"MZ","missing DOS header")
    pe=num(data,0x3c,"I")
    check(data[pe:pe+4]==b"PE\x00\x00","invalid PE signature")
    machine=num(data,pe+4,"H"); count=num(data,pe+6,"H")
    check(1<=count<=96,"section count")
    opt=pe+24; optsize=num(data,pe+20,"H")
    check(num(data,opt,"H")==0x10b and optsize>=224,"requires PE32")
    check(num(data,opt+92,"I")>=15,"missing CLR data directory")
    secstart=opt+optsize; sections=[]
    for i in range(count):
        pos=secstart+40*i
        check(pos+40<=len(data),"section table truncated")
        name=data[pos:pos+8].split(b"\x00",1)[0].decode("ascii","replace")
        virtual_size=num(data,pos+8,"I"); rva=num(data,pos+12,"I")
        size=num(data,pos+16,"I"); offset=num(data,pos+20,"I")
        check(offset+size<=len(data),"section outside file")
        sections.append(dict(name=name,rva=rva,virtualSize=virtual_size,rawSize=size,rawOffset=offset))
    def resolve(rva,size):
        for s in sections:
            delta=rva-s["rva"]
            if delta>=0 and delta+size<=s["rawSize"]:
                pos=s["rawOffset"]+delta
                check(pos+size<=len(data),"invalid RVA")
                return pos
        raise StaticMetadataError("unmapped RVA")
    clr_rva=num(data,opt+96+14*8,"I");clr_size=num(data,opt+96+14*8+4,"I")
    check(clr_rva and clr_size>=72,"missing CLR header")
    clr=resolve(clr_rva,72)
    check(num(data,clr,"I")>=72,"invalid CLR header size")
    md_rva=num(data,clr+8,"I"); md_size=num(data,clr+12,"I")
    check(md_size>=24,"empty metadata")
    md=resolve(md_rva,md_size)
    check(data[md:md+4]==b"BSJB","invalid CLR metadata signature")
    version_size=num(data,md+12,"I")
    check(0<version_size<=256 and 16+version_size+4<=md_size,"invalid version string")
    version=data[md+16:md+16+version_size].split(b"\x00",1)[0].decode("ascii","replace")
    pos=md+16+version_size
    count_streams=num(data,pos+2,"H");pos+=4
    check(1<=count_streams<=32,"invalid stream count")
    streams={}; ranges=[]
    for _ in range(count_streams):
        off=num(data,pos,"I"); size=num(data,pos+4,"I")
        name_at=pos+8
        end=data.find(b"\x00",name_at,min(name_at+64,md+md_size))
        check(end>=0,"unterminated stream name")
        name=data[name_at:end].decode("ascii","replace")
        check(name and name not in streams and off+size<=md_size,"invalid/duplicate stream")
        check(not any(off<right and off+size>left for left,right in ranges),"overlapping streams")
        ranges.append((off,off+size));streams[name]=dict(offset=off,size=size)
        pos=(end+4)&~3
        check(pos<=md+md_size,"stream directory outside metadata")
    table_name="#~" if "#~" in streams else "#-" if "#-" in streams else None
    check(table_name is not None,"no metadata table stream")
    table=md+streams[table_name]["offset"]
    endtable=table+streams[table_name]["size"]
    check(table+24<=endtable,"truncated table header")
    valid=num(data,table+8,"Q");pointer=table+24; counts={}
    for i in range(64):
        if (valid>>i)&1:
            check(pointer+4<=endtable,"table rows truncated")
            counts[TABLES.get(i,"Table"+str(i))]=num(data,pointer,"I")
            pointer+=4
    return dict(kind="f25-pe-clr-static-metadata",format="PE32",machine=f"0x{machine:04x}",
        clrHeaderRva=clr_rva,clrHeaderSize=clr_size,metadataRva=md_rva,
        metadataSize=md_size,metadataVersion=version,
        clrFlags=f"0x{num(data,clr+16,'I'):08x}",
        entryPointToken=f"0x{num(data,clr+20,'I'):08x}",
        sections=sections,streams=streams,tables=counts)

def inspect_zip(path, member):
    with zipfile.ZipFile(path) as z:
        members=[x for x in z.infolist() if x.filename==member]
        check(len(members)==1 and members[0].file_size<=20_000_000,"missing/oversized member")
        data=z.read(members[0])
    return dict(binarySha256=hashlib.sha256(data).hexdigest(),
                binaryBytes=len(data),metadata=read_pe(data),
                executed=False,extracted=False,approved=False,operational=False,
                humanDecision="NOT_MADE")

def main():
    p=argparse.ArgumentParser()
    p.add_argument("zip",type=Path)
    p.add_argument("--member",default="FMRTE v1.0.0.5/FMRTE.exe")
    p.add_argument("--expected-zip-sha256",required=True)
    args=p.parse_args()
    raw=args.zip.read_bytes()
    check(hashlib.sha256(raw).hexdigest()==args.expected_zip_sha256,"archive identity mismatch")
    print(json.dumps(inspect_zip(args.zip,args.member),indent=2,ensure_ascii=False))

if __name__=="__main__":main()
