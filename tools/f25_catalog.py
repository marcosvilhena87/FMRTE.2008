"""F25.3 — offline PE/CLR symbol catalog; reads bytes, never loads or executes assembly."""
from __future__ import annotations
import argparse, hashlib, json, sys, zipfile
from pathlib import Path
sys.path.insert(0,str(Path(__file__).resolve().parent))
from f25_clr_metadata import read_pe, SCHEMAS, CODED, check, num, StaticMetadataError
MAX_ROWS=200000
def catalog(data:bytes):
    check(len(data)<=20000000,"binary budget")
    pe=read_pe(data);rva=pe["metadataRva"];length=pe["metadataSize"]
    sections=[s for s in pe["sections"] if s["rva"]<=rva and rva+length<=s["rva"]+s["rawSize"]]
    check(len(sections)==1,"ambiguous metadata RVA")
    root=sections[0]["rawOffset"]+rva-sections[0]["rva"]
    streams=pe["streams"]
    check("#Strings" in streams and "#Blob" in streams,"required heaps absent")
    def stream(name):
        s=streams[name];begin=root+s["offset"];end=begin+s["size"]
        check(end<=len(data),"heap bounds")
        return data[begin:end]
    strings=stream("#Strings")
    check(strings and strings[0]==0,"#Strings heap invalid")
    ts=stream("#~" if "#~" in streams else "#-")
    flags=ts[6];valid=num(ts,8,"Q");p=24;counts={}
    for i in range(64):
        if valid>>i&1:counts[i]=num(ts,p,"I");p+=4
    check(sum(counts.values())<=MAX_ROWS,"row budget")
    def width(col):
        if col=="H":return 2
        if col=="I":return 4
        if col in ("S","G","B"):return 4 if flags & {"S":1,"G":2,"B":4}[col] else 2
        if isinstance(col,int):return 4 if counts.get(col,0)>=65536 else 2
        bits,targets=CODED[col]
        return 4 if any(counts.get(t,0)>=1<<(16-bits) for t in targets) else 2
    selected={}
    for tid in range(64):
        if not valid>>tid&1:continue
        schema=SCHEMAS[tid];size=sum(width(c) for c in schema);n=counts[tid]
        check(p+n*size<=len(ts),"table bounds")
        if tid in (2,6,32,35,40):
            selected[tid]=[]
            for rid in range(n):
                pos=p+rid*size;cols=[]
                for col in schema:
                    w=width(col);cols.append(num(ts,pos,"H" if w==2 else "I"));pos+=w
                selected[tid].append((rid+1,cols))
        p+=n*size
    def string(index):
        check(0<=index<len(strings),"string index outside heap")
        end=strings.find(b"\0",index)
        check(end>=index,"unterminated heap string")
        try:return strings[index:end].decode("utf-8","strict")
        except UnicodeDecodeError as exc:raise StaticMetadataError("invalid UTF-8") from exc
    types=[dict(token=f"0x{0x02000000|rid:08x}",name=string(v[1]),namespace=string(v[2]),methodList=v[5],fieldList=v[4]) for rid,v in selected.get(2,[])]
    methods=[dict(token=f"0x{0x06000000|rid:08x}",name=string(v[3]),rva=v[0],flags=v[2],ownerType=None) for rid,v in selected.get(6,[])]
    for i,t in enumerate(types):
        start=t["methodList"];stop=types[i+1]["methodList"] if i+1<len(types) else len(methods)+1
        check(1<=start<=stop<=len(methods)+1,"bad TypeDef MethodList")
        for j in range(start-1,stop-1):methods[j]["ownerType"]=t["token"]
    check(all(m["ownerType"] is not None for m in methods),"orphan MethodDef")
    assemblies=[dict(token=f"0x{0x20000000|rid:08x}",name=string(v[7]),version=f"{v[1]}.{v[2]}.{v[3]}.{v[4]}",culture=string(v[8])) for rid,v in selected.get(32,[])]
    refs=[dict(token=f"0x{0x23000000|rid:08x}",name=string(v[6]),version=f"{v[0]}.{v[1]}.{v[2]}.{v[3]}",culture=string(v[7])) for rid,v in selected.get(35,[])]
    resources=[dict(token=f"0x{0x28000000|rid:08x}",name=string(v[2]),offset=v[0],flags=v[1],implementation=v[3]) for rid,v in selected.get(40,[])]
    return dict(schemaVersion=1,kind="f25-static-cli-symbol-catalog",
        identity=dict(executableSha256=hashlib.sha256(data).hexdigest(),size=len(data),metadataVersion=pe["metadataVersion"]),
        counts=dict(types=len(types),methods=len(methods),assemblies=len(assemblies),assemblyReferences=len(refs),manifestResources=len(resources)),
        assemblies=assemblies,assemblyReferences=refs,types=types,methods=methods,manifestResources=resources,
        security=dict(executed=False,operational=False,approved=False,humanDecision="NOT_MADE"))
def main():
    p=argparse.ArgumentParser();p.add_argument("archive",type=Path)
    p.add_argument("--expected-zip-sha256",required=True);p.add_argument("--output",type=Path)
    args=p.parse_args()
    check(args.archive.is_file() and args.archive.stat().st_size<=100000000,"missing/oversized archive")
    with args.archive.open("rb") as f:
        digest=hashlib.file_digest(f,"sha256").hexdigest()
    check(digest==args.expected_zip_sha256,"archive SHA mismatch")
    with zipfile.ZipFile(args.archive) as archive:
        matches=[i for i in archive.infolist() if i.filename=="FMRTE v1.0.0.5/FMRTE.exe"]
        check(len(matches)==1 and matches[0].file_size<=20000000,"missing/oversized executable")
        check(matches[0].compress_type in (zipfile.ZIP_STORED,zipfile.ZIP_DEFLATED),"unsupported compression")
        with archive.open(matches[0]) as src:
            chunks=[];total=0
            while True:
                chunk=src.read(1024*1024)
                if not chunk:break
                total+=len(chunk);check(total<=20000000,"decompression budget");chunks.append(chunk)
    result=catalog(b"".join(chunks))
    text=json.dumps(result,indent=2,ensure_ascii=False)+"\n"
    if args.output:args.output.write_text(text,encoding="utf-8")
    else:print(text)
if __name__=="__main__":main()
