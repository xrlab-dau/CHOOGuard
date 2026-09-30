"""Ranking evidence must not depend on the ordering of tied candidates."""
import unittest

import numpy as np

from train_imminence import within_request


class RankingTests(unittest.TestCase):
    def metric(self, truth, prediction):
        rows = [{"source": "one-shift.jsonl", "at": "one-request"} for _ in truth]
        return within_request(rows, np.array(truth), np.array(prediction))

    def test_any_true_top_candidate_counts_as_correct(self):
        _, hit, counted = self.metric([2, 2, 0], [1, 2, 0])
        self.assertEqual((hit, counted), (1.0, 1))

    def test_prediction_ties_have_order_independent_expected_hit(self):
        _, hit, counted = self.metric([2, 0, 1], [2, 2, 1])
        self.assertEqual((hit, counted), (0.5, 1))
        _, reversed_hit, _ = self.metric([0, 2, 1], [2, 2, 1])
        self.assertEqual(reversed_hit, hit)

    def test_constant_predictor_keeps_top_hit_denominator(self):
        rho, hit, counted = self.metric([0, 1, 2], [1, 1, 1])
        self.assertIsNone(rho)
        self.assertAlmostEqual(hit, 1 / 3)
        self.assertEqual(counted, 1)


if __name__ == "__main__":
    unittest.main()
