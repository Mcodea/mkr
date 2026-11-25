import base64
import json
import unittest

from mkr_engine import respond


def run(csv, operation, **fields):
    request = dict(version=1, requestId="csv-test", operation=operation,
                   payload=dict(raw=base64.b64encode(csv).decode(), delimiter=";", **fields))
    return respond(json.dumps(request).encode())


class CsvTests(unittest.TestCase):
    def test_preview_keeps_delimiters_quotes_and_newlines(self):
        csv = '\ufefftemps;valeur;note\r\n0; 1 ;"A;B\r\nC"\r\n'.encode()
        result = run(csv, "inspect_csv")["result"]
        suggestion = result.pop("suggestedFormat")                      # a proposal, never applied silently
        self.assertEqual(result, dict(headers=["temps", "valeur", "note"],
                                      preview=[["0", " 1 ", "A;B\r\nC"]], rowCount=1))
        self.assertEqual((suggestion["decimal"], suggestion["timeColumn"]), (".", None))

    def test_quality_equal_instants_and_unmodified_anomalies(self):
        csv = (b"ts;x\n2025-01-01T00:00:00Z;2\n2025-01-01T01:00:00+01:00;\n"
               b"2024-12-31T23:59:58Z;NaN\n2025-01-01T00:00:01Z;4\n"
               b"2025-01-01T00:00:01;1e999\n2025-01-01T00:00:08Z;1_000\n")
        response = run(csv, "analyze_csv", timeColumn="ts", valueColumn="x", role="PV", unit="bar")
        report = response["result"]["quality"]
        self.assertEqual(report, dict(algorithmVersion="1.1.0", validValues=2, missingValues=1,
                                     invalidValues=3, invalidTimestamps=1, duplicateTimestamps=1,
                                     reversedTimestamps=1, minimum=2, maximum=4,
                                     minPositiveStepSeconds=3, maxPositiveStepSeconds=3))

    def test_rejects_invalid_input_without_partial_result(self):
        cases = (
            (b"x;x\n1;2\n", "csv_header"),
            (b"x;y\n1\n", "csv_width"),
            (b"x;y\n1;\xff\n", "csv_encoding_or_syntax"),
            (b'x;y\n1;"bad\n', "csv_encoding_or_syntax"),
            (b"x;y\n", "csv_empty"),
            (b"x;y\n" + b"1;2\n" * 10_001, "csv_limit"),
        )
        for raw, code in cases:
            with self.subTest(code=code):
                self.assertEqual(run(raw, "inspect_csv")["error"]["code"], code)

    def test_rejects_ambiguous_mapping(self):
        response = run(b"t;x\nZ;1\n", "analyze_csv", timeColumn="t", valueColumn="t", role="PV", unit="bar")
        self.assertEqual(response["error"]["code"], "csv_mapping")


if __name__ == "__main__":
    unittest.main()
