from decimal import Decimal as D
import unittest

from mkr_data_import.transformations import Transformation, transform


class TransformationTests(unittest.TestCase):
    def test_replay_keeps_original_and_records_parameters(self):
        original = ((D(0), D(1)), (D(2), D(5)), (D(4), D(9)))
        history = (
            Transformation("window", (D(0), D(3)), "Isolate first ramp."),
            Transformation("resample", (D(1), D(2)), "Align on one-second intervals."),
            Transformation("moving_average", (D(2),), "Apply causal averaging."),
        )
        output = transform(original, history)
        self.assertEqual(output.original, original)
        self.assertEqual(output.history, history)
        self.assertEqual(output.values, ((D(1), D(2)), (D(2), D(4))))
        self.assertEqual(transform(output.original, output.history), output)

    def test_interpolation_refuses_to_bridge_long_gaps(self):
        with self.assertRaisesRegex(ValueError, "maximum"):
            transform(((D(0), D(1)), (D(9), D(7))),
                      (Transformation("resample", (D(1), D(3)), "Align samples."),))
