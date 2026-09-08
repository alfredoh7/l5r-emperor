import unittest

from download_expansion_printings import scope_to_needed, slugify


class DownloadExpansionPrintingsTests(unittest.TestCase):
    def response(self, cardids):
        hits = [{"_source": {"cardid": cardid, "title": [f"Card {cardid}"]}} for cardid in cardids]
        return {"timed_out": False, "_shards": {"failed": 0}, "hits": {"total": len(hits), "hits": hits}}

    def test_slugify_lowercases_and_collapses_punctuation(self):
        self.assertEqual(slugify("The Shadow's Embrace"), "the-shadow-s-embrace")
        self.assertEqual(slugify("Promotional–Emperor"), "promotional-emperor")

    def test_scope_keeps_only_needed_cards_and_updates_total(self):
        scoped = scope_to_needed(self.response([1, 2, 3]), {2})
        self.assertEqual(scoped["hits"]["total"], 1)
        self.assertEqual([h["_source"]["cardid"] for h in scoped["hits"]["hits"]], [2])

    def test_scope_tolerates_float_cardids(self):
        scoped = scope_to_needed(self.response([14175.0]), {14175})
        self.assertEqual(scoped["hits"]["total"], 1)

    def test_scope_rejects_incomplete_or_failed_search(self):
        for broken in (
            {**self.response([1]), "timed_out": True},
            {**self.response([1]), "_shards": {"failed": 1}},
        ):
            with self.subTest(broken=broken), self.assertRaises(ValueError):
                scope_to_needed(broken, {1})
        truncated = self.response([1, 2])
        truncated["hits"]["total"] = 5
        with self.assertRaises(ValueError):
            scope_to_needed(truncated, {1, 2})

    def test_scope_can_return_no_matches_without_failing(self):
        scoped = scope_to_needed(self.response([1, 2]), {999})
        self.assertEqual(scoped["hits"]["hits"], [])
        self.assertEqual(scoped["hits"]["total"], 0)


if __name__ == "__main__":
    unittest.main()
