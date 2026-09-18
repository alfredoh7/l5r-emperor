import unittest
from unittest.mock import patch
from publish import deliver, verify_asset


class PublishTests(unittest.TestCase):
    def test_corrupt_or_incomplete_upload_is_rejected(self):
        receipt = {"asset": "review.zip", "bytes": 12, "sha256": "a" * 64}
        for assets in [[], [{"name": "review.zip", "size": 11, "digest": "sha256:" + "a" * 64}],
                       [{"name": "review.zip", "size": 12, "digest": "sha256:" + "b" * 64}]]:
            with self.subTest(assets=assets), self.assertRaises(ValueError):
                verify_asset(receipt, {"assets": assets})

    def test_no_notification_before_exact_windows_check_passes(self):
        for runs in [[], [{"name": "Windows review package", "id": 1, "conclusion": "failure"}],
                     [{"name": "Unrelated green test", "id": 1, "conclusion": "success"}],
                     [{"name": "Windows review package", "id": 1, "conclusion": "success"},
                      {"name": "Windows review package", "id": 2, "conclusion": None}]]:
            with self.subTest(runs=runs), patch("publish.api", return_value={"check_runs": runs}), patch("publish.gh") as gh:
                with self.assertRaises(ValueError):
                    deliver({}, {}, "a" * 40)
                gh.assert_not_called()

    def test_different_receipt_cannot_reuse_green_check(self):
        with patch("publish.api", return_value={"check_runs": [
            {"name": "Windows review package", "id": 1, "conclusion": "success"}
        ]}), patch("publish.gh", return_value='{"sha256":"different"}') as gh:
            with self.assertRaises(ValueError):
                deliver({"slug": "fixture"}, {"sha256": "expected"}, "a" * 40)
            self.assertEqual(gh.call_count, 1)
            self.assertNotIn("POST", gh.call_args.args)


if __name__ == "__main__":
    unittest.main()
