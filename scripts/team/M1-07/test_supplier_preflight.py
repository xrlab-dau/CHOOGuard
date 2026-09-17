import unittest
from pathlib import Path
from unittest.mock import patch

import review_integrity as api


class SupplierPreflight(unittest.TestCase):
    def setUp(self):
        api._bindings = None
        api._contract_hash = None
        api.native = api.boundary = None

    def test_clean_supplier_closure_loads(self):
        api.load_suppliers()
        self.assertIsNotNone(api.native)
        self.assertIsNotNone(api.boundary)

    def test_changed_direct_supplier_refuses_before_import(self):
        path = api.REPO / api.SUPPLIERS[0]
        original = Path.read_bytes
        def changed(self):
            data = original(self)
            return data + b'\n# drift\n' if self == path else data
        with patch.object(Path, 'read_bytes', changed):
            with self.assertRaisesRegex(api.Refused, 'supplier_source_drift'):
                api.load_suppliers()

    def test_missing_transitive_supplier_refuses_before_import(self):
        path = api.REPO / api.SUPPLIERS[2]
        original = Path.read_bytes
        def missing(self):
            if self == path:
                raise OSError('missing supplier')
            return original(self)
        with patch.object(Path, 'read_bytes', missing):
            with self.assertRaisesRegex(api.Refused, 'supplier_source_drift'):
                api.load_suppliers()


if __name__ == '__main__':
    unittest.main()
