"""Bounded read-only decoding of the current SimulationJournal v3 format."""
import base64
import copy
import hashlib
import json
import zlib
from pathlib import Path

MAX_COMPRESSED = 16 * 1024 * 1024
MAX_PLAIN = 64 * 1024 * 1024
MAX_LOG = 256 * 1024 * 1024
EMPTY_HASH = '0' * 64


class ProbeError(ValueError):
    pass


def strict_json(raw):
    def pairs(items):
        result = {}
        for key, value in items:
            if key in result:
                raise ProbeError('DUPLICATE_JSON_KEY')
            result[key] = value
        return result
    try:
        return json.loads(raw, object_pairs_hook=pairs,
                          parse_constant=lambda _: (_ for _ in ()).throw(ProbeError('NONFINITE_JSON')))
    except (json.JSONDecodeError, UnicodeDecodeError) as exc:
        raise ProbeError('MALFORMED_JSON') from exc


def integer(value):
    if isinstance(value, bool) or not isinstance(value, int) or not 0 <= value <= 2**63 - 1:
        raise ProbeError('INVALID_INTEGER')
    return value


def digest(envelope):
    keys = ('Schema', 'Kind', 'WorldId', 'ShiftId', 'DefinitionHash', 'Ordinal', 'Sequence', 'Tick', 'PlainBytes', 'PreviousHash', 'Payload')
    try:
        return hashlib.sha256('\n'.join(str(envelope[k]) for k in keys).encode('utf-8')).hexdigest()
    except KeyError as exc:
        raise ProbeError('JOURNAL_FIELD_MISSING') from exc


def decode(envelope, identity=None):
    if not isinstance(envelope, dict) or envelope.get('Schema') != 3 or isinstance(envelope.get('Schema'), bool):
        raise ProbeError('JOURNAL_SCHEMA')
    for key in ('Ordinal', 'Sequence', 'Tick', 'PlainBytes'):
        integer(envelope.get(key))
    plain_limit = MAX_PLAIN if envelope.get('Kind') == 'checkpoint' else 32 * 1024 * 1024
    if envelope['PlainBytes'] > plain_limit or envelope.get('Hash') != digest(envelope):
        raise ProbeError('JOURNAL_HASH_OR_LENGTH')
    if identity and tuple(envelope.get(k) for k in ('WorldId', 'ShiftId', 'DefinitionHash')) != identity:
        raise ProbeError('JOURNAL_IDENTITY')
    payload = envelope.get('Payload')
    if not isinstance(payload, str) or len(payload) > MAX_COMPRESSED * 4 // 3 + 4:
        raise ProbeError('JOURNAL_COMPRESSED_BOUND')
    try:
        raw = base64.b64decode(payload, validate=True)
        if len(raw) > MAX_COMPRESSED:
            raise ProbeError('JOURNAL_COMPRESSED_BOUND')
        decoder = zlib.decompressobj(-zlib.MAX_WBITS)  # .NET DeflateStream emits raw DEFLATE.
        plain = decoder.decompress(raw, envelope['PlainBytes'] + 1)
        if len(plain) != envelope['PlainBytes'] or decoder.unconsumed_tail or decoder.unused_data or not decoder.eof:
            raise ProbeError('JOURNAL_INFLATE_LENGTH')
        return strict_json(plain)
    except (ValueError, zlib.error) as exc:
        if isinstance(exc, ProbeError):
            raise
        raise ProbeError('JOURNAL_INFLATE_INVALID') from exc


def merge(items, changes, key):
    output = {item[key]: copy.deepcopy(item) for item in items}
    if len(output) != len(items) or len({item[key] for item in changes}) != len(changes):
        raise ProbeError('JOURNAL_DUPLICATE_ID')
    for item in changes:
        output[item[key]] = copy.deepcopy(item)
    return list(output.values())


