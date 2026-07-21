# Pilot Data Integrity Report

Collection data uses `flowMode=pilot`, `runQualification=collection`, `dataOrigin=participant_collection`, `collectionEligible=true`, `developerTestAssignment=false`, `deploymentProfile=editor_collection`.

The final validation Bundle contains 13 files covering manifest, assignment, timing/study events, goals, questionnaires, ranking, integrity and checksums. `SessionDataIntegrityAuditor` reports PASS and checksum verification reports PASS.

Python analysis reports: 1 session, 3 assignments, 3 attempts, 3 condition summaries, 12 Goal rows, 9 questionnaire rows, 3 ranking rows and 0 exclusions. Primary-attempt policy is `latest_valid_completed_attempt`.

The deterministic manual validation injected final transcripts rather than recording live speech, so this sample contains zero analyzable real turn timing rows. It proves storage/linkage/export/integrity/analysis interoperability, but a live microphone/STT/TTS/LLM smoke run remains required before participant collection.
