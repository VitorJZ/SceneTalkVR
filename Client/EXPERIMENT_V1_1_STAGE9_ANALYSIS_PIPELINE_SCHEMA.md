# Experiment v1.1 Stage 9 Analysis Pipeline Schema

Analysis schema `1.0`; Python `>=3.11`; source package `scenetalkvr_analysis`.

## Safety boundary

The pipeline reads `manifest.json` first, validates SHA-256 and integrity, hashes every input before and after processing, and writes only to an independent output directory. The default configuration rejects ineligible, synthetic, developer-test, checksum-failed, integrity-failed, protocol-mismatched, or assignment-less bundles. Synthetic input is accepted only with `includeSyntheticForTesting=true`; provenance flags remain in every relevant output.

Collection data with `primaryAttemptPolicy=UNCONFIRMED` may produce QC and attempt-level tables, but `primaryAnalysisGenerated` remains false. Raw attempts are never overwritten. Transcript and aggregate interview text are excluded by default.

## Commands

```text
python -m scenetalkvr_analysis validate-bundle <bundle>
python -m scenetalkvr_analysis analyze-bundle <bundle> --config <config> --output <dir>
python -m scenetalkvr_analysis analyze-batch <root> --config <config> --output <dir>
python -m scenetalkvr_analysis build-dictionary --output <csv>
python -m scenetalkvr_analysis qc-report <root> --output <md>
```

Commands return non-zero on failure, print a human summary plus machine-readable JSON, and never emit partial success after a fatal validation error.

## Tables

- `sessions.csv`: provenance, versions, integrity, inclusion and exclusions.
- `assignments_long.csv`: sequence, condition/provider/style or embodiment, task, run/attempt IDs and validity.
- `turns_long.csv`: turn identity, feedback/actor/failure fields and seven event-derived latency measures.
- `condition_summary.csv`: counts, invalid/retry metrics, latency summaries, goal completion and duration.
- `goals_long.csv`: candidate/confirm/reject evidence and reviewer state.
- `questionnaire_items_long.csv`: raw/scored values, reverse flag, range, revision and linkage.
- `scale_scores.csv`: expected/answered/missing count, mean, sum and scorable flag for seven configured scales.
- `rankings_long.csv`, `interviews_long.csv`, `exclusions.csv`, and `all_attempts.csv`.

Formal condition derivation is fixed in code: NE=Non-Split/Dialogue Avatar+Explicit, NR=Non-Split/Dialogue Avatar+Recast, SE=Split/Assistant Agent+Explicit, SR=Split/Assistant Agent+Recast.

Timing uses only `monotonicElapsedMs`; absent events remain missing, never zero. Exported summaries are compared with recomputation using configured `timingToleranceMs`. Reverse scores are independently recomputed as `scaleMax + scaleMin - rawValue` against versioned catalog definitions.

The analysis manifest records source bundle and hashes, config hash, analysis/code/schema versions, generated UTC time, table counts, privacy flags and a deterministic output-content hash that excludes runtime timestamps and run IDs.
