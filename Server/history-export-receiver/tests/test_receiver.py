import importlib.util
import json
import sys
import tempfile
import threading
import unittest
import zipfile
from datetime import timedelta
from pathlib import Path
from urllib.request import Request, urlopen


MODULE_PATH = Path(__file__).resolve().parents[1] / "scenetalk_history_export_receiver.py"
SPEC = importlib.util.spec_from_file_location("scenetalk_history_export_receiver", MODULE_PATH)
receiver = importlib.util.module_from_spec(SPEC)
assert SPEC.loader is not None
sys.modules[SPEC.name] = receiver
SPEC.loader.exec_module(receiver)


def sample_bundle():
    return {
        "schemaVersion": "1.0",
        "exportId": "0123456789abcdef0123456789abcdef",
        "exportedAtUtc": "2026-07-29T06:30:00Z",
        "sortOrder": "chronological_ascending",
        "warnings": [],
        "experiments": [
            {
                "summary": {
                    "experimentId": "later",
                    "participantId": "参与者二",
                    "sessionId": "session-2",
                    "kind": 1,
                    "status": 2,
                    "createdAtUnixMs": 200,
                },
                "attempts": [],
                "conversations": [],
                "questionnaires": [],
                "rankings": [],
            },
            {
                "summary": {
                    "experimentId": "earlier",
                    "participantId": "参与者一",
                    "sessionId": "session-1",
                    "kind": 0,
                    "status": 1,
                    "createdAtUnixMs": 100,
                },
                "attempts": [
                    {"attemptId": "a2", "startedAtUnixMs": 20, "attemptIndex": 2},
                    {"attemptId": "a1", "startedAtUnixMs": 10, "attemptIndex": 1},
                ],
                "conversations": [
                    {
                        "summary": {"sessionId": "conversation", "createdAtUnixMs": 10},
                        "turns": [
                            {"sequenceIndex": 2, "createdAtUnixMs": 20},
                            {"sequenceIndex": 1, "createdAtUnixMs": 10},
                        ],
                    }
                ],
                "questionnaires": [
                    {
                        "questionnaireRecordId": "q1",
                        "attemptId": "a1",
                        "prompts": [
                            {
                                "itemId": "comfort",
                                "promptEnglish": "Comfort",
                                "promptChinese": "舒适度",
                            }
                        ],
                        "session": {
                            "protocolVersion": "1.1",
                            "questionnaireCatalogVersion": "1.0",
                            "questionnaireId": "condition",
                            "questionnaireVersion": "1.0",
                            "conditionRunId": "run-1",
                            "questionnaireLinkageKey": "link-1",
                            "conditionPosition": 0,
                            "taskId": "restaurant",
                            "completionStatus": 2,
                            "completionReason": "participant_submitted",
                            "startedAtUtc": "2026-07-29T06:00:00Z",
                            "submittedAtUtc": "2026-07-29T06:05:00Z",
                            "completionRate": 1.0,
                            "revision": 1,
                            "responses": [
                                {
                                    "sectionId": "presence",
                                    "itemId": "comfort",
                                    "rawValue": "很好",
                                    "responseCapturedAtUtc": "2026-07-29T06:04:00Z",
                                    "scoredValue": 5,
                                    "hasScoredValue": True,
                                }
                            ],
                            "sectionScores": [
                                {
                                    "sectionId": "presence",
                                    "mean": 5,
                                    "answeredCount": 1,
                                    "itemCount": 1,
                                    "hasMissing": False,
                                }
                            ],
                        },
                    },
                    {
                        "questionnaireRecordId": "q-skipped",
                        "attemptId": "a2",
                        "prompts": [],
                        "session": {
                            "questionnaireId": "condition",
                            "conditionRunId": "run-2",
                            "questionnaireLinkageKey": "link-2",
                            "startedAtUtc": "2026-07-29T06:10:00Z",
                            "skippedAtUtc": "2026-07-29T06:11:00Z",
                            "completionStatus": 6,
                            "completionReason": "participant_skipped",
                            "responses": [],
                            "sectionScores": [],
                        },
                    },
                ],
                "rankings": [],
            },
        ],
    }


