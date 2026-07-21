# Experiment v1.2 Data Integrity Report

`EditorCollectionBundleExporter` validates collection identity before export. A bundle must be Formal/Collection, participant-origin, non-demo, non-synthetic, non-developer, non-QA, completed, and use `editor_collection`. Manifest data includes participant/session, protocol/resource snapshots, condition-task mapping, participant selection order, run IDs, goals/evidence, questionnaire linkage, ranking, file hashes and integrity status.

The analysis pipeline now defaults to `latest_valid_completed_attempt`. It retains every attempt, marks the final Valid+Completed attempt per condition as primary, and leaves TechnicalInvalid/non-primary attempts in the tables. Official collection analysis rejects an unconfirmed primary policy.

Validation:

- Python: 40/40 passed.
- Automated Unity bundle/schema/matrix and lifecycle regressions are included in 247 EditMode + 36 PlayMode passes.
- The automated Game View run was deliberately invalidated by `qaAutomationUsed`; export rejection `collection_bundle_identity_invalid` is the expected integrity result.
- No real participant bundle was generated, because transcript injection must not be represented as human collection.

The first human microphone run should export, audit checksums, run `Client/Analysis`, and confirm that original hashes remain unchanged before enrollment begins.
