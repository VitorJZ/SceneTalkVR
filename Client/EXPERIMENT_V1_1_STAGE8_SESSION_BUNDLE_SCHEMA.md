# Experiment v1.1 Stage 8 — Session Bundle Schema

```text
SessionBundle/
  manifest.json
  assignment/
  timing/
  study/
  questionnaire/
  ranking/
  interview/
  integrity/
  checksums.sha256
```

`manifest.json` records `bundleSchemaVersion`, `dataOrigin`, `collectionEligible`, `sessionMode`, participant/session IDs, Git commit, protocol/task/questionnaire/assignment versions, UTC creation time, file records, and integrity status. Each file record contains relative path, byte size and SHA-256.

`checksums.sha256` covers every bundle file other than itself using the standard `hash␠␠relative/path` format. Export fails when Assignment, Timing, Study, Questionnaire or Ranking data is missing. Existing destinations are never overwritten.

Synthetic inputs are staged under `Library/SceneTalkVR/.../SyntheticRaw` and exported under a separate `SyntheticBundles` tree. They always use:

```text
dataOrigin = synthetic_dry_run
collectionEligible = false
developerTestAssignment = true
```

The exporter copies inputs and never edits source files. It rejects obvious private-key, bearer-token and SiliconFlow environment-key material. Runtime enums remain represented by their serialized numeric values plus explicit human-readable labels in event and Assignment fields.

The integrity auditor reads the exported bundle and independently checks manifest/checksums, identity/version linkage, Assignment isolation, Feedback First timing, summary recomputation, condition/goal/retry closure, Questionnaire scoring/revisions, ranking completeness and interview linkage. It emits a separate PASS/WARNING/FAIL report without changing source data.