def formal_statistics_bundle():
    return {
        "schemaVersion": "1.0",
        "exportId": "abcdef0123456789abcdef0123456789",
        "exportedAtUtc": "2026-07-29T08:00:00Z",
        "sortOrder": "chronological_ascending",
        "warnings": [],
        "questionnaireDefinitions": [
            {
                "questionnaireId": "formal_condition_v1",
                "questionnaireVersion": "1.0",
                "questionnaireCatalogVersion": "catalog-1",
                "items": [
                    {
                        "itemId": "clarity",
                        "displayOrder": 0,
                        "promptEnglish": "The feedback was clear.",
                        "promptChinese": "反馈内容很清楚。",
                        "itemType": 0,
                    },
                    {
                        "itemId": "continuity",
                        "displayOrder": 1,
                        "promptEnglish": "I could continue smoothly.",
                        "promptChinese": "我能顺畅地继续对话。",
                        "itemType": 0,
                    },
                ],
            },
            {
                "questionnaireId": "formal_final_v1",
                "questionnaireVersion": "1.0",
                "questionnaireCatalogVersion": "catalog-1",
                "items": [
                    {
                        "itemId": "formal_rank_01",
                        "displayOrder": 0,
                        "promptEnglish": "Rank the four conditions.",
                        "promptChinese": "请将四种反馈条件排序。",
                        "itemType": 2,
                        "choiceValues": ["NE", "NR", "SE", "SR"],
                    }
                ],
            },
        ],
        "experiments": [
            {
                "summary": {
                    "experimentId": "pilot",
                    "participantId": "pilot-participant",
                    "sessionId": "pilot-session",
                    "kind": 0,
                    "status": 2,
                    "createdAtUnixMs": 50,
                },
                "attempts": [],
                "conversations": [],
                "questionnaires": [
                    {
                        "questionnaireRecordId": "pilot-questionnaire",
                        "prompts": [],
                        "session": {
                            "questionnaireId": "formal_condition_v1",
                            "taskId": "must_not_export",
                            "completionStatus": 2,
                            "submittedAtUtc": "2026-07-29T01:00:00Z",
                            "responses": [],
                        },
                    }
                ],
                "rankings": [
                    {
                        "response": {
                            "questionnaireId": "formal_final_v1",
                            "submittedAtUtc": "2026-07-29T06:30:00Z",
                            "rankings": [
                                {"conditionCode": "NE", "rank": 1},
                                {"conditionCode": "NR", "rank": 2},
                                {"conditionCode": "SE", "rank": 3},
                                {"conditionCode": "SR", "rank": 4},
                            ],
                            "preferredConditionCode": "NE",
                            "reason": "旧排序不应导出",
                        }
                    },
                    {
                        "response": {
                            "questionnaireId": "formal_final_v1",
                            "submittedAtUtc": "2026-07-29T01:10:00Z",
                            "rankings": [],
                        }
                    }
                ],
            },
            {
                "summary": {
                    "experimentId": "formal-rehearsal",
                    "participantId": "formal-rehearsal-participant",
                    "sessionId": "formal-rehearsal-session",
                    "kind": 1,
                    "status": 2,
                    "createdAtUnixMs": 100,
                },
                "attempts": [
                    {
                        "attemptId": "rehearsal-ne",
                        "conditionKey": "NE",
                        "taskId": "hotel_check_in",
                        "status": 2,
                        "startedAtUnixMs": 100,
                        "endedAtUnixMs": 110,
                        "attemptIndex": 1,
                    }
                ],
                "conversations": [
                    {
                        "summary": {
                            "sessionId": "rehearsal-conversation",
                            "taskType": "hotel_check_in",
                            "experimentAttemptId": "rehearsal-ne",
                            "experimentRunId": "rehearsal-run-ne",
                            "turnCount": 3,
                            "correctionCount": 1,
                        },
                        "settings": {},
                        "turns": [],
                    }
                ],
                "questionnaires": [
                    {
                        "questionnaireRecordId": "rehearsal-questionnaire",
                        "attemptId": "rehearsal-ne",
                        "prompts": [],
                        "session": {
                            "questionnaireId": "formal_condition_v1",
                            "conditionRunId": "rehearsal-run-ne",
                            "formalCondition": 0,
                            "taskId": "hotel_check_in",
                            "completionStatus": 2,
                            "submittedAtUtc": "2026-07-29T05:00:00Z",
                            "dataOrigin": "rehearsal",
                            "collectionEligible": False,
                            "responses": [
                                {
                                    "itemId": "clarity",
                                    "scoredValue": 3,
                                    "hasScoredValue": True,
                                    "responseCapturedAtUtc": "2026-07-29T04:59:00Z",
                                }
                            ],
                        },
                    }
                ],
                "rankings": [
                    {
                        "response": {
                            "questionnaireId": "formal_final_v1",
                            "submittedAtUtc": "2026-07-29T06:00:00Z",
                            "rankings": [{"conditionCode": "NE", "rank": 1}],
                            "preferredConditionCode": "NE",
                            "reason": "彩排偏好",
                        }
                    }
                ],
            },
            {
                "summary": {
                    "experimentId": "formal-participant",
                    "participantId": "formal-participant",
                    "sessionId": "formal-session",
                    "kind": "Formal",
                    "status": 2,
                    "createdAtUnixMs": 200,
                },
                "attempts": [
                    {
                        "attemptId": "ne-old",
                        "conditionKey": "NE",
                        "taskId": "obsolete_hotel_task",
                        "status": 2,
                        "startedAtUnixMs": 100,
                        "endedAtUnixMs": 120,
                        "attemptIndex": 1,
                    },
                    {
                        "attemptId": "ne-final",
                        "conditionKey": "NE",
                        "taskId": "hotel_check_in",
                        "status": "Completed",
                        "startedAtUnixMs": 200,
                        "endedAtUnixMs": 220,
                        "attemptIndex": 2,
                    },
                    {
                        "attemptId": "nr-final",
                        "conditionKey": "NR",
                        "taskId": "furniture_shopping",
                        "status": 2,
                        "endedAtUnixMs": 230,
                        "attemptIndex": 1,
                    },
                    {
                        "attemptId": "se-final",
                        "conditionKey": "SE",
                        "taskId": "gym_membership",
                        "status": 2,
                        "endedAtUnixMs": 240,
                        "attemptIndex": 1,
                    },
                    {
                        "attemptId": "sr-final",
                        "conditionKey": "SR",
                        "taskId": "tourist_assistance",
                        "status": 2,
                        "endedAtUnixMs": 250,
                        "attemptIndex": 1,
                    },
                ],
                "conversations": [
                    {
                        "summary": {
                            "sessionId": "obsolete-conversation",
                            "taskType": "obsolete_hotel_task",
                            "experimentAttemptId": "ne-old",
                            "experimentRunId": "obsolete-run",
                            "turnCount": 99,
                            "correctionCount": 99,
                        },
                        "settings": {},
                        "turns": [],
                    },
                    {
                        "summary": {
                            "sessionId": "formal-ne-conversation",
                            "taskType": "hotel_check_in",
                            "experimentAttemptId": "ne-final",
                            "experimentRunId": "formal-run-ne",
                            "turnCount": 8,
                            "correctionCount": 3,
                        },
                        "settings": {},
                        "turns": [],
                    },
                    {
                        "summary": {
                            "sessionId": "formal-ne-previous-conversation",
                            "taskType": "hotel_check_in",
                            "experimentAttemptId": "ne-previous",
                            "experimentRunId": "formal-run-ne-previous",
                            "turnCount": 4,
                            "correctionCount": 2,
                        },
                        "settings": {},
                        "turns": [],
                    },
                    {
                        "summary": {
                            "sessionId": "formal-nr-conversation",
                            "taskType": "furniture_shopping",
                        },
                        "settings": {
                            "experimentAttemptId": "nr-final",
                            "experimentRunId": "formal-run-nr",
                            "condition": {
                                "scenarioId": "furniture_shopping",
                                "task": {"taskId": "furniture_shopping"},
                            },
                        },
                        "turns": [
                            {
                                "sequenceIndex": 1,
                                "payload": {"correctionFeedback": {"hasFeedback": True}},
                            },
                            {
                                "sequenceIndex": 2,
                                "payload": {"correctionFeedback": {"hasFeedback": False}},
                            },
                        ],
                    },
                ],
                "questionnaires": [
                    {
                        "questionnaireRecordId": "formal-submitted",
                        "attemptId": "ne-final",
                        "prompts": [],
                        "session": {
                            "questionnaireId": "formal_condition_v1",
                            "conditionRunId": "formal-run-ne",
                            "formalCondition": 0,
                            "taskId": "hotel_check_in",
                            "completionStatus": "Submitted",
                            "submittedAtUtc": "2026-07-29T06:05:00Z",
                            "responses": [
                                {
                                    "itemId": "clarity",
                                    "rawValue": "2",
                                    "scoredValue": 6,
                                    "hasScoredValue": True,
                                    "reverseScored": True,
                                    "revision": 1,
                                    "responseCapturedAtUtc": "2026-07-29T06:04:00Z",
                                }
                            ],
                        },
                    },
                    {
                        "questionnaireRecordId": "formal-skipped",
                        "attemptId": "nr-final",
                        "prompts": [],
                        "session": {
                            "questionnaireId": "formal_condition_v1",
                            "conditionRunId": "formal-run-nr",
                            "formalCondition": 1,
                            "taskId": "furniture_shopping",
                            "completionStatus": 6,
                            "skippedAtUtc": "2026-07-29T06:10:00Z",
                            "responses": [
                                {
                                    "itemId": "clarity",
                                    "scoredValue": 4,
                                    "hasScoredValue": True,
                                    "revision": 1,
                                    "responseCapturedAtUtc": "2026-07-29T06:09:00Z",
                                }
                            ],
                        },
                    },
                    {
                        "questionnaireRecordId": "formal-in-progress",
                        "prompts": [],
                        "session": {
                            "questionnaireId": "formal_condition_v1",
                            "taskId": "must_not_export",
                            "completionStatus": 1,
                            "responses": [],
                        },
                    },
                ],
                "rankings": [
                    {
                        "response": {
                            "questionnaireId": "formal_final_v1",
                            "submittedAtUtc": "2026-07-29T07:00:00Z",
                            "rankings": [
                                {"conditionCode": "NE", "rank": 2},
                                {"conditionCode": "NR", "rank": 1},
                                {"conditionCode": "SE", "rank": 3},
                            ],
                            "preferredConditionCode": "NR",
                            "reason": "更喜欢这种反馈方式",
                        }
                    }
                ],
            },
        ],
    }


