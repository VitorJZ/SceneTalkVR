# SceneTalkVR History Export Receiver

This local-only service receives PICO experiment-history snapshots through the `adb reverse` mapping on port `8789` and writes:

- `experiment_history.json`
- `questionnaire_records.xlsx`

Use the unified launcher from the repository root instead of starting this service directly:

```bash
python Server/gateway-launcher/scenetalk_gateway_launcher.py
```

The receiver binds to `127.0.0.1` by default. The launcher passes the output directory through `SCENETALK_EXPORT_DIR`; the default is `Documents/SceneTalkVRExports`.

The Excel workbook keeps the raw `Questionnaires`, `Responses`, and `Scores` sheets and adds:

- `FormalSceneStats`: one terminal Formal condition questionnaire per row, with scored questionnaire items in columns and numeric `-1` for missing scores.
- `FormalRankingStats`: one row per participant, using the latest completed Formal final ranking when duplicate history exists; includes condition-to-task mappings, NE/NR/SE/SR ranks, and the selected preference with its reason.

Both summary sheets exclude Pilot records, sort by completion time, and display completion timestamps in China Standard Time as `dd/mm/yyyy hh:mm:ss`.

Run its isolated tests with:

```bash
python -m unittest discover -s Server/history-export-receiver/tests -v
```
