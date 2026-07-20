from .bundle_reader import SessionBundle

def integrity_status(bundle: SessionBundle) -> str: return str(bundle.manifest.get("integrityStatus", ""))
