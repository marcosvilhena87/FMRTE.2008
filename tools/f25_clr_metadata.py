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

# ECMA-335 II.22: width-aware static table layout (no assembly loading).
CODED={
 "TypeDefOrRef":(2,(2,1,27)),"HasConstant":(2,(4,8,23)),
 "HasCustomAttribute":(5,(6,4,1,2,8,9,10,0,14,23,20,17,26,27,32,35,38,39,40,42,44,43)),
 "HasFieldMarshal":(1,(4,8)),"HasDeclSecurity":(2,(2,6,32)),
 "MemberRefParent":(3,(2,1,26,6,27)),"HasSemantics":(1,(20,23)),
 "MethodDefOrRef":(1,(6,10)),"MemberForwarded":(1,(4,6)),
 "Implementation":(2,(38,35,39)),"CustomAttributeType":(3,(6,10)),
 "ResolutionScope":(2,(0,26,35,1)),"TypeOrMethodDef":(1,(2,6)),
}
# H=u16, I=u32, S/G/B=heap indices, integer=table index, string=coded index.
SCHEMAS={
 0:("H","S","G","G","G"),1:("ResolutionScope","S","S"),
 2:("I","S","S","TypeDefOrRef",4,6),3:(4,),
 4:("H","S","B"),5:(6,),6:("I","H","H","S","B",8),
 7:(8,),8:("H","H","S"),9:(2,"TypeDefOrRef"),
 10:("MemberRefParent","S","B"),11:("H","HasConstant","B"),
 12:("HasCustomAttribute","CustomAttributeType","B"),
 13:("HasFieldMarshal","B"),14:("H","HasDeclSecurity","B"),
 15:("H","I",2),16:("I",4),17:("B",),
 18:(2,20),19:(20,),20:("H","S","TypeDefOrRef"),
 21:(2,23),22:(23,),23:("H","S","B"),
 24:("H",6,"HasSemantics"),25:(2,"MethodDefOrRef","MethodDefOrRef"),
 26:("S",),27:("B",),28:("H","MemberForwarded","S",26),
 29:("I",4),30:("I","I"),31:("I",),
 32:("I","H","H","H","H","I","B","S","S"),
 33:("I",),34:("I","I","I"),
 35:("H","H","H","H","I","B","S","S","B"),
 36:("I",35),37:("I","I","I",35),
 38:("I","S","B"),39:("I","I","S","S","Implementation"),
 40:("I","I","S","Implementation"),41:(2,2),
 42:("H","H","TypeOrMethodDef","S"),43:("MethodDefOrRef","B"),
 44:(42,"TypeDefOrRef"),
}
def table_sizes(data,table,endtable,valid,heap_flags,row_counts,first_row):
    check(heap_flags & ~0x07 == 0,"invalid heap-size flags")
    def width(col):
        if col in ("H","I"): return 2 if col=="H" else 4
        if col in ("S","G","B"):
            return 4 if heap_flags & {"S":1,"G":2,"B":4}[col] else 2
        if isinstance(col,int):return 4 if row_counts.get(col,0)>=65536 else 2
        bits,targets=CODED[col]
        return 4 if any(row_counts.get(t,0)>=1<<(16-bits) for t in targets) else 2
    cursor=first_row
    layout={}
    for idx in range(64):
        if not (valid>>idx)&1:continue
        check(idx in SCHEMAS,"unsupported metadata table layout")
        row_size=sum(width(c) for c in SCHEMAS[idx])
        rows=row_counts[idx]
        check(rows<=(endtable-cursor)//row_size,"metadata table data truncated/implausible row count")
        layout[TABLES.get(idx,"Table"+str(idx))]={"rows":rows,"rowBytes":row_size,"byteOffset":cursor-table}
        cursor+=rows*row_size
    check(cursor<=endtable,"metadata table data overflow")
    return layout

def read_pe(data):
    check(len(data)>=0x100 and data[:2]==b"MZ","missing DOS header")
    pe=num(data,0x3c,"I")
    check(pe>=64 and pe+24<=len(data),"invalid PE offset")
    check(data[pe:pe+4]==b"PE\x00\x00","invalid PE signature")
    machine=num(data,pe+4,"H"); count=num(data,pe+6,"H")
    check(1<=count<=96,"section count")
    opt=pe+24; optsize=num(data,pe+20,"H")
    check(num(data,opt,"H")==0x10b and optsize>=224 and opt+optsize<=len(data),"requires PE32")
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
    check(num(data,clr+4,"H")>=2,"invalid CLI major version")
    md_rva=num(data,clr+8,"I"); md_size=num(data,clr+12,"I")
    check(md_size>=24,"empty metadata")
    md=resolve(md_rva,md_size)
    check(data[md:md+4]==b"BSJB","invalid CLR metadata signature")
    check(num(data,md+8,"I")==0,"reserved CLR metadata root field not zero")
    version_size=num(data,md+12,"I")
    check(0<version_size<=256 and 16+version_size+4<=md_size,"invalid version string")
    version=data[md+16:md+16+version_size].split(b"\x00",1)[0].decode("ascii","replace")
    pos=md+16+version_size
    check(pos+4<=md+md_size,"metadata header truncated")
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
    directory_end=pos-md
    check(all(off>=directory_end for off,_ in ranges),"stream overlaps metadata header/directory")
    table_name="#~" if "#~" in streams else "#-" if "#-" in streams else None
    check(table_name is not None,"no metadata table stream")
    table=md+streams[table_name]["offset"]
    endtable=table+streams[table_name]["size"]
    check(table+24<=endtable,"truncated table header")
    check(num(data,table,"I")==0,"metadata table reserved header")
    check(num(data,table+4,"B")>=1,"metadata table major version")
    heap_flags=num(data,table+6,"B")
    valid=num(data,table+8,"Q");pointer=table+24; counts={}; row_counts={}
    for i in range(64):
        if (valid>>i)&1:
            check(pointer+4<=endtable,"table rows truncated")
            rows=num(data,pointer,"I")
            counts[TABLES.get(i,"Table"+str(i))]=rows
            row_counts[i]=rows
            pointer+=4
    layout=table_sizes(data,table,endtable,valid,heap_flags,row_counts,pointer)
    return dict(kind="f25-pe-clr-static-metadata",format="PE32",machine=f"0x{machine:04x}",
        clrHeaderRva=clr_rva,clrHeaderSize=clr_size,metadataRva=md_rva,
        metadataSize=md_size,metadataVersion=version,
        clrFlags=f"0x{num(data,clr+16,'I'):08x}",
        entryPointToken=f"0x{num(data,clr+20,'I'):08x}",
        sections=sections,streams=streams,tables=counts,tableLayout=layout)

def inspect_zip(path,member,expected_zip_sha256):
    check(bool(re.fullmatch(r"[0-9a-f]{64}",expected_zip_sha256)),"expected ZIP SHA-256 format")
    path=Path(path)
    check(path.is_file() and path.stat().st_size<=100*1024*1024,"archive too large/missing")
    with path.open("rb") as stream:
        digest=hashlib.sha256()
        for chunk in iter(lambda:stream.read(1024*1024),b""):digest.update(chunk)
        check(digest.hexdigest()==expected_zip_sha256,"archive identity mismatch")
        stream.seek(0)
        with zipfile.ZipFile(stream) as archive:
            members=[x for x in archive.infolist() if x.filename==member]
            check(len(members)==1 and 0<=members[0].file_size<=20_000_000,"missing/oversized member")
            check(members[0].compress_type in (zipfile.ZIP_STORED,zipfile.ZIP_DEFLATED),"compression unsupported")
            payload=bytearray()
            with archive.open(members[0]) as source:
                while True:
                    block=source.read(1024*1024)
                    if not block:break
                    check(len(payload)+len(block)<=20_000_000,"member exceeds byte budget")
                    payload.extend(block)
    data=bytes(payload)
    return dict(binarySha256=hashlib.sha256(data).hexdigest(),
                binaryBytes=len(data),metadata=read_pe(data),
                executed=False,extracted=False,approved=False,operational=False,
                humanDecision="NOT_MADE")

def main():
    parser=argparse.ArgumentParser()
    parser.add_argument("zip",type=Path)
    parser.add_argument("--member",default="FMRTE v1.0.0.5/FMRTE.exe")
    parser.add_argument("--expected-zip-sha256",required=True)
    args=parser.parse_args()
    print(json.dumps(inspect_zip(args.zip,args.member,args.expected_zip_sha256),indent=2,ensure_ascii=False))

if __name__=="__main__":main()