class ReceiverTests(unittest.TestCase):
    def test_bundle_is_sorted_from_early_to_late(self):
        result = receiver.validate_and_sort_bundle(sample_bundle())
        self.assertEqual(["earlier", "later"], [x["summary"]["experimentId"] for x in result["experiments"]])
        self.assertEqual(["a1", "a2"], [x["attemptId"] for x in result["experiments"][0]["attempts"]])
        turns = result["experiments"][0]["conversations"][0]["turns"]
        self.assertEqual([1, 2], [x["sequenceIndex"] for x in turns])

    def test_formal_scene_statistics_include_all_formal_and_fill_missing_scores(self):
        bundle = receiver.validate_and_sort_bundle(formal_statistics_bundle())

        headers, rows = receiver.formal_scene_statistics(bundle)

        self.assertEqual("participantId", headers[0])
        self.assertEqual("完成时间", headers[1])
        self.assertEqual("taskId", headers[2])
        self.assertEqual("formalCondition", headers[3])
        self.assertEqual(["对话轮次", "纠错次数"], headers[4:6])
        self.assertIn("反馈内容很清楚。 [clarity]", headers)
        self.assertIn("我能顺畅地继续对话。 [continuity]", headers)
        self.assertEqual(
            ["formal-rehearsal-participant", "formal-participant", "formal-participant"],
            [row[0] for row in rows],
        )
        self.assertEqual("hotel_check_in", rows[0][2])
        self.assertEqual(["NE", "NE", "NR"], [row[3] for row in rows])
        self.assertEqual([3, 1], rows[0][4:6])
        self.assertEqual([12, 5], rows[1][4:6])
        self.assertEqual([2, 1], rows[2][4:6])
        self.assertEqual([3.0, -1], rows[0][6:8])
        self.assertEqual([6.0, -1], rows[1][6:8])
        self.assertEqual([4.0, -1], rows[2][6:8])
        questionnaire_rows, _, _ = receiver.workbook_rows(bundle)
        participant_index = receiver.QUESTIONNAIRE_HEADERS.index("participantId")
        questionnaire_index = receiver.QUESTIONNAIRE_HEADERS.index("questionnaireId")
        task_index = receiver.QUESTIONNAIRE_HEADERS.index("taskId")
        condition_index = receiver.QUESTIONNAIRE_HEADERS.index("formalCondition")
        questionnaire_conditions = {
            (row[participant_index], row[task_index]): row[condition_index]
            for row in questionnaire_rows
            if row[questionnaire_index] == "formal_condition_v1"
        }
        self.assertTrue(all(
            row[3] == questionnaire_conditions[(row[0], row[2])]
            for row in rows
        ))
        local_time = receiver.EXCEL_EPOCH + timedelta(days=rows[0][1].serial)
        self.assertEqual("2026-07-29 13:00:00", local_time.strftime("%Y-%m-%d %H:%M:%S"))
        self.assertNotIn("pilot-participant", [row[0] for row in rows])
        self.assertNotIn("must_not_export", [row[2] for row in rows])

    def test_formal_ranking_statistics_map_tasks_ranks_and_preference(self):
        bundle = receiver.validate_and_sort_bundle(formal_statistics_bundle())

        headers, rows = receiver.formal_ranking_statistics(bundle)

        self.assertEqual(["participantId", "完成时间", "taskId"], headers[:3])
        self.assertEqual("偏好内容", headers[-1])
        self.assertTrue(all("请将四种反馈条件排序。" in header for header in headers[3:7]))
        self.assertEqual(
            ["formal-rehearsal-participant", "formal-participant"],
            [row[0] for row in rows],
        )
        participant = rows[1]
        self.assertEqual(
            "NE=hotel_check_in; NR=furniture_shopping; SE=gym_membership; SR=tourist_assistance",
            participant[2],
        )
        self.assertEqual([2, 1, 3, -1], participant[3:7])
        self.assertIn("首选条件=NR", participant[-1])
        self.assertIn("首选taskId=furniture_shopping", participant[-1])
        self.assertIn("理由=更喜欢这种反馈方式", participant[-1])
        self.assertNotIn("旧排序不应导出", participant[-1])
        self.assertNotIn("obsolete_hotel_task", participant[2])
        self.assertNotIn("pilot-participant", [row[0] for row in rows])

    def test_export_writes_json_and_excel_atomically_and_is_idempotent(self):
        with tempfile.TemporaryDirectory() as folder:
            config = receiver.ReceiverConfig("127.0.0.1", 8789, Path(folder))
            first = receiver.write_export(config, sample_bundle())
            second = receiver.write_export(config, sample_bundle())
            self.assertEqual(first["exportDirectory"], second["exportDirectory"])
            self.assertEqual(2, first["experimentCount"])
            self.assertEqual(2, first["questionnaireCount"])
            self.assertEqual(1, first["responseCount"])

            export_dir = Path(first["exportDirectory"])
            self.assertEqual(
                {"experiment_history.json", "questionnaire_records.xlsx"},
                {path.name for path in export_dir.iterdir()},
            )
            stored = json.loads((export_dir / "experiment_history.json").read_text(encoding="utf-8"))
            self.assertEqual("earlier", stored["experiments"][0]["summary"]["experimentId"])
            with zipfile.ZipFile(export_dir / "questionnaire_records.xlsx") as workbook:
                names = set(workbook.namelist())
                for index in range(1, 6):
                    self.assertIn(f"xl/worksheets/sheet{index}.xml", names)
                workbook_xml = workbook.read("xl/workbook.xml").decode("utf-8")
                self.assertIn('name="FormalSceneStats"', workbook_xml)
                self.assertIn('name="FormalRankingStats"', workbook_xml)
                questionnaire_xml = workbook.read("xl/worksheets/sheet1.xml").decode("utf-8")
                response_xml = workbook.read("xl/worksheets/sheet2.xml").decode("utf-8")
                self.assertIn("participant_skipped", questionnaire_xml)
                self.assertIn("舒适度", response_xml)
                self.assertIn("很好", response_xml)

    def test_formal_statistics_workbook_contains_native_dates_and_five_sheets(self):
        with tempfile.TemporaryDirectory() as folder:
            path = Path(folder) / "questionnaire_records.xlsx"
            receiver.write_xlsx(path, receiver.validate_and_sort_bundle(formal_statistics_bundle()))

            with zipfile.ZipFile(path) as workbook:
                scene_xml = workbook.read("xl/worksheets/sheet4.xml").decode("utf-8")
                ranking_xml = workbook.read("xl/worksheets/sheet5.xml").decode("utf-8")
                styles_xml = workbook.read("xl/styles.xml").decode("utf-8")
                self.assertIn('numFmtId="164" formatCode="dd/mm/yyyy hh:mm:ss"', styles_xml)
                self.assertIn('s="2"', scene_xml)
                self.assertIn('xSplit="6" ySplit="1"', scene_xml)
                self.assertIn("formalCondition", scene_xml)
                self.assertIn("对话轮次", scene_xml)
                self.assertIn("纠错次数", scene_xml)
                self.assertIn("反馈内容很清楚。", scene_xml)
                self.assertIn("请将四种反馈条件排序。", ranking_xml)
                self.assertIn("首选taskId=furniture_shopping", ranking_xml)
                self.assertIn('<col min="3" max="3" width="84"', ranking_xml)

    def test_same_export_id_with_different_content_is_rejected(self):
        with tempfile.TemporaryDirectory() as folder:
            config = receiver.ReceiverConfig("127.0.0.1", 8789, Path(folder))
            receiver.write_export(config, sample_bundle())
            changed = sample_bundle()
            changed["warnings"] = ["changed"]
            with self.assertRaisesRegex(receiver.ExportError, "different content"):
                receiver.write_export(config, changed)

    def test_invalid_schema_is_rejected_without_output(self):
        with tempfile.TemporaryDirectory() as folder:
            payload = sample_bundle()
            payload["schemaVersion"] = "2.0"
            config = receiver.ReceiverConfig("127.0.0.1", 8789, Path(folder))
            with self.assertRaisesRegex(receiver.ExportError, "Expected history export schema"):
                receiver.write_export(config, payload)
            self.assertEqual([], list(Path(folder).iterdir()))

    def test_empty_history_is_rejected_without_output(self):
        with tempfile.TemporaryDirectory() as folder:
            payload = sample_bundle()
            payload["experiments"] = []
            config = receiver.ReceiverConfig("127.0.0.1", 8789, Path(folder))
            with self.assertRaisesRegex(receiver.ExportError, "no experiment history"):
                receiver.write_export(config, payload)
            self.assertEqual([], list(Path(folder).iterdir()))

    def test_http_health_and_export_endpoints(self):
        with tempfile.TemporaryDirectory() as folder:
            config = receiver.ReceiverConfig("127.0.0.1", 0, Path(folder))

            class Handler(receiver.HistoryExportHandler):
                receiver_config = config

            server = receiver.ThreadingHTTPServer((config.host, 0), Handler)
            thread = threading.Thread(target=server.serve_forever, daemon=True)
            thread.start()
            base_url = f"http://127.0.0.1:{server.server_address[1]}"
            try:
                with urlopen(base_url + "/health", timeout=2) as response:
                    health = json.loads(response.read().decode("utf-8"))
                self.assertEqual("history-export", health["service"])

                body = json.dumps(sample_bundle(), ensure_ascii=False).encode("utf-8")
                request = Request(
                    base_url + "/api/history/export",
                    data=body,
                    headers={"Content-Type": "application/json"},
                    method="POST",
                )
                with urlopen(request, timeout=5) as response:
                    result = json.loads(response.read().decode("utf-8"))
                self.assertEqual("ok", result["status"])
                self.assertTrue((Path(result["exportDirectory"]) / result["excelFile"]).is_file())
            finally:
                server.shutdown()
                server.server_close()
                thread.join(timeout=2)


if __name__ == "__main__":
    unittest.main()
