-- Version 1 reference-backed storage slice. Runtime statements in SqliteRunStore.Schema.
-- Receipt is a version-bound binary/base64 encoding, not the K02 JSON wire format.
CREATE TABLE cg_run(run_id TEXT PRIMARY KEY, revision INTEGER NOT NULL CHECK(revision>=0), sequence INTEGER NOT NULL CHECK(sequence>=0), reservations TEXT NOT NULL, projection TEXT NOT NULL);
CREATE TABLE cg_commit(run_id TEXT NOT NULL REFERENCES cg_run(run_id), requester_id TEXT NOT NULL, intent_id TEXT NOT NULL, fingerprint TEXT NOT NULL, commit_id TEXT NOT NULL UNIQUE, revision INTEGER NOT NULL, receipt TEXT NOT NULL, events TEXT NOT NULL, reservations TEXT NOT NULL, outbox TEXT NOT NULL, projection TEXT NOT NULL, PRIMARY KEY(run_id,requester_id,intent_id));
CREATE TABLE cg_outbox(run_id TEXT NOT NULL REFERENCES cg_run(run_id), job_id TEXT NOT NULL, commit_id TEXT NOT NULL REFERENCES cg_commit(commit_id), PRIMARY KEY(run_id,job_id));
-- Version 2 additive migration uses this statement after exact v1 schema validation.
-- No data backfill or destructive down migration: v1 rows remain ready via a LEFT JOIN.
CREATE TABLE cg_delivery(run_id TEXT NOT NULL, job_id TEXT NOT NULL, generation INTEGER NOT NULL CHECK(generation>=0), attempt_id TEXT NOT NULL, owner_id TEXT NOT NULL, lease_until INTEGER NOT NULL, acknowledged INTEGER NOT NULL CHECK(acknowledged IN(0,1)), PRIMARY KEY(run_id,job_id), FOREIGN KEY(run_id,job_id) REFERENCES cg_outbox(run_id,job_id));
PRAGMA user_version=2;
