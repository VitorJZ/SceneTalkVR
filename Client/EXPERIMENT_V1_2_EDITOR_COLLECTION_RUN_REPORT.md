# Experiment v1.2 Editor Collection Run Report

Date: 2026-07-21 UTC. Unity: 6000.3.16f1. Baseline: `a7e482c87458b4c93a3300d41728e6b0e9b6b14e`; functional implementation: `d56c01e1469ebc90227619dd5bc6d902e684100e` on `experiment-v1.1-integration`.

The current Editor and actual SampleScene were used. No second Editor was launched. The local gateway health endpoint returned HTTP 200 with provider `tencent`.

The Game View run verified arm, Start routing, hidden task mapping, Hotel startup, read-only Goal panel, the four Hotel intent phrases, automatic questionnaire opening, independent 1–7 selections, two-step confirmed Submit, return to mode choice, four condition completion, final ranking, completion, and Resume persistence. Console errors: 0.

Because Codex cannot provide physical microphone speech, the four intent phrases were injected at the production final-participant-transcript boundary. The coordinator was then marked `qaAutomationUsed=true`; the run is non-collection and export was correctly blocked. This is truthful QA evidence, not a fabricated participant bundle. A human microphone/STT smoke run and first collection bundle remain the operator’s last pre-enrollment check.

Screenshots are stored under `Client/EXPERIMENT_V1_2_EVIDENCE`. The required ten filenames are present. The Hotel, Furniture and Gym panoramas were generated with the built-in Codex imagegen skill as true 2:1 equirectangular scenes, then uniformly resized to 2048x1024 using Lanczos; they were not derived by stretching square images.
