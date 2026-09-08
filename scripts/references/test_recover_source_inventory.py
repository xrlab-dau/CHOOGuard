"""Synthetic cached-page recovery does not rewrite prior collection evidence."""
import json
from pathlib import Path
import tempfile
import unittest

from PIL import Image
from recover_source_inventory import recover_photos


class PhotoRecoveryTests(unittest.TestCase):
    def test_missing_photo_is_bound_to_cached_url_without_rewriting_collection(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            collection = json.dumps(dict(page='https://example.org', records=[]))
            (root / 'collection.json').write_text(collection)
            (root / 'source-page.html').write_text(
                '<img src="https://static.wixstatic.com/media/abc.jpg" alt="Busan Station proxy">')
            Image.new('RGB', (8, 6)).save(root / '01-abc.jpg')
            result = recover_photos(root)
            self.assertEqual(result['recoveredCount'], 1)
            self.assertEqual(result['records'][0]['status'], 'cached_page_bound')
            self.assertEqual((root / 'collection.json').read_text(), collection)


if __name__ == '__main__':
    unittest.main()