def read_journal(directory):
    directory = Path(directory)
    checkpoint_path, log_path = directory/'checkpoint-v3.json', directory/'actions-v3.jsonl'
    if not checkpoint_path.is_file() or not log_path.is_file():
        raise ProbeError('JOURNAL_FILES_MISSING')
    if checkpoint_path.stat().st_size > MAX_COMPRESSED * 4 // 3 + 4096 or log_path.stat().st_size > MAX_LOG:
        raise ProbeError('JOURNAL_FILE_BOUND')
    checkpoint = strict_json(checkpoint_path.read_bytes())
    if checkpoint.get('Kind') != 'checkpoint':
        raise ProbeError('CHECKPOINT_KIND')
    identity = tuple(checkpoint.get(k) for k in ('WorldId', 'ShiftId', 'DefinitionHash'))
    state = decode(checkpoint)
    if state.get('SchemaVersion') != 3 or state.get('Sequence') != checkpoint['Sequence'] or state.get('SimulationTick') != checkpoint['Tick']:
        raise ProbeError('CHECKPOINT_CONTEXT')
    if tuple(state.get(k) for k in ('WorldId', 'ShiftId', 'SimulationDefinitionHash')) != identity:
        raise ProbeError('CHECKPOINT_IDENTITY')
    # Knowledge is approved-command sequence state; motion boundaries cannot alter it.
    knowledge = {0: {p['ParticipantId']: set() for p in state['Participants']}, state['Sequence']: {p['ParticipantId']: set(p['ObservedIds']) for p in state['Participants']}}
    command_receipts = []
    prefix_seen = checkpoint['Ordinal'] == 0 and checkpoint['PreviousHash'] == EMPTY_HASH
    previous_hash, ordinal, sequence, tick = EMPTY_HASH, 0, 0, 0
    truncated = 0
    with log_path.open('rb') as stream:
        for raw in stream:
            if len(raw) > MAX_COMPRESSED * 4 // 3 + 4096:
                raise ProbeError('JOURNAL_LINE_BOUND')
            if not raw.endswith(b'\n'):
                truncated = len(raw)
                break
            envelope = strict_json(raw)
            body = decode(envelope, identity)
            if envelope['Ordinal'] != ordinal + 1 or envelope['PreviousHash'] != previous_hash or envelope['Tick'] < tick:
                raise ProbeError('JOURNAL_ORDER')
            if envelope['Kind'] == 'command':
                receipt = body.get('Receipt')
                if not isinstance(receipt, dict) or receipt.get('Code') != 0 or receipt.get('Sequence') != sequence + 1 or envelope['Sequence'] != sequence + 1:
                    raise ProbeError('JOURNAL_APPROVAL_SEQUENCE')
                if body.get('WorldSchemaVersion') != 3 or body.get('SimulationTick') != envelope['Tick'] or body.get('SimulationDefinitionHash') != identity[2]:
                    raise ProbeError('JOURNAL_COMMAND_CONTEXT')
                if (receipt.get('WorldId'), receipt.get('ShiftId')) != identity[:2]:
                    raise ProbeError('JOURNAL_RECEIPT_IDENTITY')
                sequence += 1
                command_receipts.append(copy.deepcopy(receipt))
                knowledge[sequence] = {p['ParticipantId']: set(p['ObservedIds']) for p in body['Participants']}
                if envelope['Ordinal'] > checkpoint['Ordinal']:
                    state['Participants'] = merge(state['Participants'], body['Participants'], 'ParticipantId')
                    state['Entities'] = merge(state['Entities'], body['Entities'], 'EntityId')
                    state['Reports'] = merge(state.get('Reports', []), body.get('Reports', []), 'ReportId')
                    state.setdefault('Receipts', []).append(copy.deepcopy(receipt))
                    state.update(Sequence=sequence, SimulationTick=envelope['Tick'], Frames=copy.deepcopy(body['Frames']),
                                 SimulationCheckpoint=body['SimulationCheckpoint'], Paused=body['Paused'])
            elif envelope['Kind'] == 'boundary':
                if envelope['Sequence'] != sequence or body.get('Sequence') != sequence:
                    raise ProbeError('JOURNAL_BOUNDARY_SEQUENCE')
                known = knowledge.get(sequence)
                observed = {p['ParticipantId']: set(p['ObservedIds']) for p in body['Participants']}
                if known is not None and observed != known:
                    raise ProbeError('BOUNDARY_CHANGED_APPROVED_KNOWLEDGE')
                if envelope['Ordinal'] > checkpoint['Ordinal']:
                    state.update(SimulationTick=envelope['Tick'], SimulationCheckpoint=body['Checkpoint'], Frames=copy.deepcopy(body['Frames']),
                                 Participants=copy.deepcopy(body['Participants']), Entities=copy.deepcopy(body['Entities']), Paused=body['Paused'])
            else:
                raise ProbeError('JOURNAL_UNKNOWN_KIND')
            ordinal, tick, previous_hash = envelope['Ordinal'], envelope['Tick'], envelope['Hash']
            if ordinal == checkpoint['Ordinal']:
                prefix_seen = previous_hash == checkpoint['PreviousHash'] and sequence == checkpoint['Sequence']
    if not prefix_seen or ordinal < checkpoint['Ordinal'] or sequence < checkpoint['Sequence']:
        raise ProbeError('CHECKPOINT_PREFIX_NOT_IN_LOG')
    if state.get('Receipts', []) != command_receipts:
        # Current writer keeps the complete approval list in ascending sequence.
        raise ProbeError('APPROVAL_LEDGER_DIFFERS_FROM_LOG')
    return {'State': state, 'KnowledgeBySequence': knowledge, 'Ordinal': ordinal, 'TruncatedTailBytes': truncated,
            'SourceHashes': {p.name: hashlib.sha256(p.read_bytes()).hexdigest() for p in (checkpoint_path, log_path)}}
