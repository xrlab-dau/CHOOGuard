-- Design validation only. Actual Unity provider, flush, process recovery are NOT tested here.
PRAGMA foreign_keys=ON;
CREATE TABLE run(run_id TEXT PRIMARY KEY, revision INTEGER NOT NULL CHECK(revision>=0), generation INTEGER NOT NULL CHECK(generation>=0));
CREATE TABLE receipt(run_id TEXT NOT NULL REFERENCES run, requester_id TEXT NOT NULL, intent_id TEXT NOT NULL,
 fingerprint TEXT NOT NULL CHECK(length(fingerprint)=64), status TEXT NOT NULL CHECK(status IN('ACCEPTED','REJECTED','CONFLICT')),
 commit_id TEXT, sequence INTEGER, PRIMARY KEY(run_id,requester_id,intent_id),
 CHECK(status<>'ACCEPTED' OR (commit_id IS NOT NULL AND sequence IS NOT NULL)));
CREATE TABLE event(run_id TEXT NOT NULL REFERENCES run, sequence INTEGER NOT NULL CHECK(sequence>=0),event_id TEXT UNIQUE NOT NULL,kind TEXT NOT NULL,payload TEXT NOT NULL,PRIMARY KEY(run_id,sequence));
CREATE TRIGGER immutable_event_update BEFORE UPDATE ON event BEGIN SELECT RAISE(ABORT,'IMMUTABLE_EVENT'); END;
CREATE TRIGGER immutable_event_delete BEFORE DELETE ON event BEGIN SELECT RAISE(ABORT,'IMMUTABLE_EVENT'); END;
CREATE TABLE resource(run_id TEXT NOT NULL REFERENCES run,resource_id TEXT NOT NULL,capacity INTEGER NOT NULL CHECK(capacity>=0),unit TEXT NOT NULL,PRIMARY KEY(run_id,resource_id));
CREATE TABLE reservation(run_id TEXT NOT NULL,task_id TEXT NOT NULL,resource_id TEXT NOT NULL,amount INTEGER NOT NULL CHECK(amount>0),state TEXT NOT NULL CHECK(state IN('ACTIVE','RELEASED')),PRIMARY KEY(run_id,task_id,resource_id),FOREIGN KEY(run_id,resource_id) REFERENCES resource);
CREATE TRIGGER reserve_capacity_insert BEFORE INSERT ON reservation WHEN NEW.state='ACTIVE' BEGIN
 SELECT CASE WHEN NEW.amount+COALESCE((SELECT SUM(amount) FROM reservation WHERE run_id=NEW.run_id AND resource_id=NEW.resource_id AND state='ACTIVE'),0)>(SELECT capacity FROM resource WHERE run_id=NEW.run_id AND resource_id=NEW.resource_id)
 THEN RAISE(ABORT,'RESOURCE_CAPACITY_EXCEEDED') END; END;
CREATE TRIGGER reserve_capacity_update BEFORE UPDATE ON reservation WHEN NEW.state='ACTIVE' BEGIN
 SELECT CASE WHEN NEW.amount+COALESCE((SELECT SUM(amount) FROM reservation WHERE run_id=NEW.run_id AND resource_id=NEW.resource_id AND state='ACTIVE' AND NOT(run_id=OLD.run_id AND task_id=OLD.task_id AND resource_id=OLD.resource_id)),0)>(SELECT capacity FROM resource WHERE run_id=NEW.run_id AND resource_id=NEW.resource_id)
 THEN RAISE(ABORT,'RESOURCE_CAPACITY_EXCEEDED') END; END;
CREATE TABLE outbox(job_id TEXT PRIMARY KEY,run_id TEXT NOT NULL REFERENCES run,generation INTEGER NOT NULL,input_digest TEXT NOT NULL CHECK(length(input_digest)=64),state TEXT NOT NULL CHECK(state IN('READY','CLAIMED','DONE','CANCELLED')),attempt INTEGER NOT NULL DEFAULT 0);
CREATE TABLE accepted_result(job_id TEXT PRIMARY KEY REFERENCES outbox,result_digest TEXT NOT NULL CHECK(length(result_digest)=64));
CREATE TABLE checkpoint(checkpoint_id TEXT PRIMARY KEY,run_id TEXT NOT NULL REFERENCES run,cut_sequence INTEGER NOT NULL,tick_us INTEGER NOT NULL,manifest_digest TEXT NOT NULL CHECK(length(manifest_digest)=64));
