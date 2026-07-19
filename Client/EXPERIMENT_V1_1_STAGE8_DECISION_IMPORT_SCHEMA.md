# Experiment v1.1 Stage 8 — Protocol Decision Intake Schema

The intake file is a draft transport format. It is not authoritative until every entry is explicitly approved and imported through `SceneTalkVR > Experiment > Protocol Decision Import`.

## Document

- `schemaVersion`: must equal `1.0`.
- `targetProtocolVersion`: protocol version reviewed by the research team.
- `decisions`: exactly the eleven required decision IDs, with no duplicate or unknown IDs.

Each decision contains `decisionId`, `proposedValue`, `allowedValues`, `confirmedBy`, `confirmedAtUtc`, `evidenceReference`, `notes`, and `approvalStatus`.

For a write, `approvalStatus` must be `Approved`; the confirmer, ISO-8601 UTC timestamp, and evidence reference must be non-empty. `allowedValues` documents the human input contract but cannot override code validation.

## Value validation

- Formal mapping: a one-to-one `a/b/c/d` mapping to `NE/NR/SE/SR`.
- Pilot mapping: a one-to-one `a/b/c` mapping to `voice_only/floating_orb/humanoid_agent`.
- Formal task policy: `strict_without_replacement`, `with_replacement`, or `manual`.
- Social Comfort: `included` or `excluded`.
- Pilot style: `explicit` or `recast`.
- Voice Only: `spatial_fixed_source` or `non_spatial_head_locked`.
- Maximum turns/duration: a positive integer or `unlimited`; zero is invalid.
- Questionnaire anchors: complete bilingual wording containing both `1 =` and `7 =`; `1-7` alone is invalid.

## Safe import transaction

1. Load and parse JSON.
2. Validate all eleven IDs, values, approval and provenance.
3. Preview every old/new value and source SHA-256 without changing the asset.
4. Require the operator confirmation phrase and an Editor confirmation dialog.
5. Save the prior asset JSON under `Library/SceneTalkVR/ProtocolBackups` with its SHA-256.
6. Apply all decisions, append the change log and increase `protocolVersion`.
7. Run locked protocol validation and save. Any exception restores the complete prior JSON.
8. Run Preflight. Existing Assignments become incompatible through their stored protocol version; they are never regenerated automatically.

The checked-in Stage 8 template remains Draft. It does not change `ExperimentV11Protocol.asset`.
