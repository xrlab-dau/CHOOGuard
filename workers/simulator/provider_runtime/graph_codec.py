"""Decode readable factored-v2 and historical prefix-column-v1 prompt graphs."""
import copy
import json
import math
import re
import struct
from local_client import LocalFailure

GROUP_COLUMNS=['parent_ref','kind','tag_set','state_schema','link_set','conditional_operation_set','kernel_semantic','physical_parent_ref']


class EntityIndex(dict):
    def __init__(self, identities=None, encoding='legacy'):
        super().__init__()
        self.identities = identities
        self.encoding = encoding


def require(condition, code='invalid_compact_graph'):
    if not condition:
        raise LocalFailure(code)


def integer(value):
    return type(value) is int


def decode_graph(graph):
    if isinstance(graph,dict) and graph.get('encoding')=='factored-prefix-column-v2':
        return decode_graph(unfactor_graph(graph))
    require(isinstance(graph, dict) and graph.get('encoding') == 'prefix-column-v1')
    require(type(graph.get('numeric_scale')) is int and graph['numeric_scale'] == 10000, 'unsupported_compact_numeric_scale')
    require(graph.get('group_columns') == GROUP_COLUMNS,'invalid_compact_group_columns')
    encoded = graph.get('identities')
    require(isinstance(encoded, list) and 1 <= len(encoded) <= 100000, 'invalid_compact_identity_table')
    identities = []
    previous = ''
    size = 0
    for pair in encoded:
        require(isinstance(pair, list) and len(pair) == 2 and integer(pair[0]) and isinstance(pair[1], str), 'invalid_compact_identity_prefix')
        previous_bytes = previous.encode('utf-16-le', errors='surrogatepass')
        require(0 <= pair[0] <= len(previous_bytes) // 2, 'invalid_compact_identity_prefix')
        require(pair[0] == 0 or not 0xD800 <= int.from_bytes(previous_bytes[(pair[0]-1)*2:pair[0]*2],'little') <= 0xDBFF,'invalid_compact_identity_prefix')
        raw = previous_bytes[:pair[0] * 2] + pair[1].encode('utf-16-le', errors='surrogatepass')
        identity = raw.decode('utf-16-le', errors='surrogatepass')
        require(identity and len(raw) <= 16384, 'invalid_compact_identity_table')
        # .NET ordinal sorting compares UTF-16 code units, including non-ASCII identifiers.
        if identities:
            require(previous.encode('utf-16-be', errors='surrogatepass') < identity.encode('utf-16-be', errors='surrogatepass'), 'unsorted_or_duplicate_compact_identity')
        identities.append(identity)
        previous = identity
        size += len(raw)
        require(size <= 33554432, 'compact_identity_expansion_too_large')
    require('@world' in identities, 'missing_compact_world_identity')

    def ref(value, nullable=False):
        if nullable and value is None:
            return None
        require(integer(value) and 0 <= value < len(identities), 'invalid_compact_identity_reference')
        return identities[value]

    tag_sets = graph.get('tag_sets')
    schemas = graph.get('state_schemas')
    operations = graph.get('conditional_operation_sets')
    links = graph.get('link_sets')
    for table in (tag_sets, schemas, operations):
        require(isinstance(table, list) and all(isinstance(row, list) and all(isinstance(value, str) for value in row) for row in table), 'invalid_compact_schema_table')
    require(isinstance(links, list) and all(isinstance(row, list) for row in links), 'invalid_compact_link_table')
    decoded_links = [[ref(value) for value in row] for row in links]

    def index(value, table):
        require(integer(value) and 0 <= value < len(table), 'invalid_compact_schema_reference')
        return value

    def column(value, count):
        if isinstance(value,dict):
            require(set(value) == {'raw'} and isinstance(value['raw'],list) and len(value['raw']) == count,'invalid_compact_raw_column')
            for number in value['raw']:
                require(type(number) in (int,float),'invalid_compact_raw_column')
                try:
                    finite=math.isfinite(number)
                except OverflowError:
                    finite=False
                require(finite,'invalid_compact_raw_column')
            return list(value['raw'])
        require(integer(value) or isinstance(value, list) and len(value) == count and all(integer(item) for item in value), 'invalid_compact_numeric_column')
        deltas = [value] * count if integer(value) else value
        units = []
        current = 0
        for i, item in enumerate(deltas):
            require(-(2**63) <= item < 2**63, 'compact_numeric_overflow')
            current = item if integer(value) or i == 0 else current + item
            require(-(2**63) <= current < 2**63 and abs(current) <= 10**14, 'compact_numeric_overflow')
            number = current / 10000
            require(math.isfinite(number), 'compact_numeric_overflow')
            units.append(number)
        return units

    groups = graph.get('entity_groups')
    require(isinstance(groups, list), 'invalid_compact_entity_groups')
    entities = []
    seen = set()
    for group in groups:
        require(isinstance(group, dict) and set(group) == {'common', 'entity_refs', 'xyz_columns', 'state_value_columns'}, 'invalid_compact_entity_group')
        common, refs = group['common'], group['entity_refs']
        require(isinstance(common, list) and len(common) == 8 and isinstance(common[1], str)
                and type(common[6]) is bool and isinstance(refs, list) and refs, 'invalid_compact_entity_group')
        require(len(entities)+len(refs) <= 50000,'compact_entity_expansion_too_large')
        parent, physical_parent = ref(common[0], True), ref(common[7], True)
        tag = index(common[2], tag_sets)
        schema = index(common[3], schemas)
        link = index(common[4], links)
        operation = index(common[5], operations)
        require(isinstance(group['xyz_columns'], list) and len(group['xyz_columns']) == 3
                and isinstance(group['state_value_columns'], list) and len(group['state_value_columns']) == len(schemas[schema]), 'invalid_compact_numeric_column')
        xyz = [column(value, len(refs)) for value in group['xyz_columns']]
        values = [column(value, len(refs)) for value in group['state_value_columns']]
        for i, value in enumerate(refs):
            identity = ref(value)
            require(identity != '@world' and identity not in seen, 'duplicate_or_world_compact_entity')
            seen.add(identity)
            entities.append([identity, parent, common[1], [axis[i] for axis in xyz], tag, schema,
                             [axis[i] for axis in values], link, operation, common[6], physical_parent])
    require(type(graph.get('entity_count')) is int and graph['entity_count'] == len(entities)
            and type(graph.get('kernel_semantic_count')) is int
            and graph['kernel_semantic_count'] == sum(int(row[9]) for row in entities), 'incomplete_compact_entity_graph')
    visuals = graph.get('visuals')
    require(isinstance(visuals, list) and type(graph.get('visual_count')) is int and graph['visual_count'] == len(visuals), 'incomplete_compact_visual_graph')
    decoded_visuals = []
    seen_visual = set()
    for row in visuals:
        require(isinstance(row, list) and len(row) == 3 and isinstance(row[2], str), 'invalid_compact_visual')
        identity = ref(row[0])
        require(identity not in seen_visual and identity != '@world', 'duplicate_or_world_compact_visual')
        seen_visual.add(identity)
        decoded_visuals.append([identity, ref(row[1], True), row[2]])
    result = dict(graph)
    for key in ('encoding','numeric_scale','identities','group_columns','entity_groups','decode_instructions'):
        result.pop(key,None)
    entities.sort(key=lambda row:row[0].encode('utf-16-be',errors='surrogatepass'))
    result.update(entities=entities, link_sets=decoded_links, visuals=decoded_visuals,
                  visual_columns=['id','physical_parent_id','qualification_status'],
                  entity_columns=['id', 'parent_id', 'kind', 'xyz', 'tag_set', 'state_schema', 'state_values',
                                  'link_set', 'conditional_operation_set', 'kernel_semantic', 'physical_parent'])
    return result, identities


def unfactor_graph(graph):
    require(graph.get('entity_group_fields')==['common','entity_refs','xyz_column_refs','state_column_refs'],'invalid_factored_group_fields')
    pool=graph.get('numeric_columns')
    require(isinstance(pool,list) and len(pool)<=1000000,'invalid_factored_numeric_table')
    values=[];seen=set();cells=0
    for column in pool:
        key=json.dumps(column,ensure_ascii=False,separators=(',',':'))
        require(key not in seen,'duplicate_factored_numeric_column');seen.add(key)
        if isinstance(column,dict):
            require(set(column)=={'exact'} and isinstance(column['exact'],list) and 0<len(column['exact'])<=50000,'invalid_factored_exact_column')
            decoded=[]
            for encoded in column['exact']:
                require(isinstance(encoded,str) and re.fullmatch(r'f64:[0-9a-f]{16}',encoded) is not None,'invalid_factored_exact_number')
                number=struct.unpack('>d',bytes.fromhex(encoded[4:]))[0]
                require(math.isfinite(number),'nonfinite_factored_exact_number');decoded.append(number)
            values.append({'raw':decoded});cells+=len(decoded)
        else:
            require(integer(column) or isinstance(column,list) and 0<len(column)<=50000 and all(integer(v) for v in column),'invalid_factored_numeric_column')
            for number in column if isinstance(column,list) else [column]:
                require(-(2**63)<=number<2**63,'compact_numeric_overflow')
            values.append(copy.deepcopy(column));cells+=len(column) if isinstance(column,list) else 1
        require(cells<=10000000,'factored_numeric_expansion_too_large')
    groups=graph.get('entity_groups');require(isinstance(groups,list),'invalid_factored_groups')
    expanded=[]
    for group in groups:
        require(isinstance(group,list) and len(group)==4,'invalid_factored_group')
        def expand(references):
            require(isinstance(references,list) and all(integer(v) and 0<=v<len(values) for v in references),'invalid_factored_numeric_reference')
            return [copy.deepcopy(values[v]) for v in references]
        expanded.append({'common':copy.deepcopy(group[0]),'entity_refs':copy.deepcopy(group[1]),
            'xyz_columns':expand(group[2]),'state_value_columns':expand(group[3])})
    result=copy.deepcopy(graph);result.update(encoding='prefix-column-v1',entity_groups=expanded)
    result.pop('numeric_columns');result.pop('entity_group_fields')
    return result
