# Experiment v1.1 Stage 3 Event Schema

Authoritative timing stream: `<participantId>_<sessionId>_events_v1.jsonl` under `Application.persistentDataPath/SceneTalkVR/ExperimentLogs`. One event is appended when the runtime event occurs; the legacy turn JSONL/CSV remains a compatibility summary.

Schema version: `1.0`.

## Common fields

| Field | Type | Meaning |
|---|---:|---|
| `schemaVersion` | string | Event schema version (`1.0`) |
| `timestampUtc` | ISO-8601 string | UTC wall-clock time at emission |
| `monotonicElapsedMs` | integer | Monotonic milliseconds since `BeginTurn`; ordering/latency authority |
| `participantId`, `sessionId`, `turnId`, `turnIndex` | string/int | Run linkage |
| `condition` | enum string | `NE`, `NR`, `SE`, or `SR` |
| `provider`, `style`, `taskId` | string | Resolved experimental context |
| `eventType` | enum string | Event listed below |
| `technicalValidity` | enum string | `Valid`, `Retry`, `FallbackUsed`, `TechnicalInvalid` |
| `failureStage`, `reason`, `fallback` | string | Empty unless a real failure/fallback occurred |
| `actualPlaybackActor` | string | `Avatar` or `Agent`, populated at playback |
| `voiceProfile`, `speakingSpeed`, `volume` | string/string/float | Effective playback settings, not a hard-coded condition label |
| `subtitlePolicy` | string | Effective subtitle policy |
| `feedbackTextHash` | SHA-256 hex | Hash of the exact feedback/recast unit |

## Event types

`UserSpeechEnded`, `CorrectionRequestStarted`, `CorrectionFirstToken`, `CorrectionTextReady`, `CorrectionTtsStarted`, `CorrectionTtsReady`, `CorrectionPlaybackStarted`, `CorrectionPlaybackEnded`, `DialogueRequestStarted`, `DialogueFirstToken`, `DialogueFirstSentenceReady`, `DialogueTtsStarted`, `DialogueFirstTtsReady`, `DialoguePlaybackStarted`, `DialoguePlaybackEnded`, `DialogueGateClosed`, `DialogueGateOpened`, `TurnCompleted`, `TurnTechnicalInvalid`.

`CorrectionFirstToken` is emitted by the first response-byte callback of the correction HTTP download handler. Streaming dialogue emits `DialogueFirstToken` from its first received chunk and `DialogueFirstSentenceReady` from the incremental JSON sentence parser. TTS and playback events are emitted by their actual preparation/playback callbacks.

## Recomputable summary

All missing measurements use `-1`, never a fabricated zero or `"none"`.

| Metric | Raw-event formula |
|---|---|
| `userEndToFeedbackAudioMs` | `CorrectionPlaybackStarted - UserSpeechEnded` |
| `userEndToDialogueAudioMs` | `DialoguePlaybackStarted - UserSpeechEnded` |
| `feedbackToDialogueGapMs` | `DialoguePlaybackStarted - CorrectionPlaybackEnded` |
| `correctionGenerationMs` | `CorrectionTextReady - CorrectionRequestStarted` |
| `dialogueFirstSentenceGenerationMs` | `DialogueFirstSentenceReady - DialogueRequestStarted` |
| `correctionTtsMs` | `CorrectionTtsReady - CorrectionTtsStarted` |
| `dialogueFirstTtsMs` | `DialogueFirstTtsReady - DialogueTtsStarted` |

UTC is for cross-file correlation. Latency calculations must use `monotonicElapsedMs`.
