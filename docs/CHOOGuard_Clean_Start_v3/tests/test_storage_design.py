"""Run SQL-design invariants on temporary disk DB; NOT Unity/provider/power-loss validation."""
import sqlite3,tempfile,unittest
from pathlib import Path
R=Path(__file__).resolve().parents[1]
class StorageDesign(unittest.TestCase):
 def setUp(self):
  self.tmp=tempfile.TemporaryDirectory();self.db=sqlite3.connect(str(Path(self.tmp.name)/'design.db'))
  self.db.executescript((R/'design/sqlite.sql').read_text());self.db.execute("INSERT INTO run VALUES('r',0,0)");self.db.execute("INSERT INTO resource VALUES('r','kit',3,'piece')");self.db.commit()
 def tearDown(self):self.db.close();self.tmp.cleanup()
 def reserve(self,task,n):self.db.execute('INSERT INTO reservation VALUES(?,?,?,?,?)',('r',task,'kit',n,'ACTIVE'))
 def test_capacity_constraint(self):
  self.reserve('a',2)
  with self.assertRaises(sqlite3.IntegrityError):self.reserve('b',2)
  self.assertEqual(self.db.execute("SELECT SUM(amount) FROM reservation WHERE state='ACTIVE'").fetchone()[0],2)
 def test_reservation_rollback(self):
  self.db.execute('BEGIN IMMEDIATE');self.reserve('a',1);self.db.execute("INSERT INTO event VALUES('r',1,'e1','reserved','{}')");self.db.rollback()
  self.assertEqual(self.db.execute('SELECT COUNT(*) FROM event').fetchone()[0],0);self.assertEqual(self.db.execute('SELECT COUNT(*) FROM reservation').fetchone()[0],0)
 def test_receipt_durable_identity(self):
  with self.assertRaises(sqlite3.IntegrityError):self.db.execute('INSERT INTO receipt VALUES(?,?,?,?,?,?,?)',('r','a','i','a'*64,'ACCEPTED',None,None))
 def test_receipt_key_uniqueness(self):
  row=('r','a','i','a'*64,'ACCEPTED','c',1);self.db.execute('INSERT INTO receipt VALUES(?,?,?,?,?,?,?)',row)
  with self.assertRaises(sqlite3.IntegrityError):self.db.execute('INSERT INTO receipt VALUES(?,?,?,?,?,?,?)',row)
 def test_requester_separates_identity(self):
  for requester in ['a','b']:self.db.execute('INSERT INTO receipt VALUES(?,?,?,?,?,?,?)',('r',requester,'i','a'*64,'ACCEPTED','c',1))
  self.assertEqual(self.db.execute('SELECT COUNT(*) FROM receipt').fetchone()[0],2)
 def test_events_immutable(self):
  self.db.execute("INSERT INTO event VALUES('r',1,'e1','test','{}')")
  with self.assertRaises(sqlite3.IntegrityError):self.db.execute("UPDATE event SET kind='other'")
 def test_foreign_resource_rejected(self):
  with self.assertRaises(sqlite3.IntegrityError):self.db.execute("INSERT INTO reservation VALUES('r','a','unknown',1,'ACTIVE')")
 def test_backup_api(self):
  self.reserve('a',2);self.db.commit();dest=sqlite3.connect(str(Path(self.tmp.name)/'copy.db'));self.db.backup(dest)
  self.assertEqual(dest.execute('SELECT amount FROM reservation').fetchone()[0],2);self.assertEqual(dest.execute('PRAGMA integrity_check').fetchone()[0],'ok');dest.close()
 def test_committed_data_reopen(self):
  self.reserve('a',2);self.db.commit();self.db.close();self.db=sqlite3.connect(str(Path(self.tmp.name)/'design.db'))
  self.assertEqual(self.db.execute('SELECT amount FROM reservation').fetchone()[0],2)
