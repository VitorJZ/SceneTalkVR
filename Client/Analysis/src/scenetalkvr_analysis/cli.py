from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

from .bundle_reader import BundleError
from .dictionary import rows as dictionary_rows
from .exports import write_csv, write_json
from .pipeline import AnalysisError, analyze_batch, analyze_bundle, validate_bundle
from .report import markdown_qc


def parser() -> argparse.ArgumentParser:
    root=argparse.ArgumentParser(prog="scenetalkvr_analysis");commands=root.add_subparsers(dest="command",required=True)
    validate=commands.add_parser("validate-bundle");validate.add_argument("bundle");validate.add_argument("--json-output")
    analyze=commands.add_parser("analyze-bundle");analyze.add_argument("bundle");analyze.add_argument("--config");analyze.add_argument("--output",required=True)
    batch=commands.add_parser("analyze-batch");batch.add_argument("root");batch.add_argument("--config");batch.add_argument("--output",required=True)
    dictionary=commands.add_parser("build-dictionary");dictionary.add_argument("--output",required=True)
    qc=commands.add_parser("qc-report");qc.add_argument("root");qc.add_argument("--config");qc.add_argument("--output",required=True)
    return root


def main(argv: list[str] | None = None) -> int:
    args=parser().parse_args(argv)
    try:
        if args.command=="validate-bundle":
            value=validate_bundle(args.bundle)
            if args.json_output: write_json(Path(args.json_output),value)
        elif args.command=="analyze-bundle": value=analyze_bundle(args.bundle,args.output,args.config)
        elif args.command=="analyze-batch": value=analyze_batch(args.root,args.output,args.config)
        elif args.command=="build-dictionary":
            data=dictionary_rows();write_csv(Path(args.output),data);value={"status":"PASS","fieldCount":len(data),"output":args.output}
        else:
            batch=analyze_batch(args.root,Path(args.output)/"sessions",args.config);summary={"bundleCount":batch["bundleCount"],"analyzedCount":batch["analyzedCount"],"errorCount":batch["errorCount"]};Path(args.output).mkdir(parents=True,exist_ok=True);(Path(args.output)/"qc_report.md").write_text(markdown_qc(summary),encoding="utf-8");write_json(Path(args.output)/"qc_report.json",summary);value=summary
        print(json.dumps(value,ensure_ascii=False,sort_keys=True));return 0
    except (AnalysisError,BundleError,ValueError,OSError) as exc:
        print(json.dumps({"status":"FAIL","error":str(exc)},ensure_ascii=False,sort_keys=True),file=sys.stderr);return 2
