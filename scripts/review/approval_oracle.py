"""Frozen, bounded approval-shape oracle. Never an approval authenticator.

The worker sees the task contract, not this oracle. Candidate code is NOT given
filesystem, imports, network, reflection or unrestricted builtins. This narrow
AST capability filter is additional containment for this pure function only;
it is not advertised as a general-purpose Python sandbox.
"""
from __future__ import annotations
import ast
import copy
import json
import re
from pathlib import Path

BUILTINS = {'isinstance': isinstance, 'type': type, 'str': str, 'dict': dict,
            'list': list, 'int': int, 'bool': bool, 'set': set, 'len': len,
            'all': all, 'any': any, 'tuple': tuple}
ATTRS = {'get', 'keys', 'issubset', 'fullmatch', 'search'}
NODES = (ast.Module, ast.FunctionDef, ast.arguments, ast.arg, ast.Expr, ast.Constant,
         ast.Assign, ast.If, ast.Raise, ast.Return, ast.Name, ast.Load, ast.Store,
         ast.Subscript, ast.Attribute, ast.Call, ast.keyword, ast.Compare, ast.BoolOp,
         ast.UnaryOp, ast.BinOp, ast.And, ast.Or, ast.Not, ast.Eq, ast.NotEq, ast.Is,
         ast.IsNot, ast.In, ast.NotIn, ast.LtE, ast.GtE, ast.Lt, ast.Gt, ast.BitOr,
         ast.Sub, ast.List, ast.Tuple, ast.Set, ast.Dict, ast.GeneratorExp,
         ast.ListComp, ast.SetComp, ast.comprehension)

class Refused(Exception):
    pass


def load_candidate(source):
    if not isinstance(source, str) or len(source.encode('utf-8')) > 12000:
        raise ValueError('candidate_size')
    tree = ast.parse(source)
    if len(tree.body) != 1 or not isinstance(tree.body[0], ast.FunctionDef):
        raise ValueError('one_function_required')
    fn=tree.body[0]
    if fn.name != 'validate_approval_record' or fn.decorator_list or fn.args.defaults or fn.args.kw_defaults:
        raise ValueError('signature_or_decorators')
    if [a.arg for a in fn.args.args] != ['schema','record'] or fn.args.vararg or fn.args.kwarg or fn.args.kwonlyargs or fn.args.posonlyargs:
        raise ValueError('signature')
    nodes=list(ast.walk(tree))
    if sum(isinstance(n,ast.FunctionDef) for n in nodes)!=1: raise ValueError('nested_function_not_allowed')
    if len(nodes)>1000: raise ValueError('ast_limit')
    for node in nodes:
        if not isinstance(node,NODES): raise ValueError('syntax_not_allowed:'+type(node).__name__)
        if isinstance(node,ast.Name) and node.id.startswith('_'): raise ValueError('private_name')
        if isinstance(node,ast.Attribute) and node.attr not in ATTRS: raise ValueError('attribute_not_allowed')
        if isinstance(node,ast.Call):
            f=node.func
            if isinstance(f,ast.Name) and f.id not in {*BUILTINS,'Refused'}: raise ValueError('call_not_allowed')
            if not isinstance(f,(ast.Name,ast.Attribute)): raise ValueError('call_not_allowed')
        if isinstance(node,ast.Assign):
            if any(not isinstance(t,ast.Name) or t.id in {*BUILTINS,'re','Refused','schema','record'} for t in node.targets):
                raise ValueError('mutation_not_allowed')
    ns={'__builtins__':BUILTINS,'re':re,'Refused':Refused}
    exec(compile(tree,'<bounded-candidate>','exec'),ns)
    return ns['validate_approval_record']


def cases(schema):
    def good(decision='approved',ref='synthetic-decision-001'):
        return {'schemaVersion':1,'state':'approval_record','manifestSha256':'a'*64,
                'decision':decision,'decisionRef':ref}
    records=[]
    for decision in ('approved','rejected','pending','unknown'):
        for ref in ('synthetic-decision-001',' 검토 ',None,'',' ','\t\n',7,False,[],{}):
            allowed=isinstance(ref,str) and bool(re.search(r'\S',ref)) or ref is None and decision in ('pending','unknown')
            records.append((f'reference-{decision}-{len(records)}',schema,good(decision,ref),allowed))
    for value in (True,False,1.0,'1',0,2,None,[],{}):
        records.append((f'version-type-{type(value).__name__}-{len(records)}',schema,{**good(),'schemaVersion':value},type(value) is float and value==1.0))
    for key in good():
        records.append(('missing-'+key,schema,{k:v for k,v in good().items() if k!=key},False))
    for value in (None,[],True,1,'approved',{}, {'extra':1}):
        records.append(('record-shape-'+str(len(records)),schema,value,False))
    for value in ('a'*63,'a'*65,'A'*64,'g'*64,'a'*64+'\n',None,[],1):
        records.append(('hash-'+str(len(records)),schema,{**good(),'manifestSha256':value},False))
    for value in ('approval_record\n','raw',None,1,True,[],{}):
        records.append(('state-'+str(len(records)),schema,{**good(),'state':value},False))
    for value in ('APPROVED','approved\n','',None,[],{},True,1):
        records.append(('decision-'+str(len(records)),schema,{**good(),'decision':value},False))
    records.append(('extra-field',schema,{**good(),'unexpected':1},False))
    narrow=copy.deepcopy(schema); narrow['$defs']['approval']['properties']['decision']['enum']=['unknown','pending','rejected']
    records.append(('schema-narrowed-decision',narrow,good(),False))
    records.append(('schema-narrowed-positive',narrow,good('rejected'),True))
    return records


def evaluate(source,schema):
    try: fn=load_candidate(source)
    except (ValueError,SyntaxError,TypeError) as e:
        return {'state':'REWORK','failures':[str(e)],'casesRun':0,'passed':0}
    failures=[]; total=0
    for name,spec,record,expected in cases(schema):
        total+=1
        arg_schema=copy.deepcopy(spec); arg_record=copy.deepcopy(record)
        before=json.dumps([arg_schema,arg_record],sort_keys=True)
        try:
            value=fn(arg_schema,arg_record); actual=value is None
            outcome='accepted' if actual else 'invalid_return'
        except Refused as e:
            actual=False; outcome='refused' if str(e)=='approval_record_invalid' else 'wrong_refusal_code'
        except Exception as e:
            actual=False; outcome='exception:'+type(e).__name__
        if actual != expected or outcome not in ('accepted','refused'):
            failures.append(name+':'+outcome)
        if before!=json.dumps([arg_schema,arg_record],sort_keys=True): failures.append(name+':input_mutation')
    return {'state':'TESTS_PASS_REVIEW_PENDING' if not failures else 'REWORK',
            'casesRun':total,'passed':total-len(failures),'failures':failures}


def main():
    import sys
    source=Path(sys.argv[1]).read_text(encoding='utf-8')
    schema=json.loads(Path(sys.argv[2]).read_text(encoding='utf-8'))
    result=evaluate(source,schema)
    print(json.dumps(result,sort_keys=True))
    return 0 if not result['failures'] else 1

if __name__=='__main__': raise SystemExit(main())
