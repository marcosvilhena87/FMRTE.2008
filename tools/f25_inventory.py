"""F25.1 static ZIP inventory. Read bytes only: no extract, execution or subprocess."""
import argparse, hashlib, json, re, stat, zipfile
from pathlib import Path

class InventoryError(ValueError): pass
def ensure(ok, reason):
    if not ok: raise InventoryError(reason)

def safe_path(name):
    ensure(isinstance(name,str) and bool(name), "empty ZIP member")
    ensure("\\" not in name and "\x00" not in name, "unsafe ZIP separator")
    ensure(not name.startswith("/") and not re.match(r"^[A-Za-z]:",name), "absolute ZIP path")
    ensure(all(x not in ("",".","..") for x in name.rstrip("/").split("/")), "ZIP traversal/ambiguous member")
    return name

def classify(name, directory):
    if directory: return "directory"
    if name.lower().endswith(".exe"): return "executable-untrusted"
    if name.lower().endswith(".xml"): return "xml-data"
    if name.lower().endswith((".png",".jpg",".jpeg")): return "image-resource"
    return "other-untrusted"

def inspect(path):
    path=Path(path)
    ensure(path.is_file() and path.stat().st_size <= 100*1024*1024, "missing/oversized archive")
    h=hashlib.sha256()
    with path.open("rb") as f:
        for chunk in iter(lambda:f.read(1024*1024),b""): h.update(chunk)
    entries=[]; seen=set(); total=0
    with zipfile.ZipFile(path) as archive:
        ensure(len(archive.infolist()) <= 1000, "too many members")
        for info in archive.infolist():
            name=safe_path(info.filename)
            ensure(name.casefold() not in seen, "duplicate/case collision")
            seen.add(name.casefold())
            ensure(not (info.flag_bits&1), "encrypted entry")
            ensure(info.compress_type in (zipfile.ZIP_STORED,zipfile.ZIP_DEFLATED), "unsupported compression")
            ensure(not stat.S_ISLNK((info.external_attr>>16)&65535), "symlink")
            ensure(info.file_size <= 20*1024*1024, "oversized member")
            total+=info.file_size
            ensure(total<=100*1024*1024,"oversized expanded archive")
            ensure(info.is_dir()==name.endswith("/"),"directory mismatch")
            digest=hashlib.sha256(); count=0
            with archive.open(info) as stream:
                for block in iter(lambda:stream.read(1024*1024),b""):
                    count+=len(block)
                    ensure(count<=20*1024*1024,"unbounded expansion")
                    digest.update(block)
            ensure(count==info.file_size, "member size mismatch")
            entries.append(dict(path=name,type=classify(name,info.is_dir()),
                bytes=info.file_size,compressedBytes=info.compress_size,
                crc32=f"{info.CRC:08x}",sha256=digest.hexdigest()))
    return dict(schemaVersion=1,kind="f25-static-zip-inventory",
        artifact=dict(label="FMRTE v1.0.0.5(1).zip",sha256=h.hexdigest(),
                      bytes=path.stat().st_size,entries=len(entries),uncompressedBytes=total),
        security=dict(executed=False,extracted=False,operational=False,approved=False,
            approvedPlayerMaps=0,humanDecision="NOT_MADE",b03="PENDING-DECLARED",
            gateA12="GATE-BLOQUEADO",acceptedRisks=0),entries=entries)

def validate_manifest(actual,expected):
    ensure(isinstance(actual,dict) and set(actual)=={"schemaVersion","kind","artifact","security","entries"},"closed schema")
    ensure(actual==expected,"manifest mismatch: bytes, hashes, paths, metadata or security")
    return True

def main():
    p=argparse.ArgumentParser()
    p.add_argument("archive",type=Path)
    p.add_argument("--manifest",type=Path)
    p.add_argument("--generate",action="store_true")
    args=p.parse_args()
    inventory=inspect(args.archive)
    if args.generate: print(json.dumps(inventory,indent=2,ensure_ascii=False))
    else:
        ensure(args.manifest is not None,"--manifest required")
        validate_manifest(json.loads(args.manifest.read_text(encoding="utf-8")),inventory)
        print(f"PASS F25.1: {len(inventory['entries'])} entries; ZIP SHA256 {inventory['artifact']['sha256']}; static only")

if __name__=="__main__": main()
