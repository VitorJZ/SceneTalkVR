# SceneTalkVR research-data analysis

Read-only, reproducible conversion of exported SceneTalkVR Session Bundles into QC reports and analysis-ready CSV tables. Python 3.11+ is supported; the runtime has no mandatory third-party dependencies.

From `Client/Analysis`:

```powershell
$env:PYTHONPATH="src"
python -m scenetalkvr_analysis validate-bundle <bundle>
python -m scenetalkvr_analysis analyze-bundle <bundle> --config config/analysis_config.template.json
python -m scenetalkvr_analysis analyze-batch <root> --config <config>
python -m scenetalkvr_analysis build-dictionary --output outputs/data_dictionary.csv
python -m scenetalkvr_analysis qc-report <root> --config <config> --output outputs/qc
python -m pytest
```

The default template excludes Synthetic and developer assignments. To inspect Stage 8/9 synthetic fixtures, copy the template and explicitly set `includeSyntheticForTesting` to `true` and `primaryAttemptPolicy` to a clearly test-only value such as `TEST_ONLY_LATEST_VALID`.

The pipeline never edits a source bundle. It records source and configuration hashes, omits transcript/free-text from ordinary aggregate tables, and blocks collection main-analysis generation while `primaryAttemptPolicy` is `UNCONFIRMED`.
