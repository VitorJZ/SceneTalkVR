# Pilot Collection Assignment Schema

Allocator version: `1.2-collection`.

Input: `participantId`, `protocolVersion`, `pilotTaskCatalogVersion`, `assignmentAlgorithmVersion`.

Group selection: `stableHash(participantId + protocolVersion) % 3`.

| Group | Position 1 | Position 2 | Position 3 |
|---|---|---|---|
| A | Walk-in / Voice Only | Ordering / Floating Orb | Wrong Dish / Humanoid |
| B | Walk-in / Floating Orb | Ordering / Humanoid | Wrong Dish / Voice Only |
| C | Walk-in / Humanoid | Ordering / Voice Only | Wrong Dish / Floating Orb |

Persisted fields include `pilotAssignmentId`, `sequenceId`, `conditionPosition`, `embodimentCondition`, `taskId`, catalog/protocol/algorithm versions and creation UTC. Creation writes the complete mapping once. Resume reads it; Retry retains it and only increments attempt/new `pilotRunId`.
