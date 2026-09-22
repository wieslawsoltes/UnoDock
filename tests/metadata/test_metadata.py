"""Test the conservative API gate with explicit synthetic public contracts."""
import copy
import hashlib
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest
ROOT = Path(__file__).resolve().parents[2]
spec = importlib.util.spec_from_file_location('compare_metadata', ROOT/'tools/compare-metadata.py')
api = importlib.util.module_from_spec(spec); spec.loader.exec_module(api)

def member(key='public method System.Void Move(System.Int32 index)', name='Move', kind='Method'):
    return dict(key=key,metadataName=name,kind=kind,attributes=[],parameters=[])

def inventory(members=None):
    typ=dict(name='Dock.Pane',key='Dock.Pane | public class : System.Object interfaces[]',
             members=members if members is not None else [member()],attributes=[],inheritedMembers=[],declaredNames=[])
    declarations=sorted([typ['key']]+[typ['name']+' | '+m['key'] for m in typ['members']])
    return dict(schema=3,types=[typ],declarations=declarations,count=len(declarations),typeCount=1,
                sha256=hashlib.sha256(('\n'.join(declarations)+'\n').encode()).hexdigest())

class GateTests(unittest.TestCase):
    def diff(self,actual,reference=None): return api.compare(reference or inventory(),actual,{})
    def test_exact(self):
        d=self.diff(inventory());self.assertEqual(2,d['mappedSignatureMatches']);self.assertEqual([],d['diagnostics']);self.assertFalse(d['fullCompatibilityVerified'])
    def test_type_mapping_token_boundary(self):
        self.assertEqual('B AExtra',api.mapped('A AExtra',{'A':'B'}))
    def test_quoted_type_name_is_data(self):
        self.assertEqual('F("A",B)',api.mapped('F("A",A)',{'A':'B'}))
    def test_whitespace_inside_default_is_data(self):
        self.assertEqual('public F("a  b\\\"c")',api.mapped('public   F("a  b\\\"c")',{}))
    def test_nonhidden_inheritance(self):
        a=inventory([]);a['types'][0]['inheritedMembers']=[member()];self.assertEqual(1,self.diff(a)['counts']['matchedInherited'])
    def test_private_name_hides_inheritance(self):
        a=inventory([]);a['types'][0].update(inheritedMembers=[member()],declaredNames=['Move']);self.assertEqual(1,self.diff(a)['unresolvedSignatureCount'])
    def test_constructors_not_inherited(self):
        c=member('public constructor System.Void .ctor()', '.ctor');a=inventory([]);a['types'][0]['inheritedMembers']=[c]
        self.assertEqual(1,self.diff(a,inventory([c]))['unresolvedSignatureCount'])
    def test_overloads(self): self.assertDifference('public method System.Void Move(System.String index)')
    def test_named_parameter(self): self.assertDifference('public method System.Void Move(System.Int32 position)')
    def test_ref_mode(self): self.assertDifference('public method System.Void Move(ref System.Int32 index)')
    def test_optional_default(self): self.assertDifference('public method System.Void Move(System.Int32 index=0)')
    def test_virtual_slot(self): self.assertDifference('public virtual method System.Void Move(System.Int32 index)')
    def test_setter_access(self):
        r=member('public property System.Int32 Index() { public get; public set; }','Index','Property')
        a=member(r['key'].replace('public set','protected set'),'Index','Property');self.assertEqual(1,self.diff(inventory([a]),inventory([r]))['unresolvedSignatureCount'])
    def test_enum_constant(self):
        r=member('public static field Dock.Side Left const=1','Left','Field');a=member(r['key'].replace('=1','=2'),'Left','Field')
        self.assertEqual(1,self.diff(inventory([a]),inventory([r]))['unresolvedSignatureCount'])
    def test_nullable_value_type(self): self.assertNotEqual(api.mapped('System.Int32?',{}),api.mapped('System.Int32',{}))
    def test_attributes_separate_from_matches(self):
        a=inventory();a['types'][0]['members'][0]['attributes']=['System.ObsoleteAttribute(){}'];d=self.diff(a)
        self.assertEqual(2,d['mappedSignatureMatches']);self.assertEqual(1,d['attributeDifferenceCount'])
    def test_parameter_attribute(self):
        a=inventory();a['types'][0]['members'][0]['parameters']=[{'attributes':['System.Runtime.InteropServices.InAttribute(){}']}]
        self.assertEqual(1,self.diff(a)['attributeDifferenceCount'])
    def test_compiler_state_machine_separated(self):
        a=inventory();a['types'][0]['members'][0]['attributes']=['System.Runtime.CompilerServices.IteratorStateMachineAttribute(typeof(Generated)){}']
        self.assertEqual([],self.diff(a)['diagnostics'])
    def test_base_shape(self):
        a=inventory();a['types'][0]['key']=a['types'][0]['key'].replace('System.Object','Other');self.assertEqual(1,self.diff(a)['counts']['typeShapeDifference'])
    def test_missing_type_accounts_for_members(self):
        a=inventory();a['types'][0]['name']='Other';d=self.diff(a);self.assertEqual(1,d['counts']['missingType']);self.assertEqual(1,d['counts']['missingTypeMember'])
    def test_old_implementation_schema_rejected(self):
        a=inventory();a['schema']=2
        with self.assertRaises(ValueError): self.diff(a)
    def test_integrity(self):
        with tempfile.TemporaryDirectory() as temp:
            p=Path(temp)/'api.json'
            for mutate in (lambda d:d.update(count=99), lambda d:d.update(sha256='wrong'),lambda d:d['declarations'].reverse(),lambda d:d['types'][0]['members'][0].update(key='wrong')):
                d=copy.deepcopy(inventory());mutate(d);p.write_text(json.dumps(d))
                with self.subTest(mutation=mutate),self.assertRaises(ValueError):api.load_inventory(p)
            p.write_text(json.dumps(inventory()));self.assertEqual(2,api.load_inventory(p)['count'])
    def test_determinism(self): self.assertEqual(json.dumps(self.diff(inventory()),sort_keys=True),json.dumps(self.diff(inventory()),sort_keys=True))
    def assertDifference(self,key): self.assertEqual(1,self.diff(inventory([member(key)]))['counts']['signatureDifference'])

if __name__=='__main__': unittest.main(verbosity=2)
