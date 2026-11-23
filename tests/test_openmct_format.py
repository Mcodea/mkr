"""Format regressions for the OpenMCT laboratory reader, using synthetic records."""
from io import BytesIO
import unittest
from zipfile import ZipFile

from reference.openmct import inspect


class OpenMctFormatTests(unittest.TestCase):
    def run_records(self, commands):
        with ZipFile(BytesIO(), "w") as archive:
            for period in (10, 20):
                header = "Mode: test\nDate: fixture\nREF,MEAS,DT_ms,PWM\n"
                rows = [f"{i},12,{period},{pwm}\n" for i, pwm in enumerate(commands)]
                archive.writestr(f"03_system_identification/{period} ms/raw_data.txt",
                                 header + "".join(rows))
            inspect(archive)

    def test_reads_applied_pwm_after_metadata(self):
        self.run_records([0, 0, 30, 30])

    def test_changing_reference_with_constant_pwm_is_rejected(self):
        with self.assertRaisesRegex(ValueError, "no recorded change"):
            self.run_records([30, 30, 30, 30])

    def test_missing_command_sample_is_rejected(self):
        with self.assertRaisesRegex(ValueError, "Invalid actuation"):
            self.run_records([0, "nan", 30])
