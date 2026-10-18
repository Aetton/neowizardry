import unittest
from tools.world_items import model
from tools.slf import FormatError
from test_world_objects import mesh

class WorldItemsTest(unittest.TestCase):
    def test_native_itm_version_mesh_transform_and_bounds(self):
        data=b'\2'+mesh();level=model(data)
        self.assertEqual(level.meshes[0].positions,[[0,0,0],[0,3000,0],[0,0,3000]])
        self.assertEqual(level.meshes[0].indices,[(0,1,2)])
        for end in range(len(data)):
            with self.assertRaises(FormatError):model(data[:end])
        for bad in (data+b'x',b'\1'+data[1:],b'\2'+mesh(compressed=True)):
            with self.assertRaises(FormatError):model(bad)

    def test_export_preserves_placements_and_reuses_original_models(self):
        import hashlib
        from pathlib import Path
        import tempfile
        from unittest.mock import patch
        from tools import world_items
        row={'id':3,'definition_id':0,'position':[10,20,30],'stack_count':21,'charges':0,
             'identified':False,'flags':0,'instance_flags':[0]*5}
        raw=bytearray(269)  # Empty internal name uses native appearance 0: Dagger.
        catalog={'items':[{'id':0,'instance':{'raw_record_hex':raw.hex()}}]}
        class Source:
            path=Path('/original/Levels/LEVELS.SLF')
            def read(self,name):return name.encode()
        class Resources:
            def read(self,name):
                if name!='ITEMS3D/Dagger.ITM':raise KeyError(name)
                return b'\2'+mesh()
        with tempfile.TemporaryDirectory() as tmp, patch.object(world_items,'Archive',return_value=Source()), patch.object(world_items,'item_catalog',return_value=catalog), patch.object(world_items.level_status,'parse',return_value={
                'sha256':'c'*64,'levels':[{'level':8,'items':[[row],[dict(row,id=4,flags=1)]]}]}):
            result=world_items.export(Path('/unused'),Path(tmp),Resources())
            self.assertEqual([m['location_id'] for m in result['members']],[3,4])
            self.assertEqual([m['hidden'] for m in result['members']],[False,True])
            self.assertEqual(result['members'][0]['position'],dict(x=10,y=20,z=30))
            self.assertEqual(result['members'][0]['stack_count'],21)
            models=list(Path(tmp).glob('*.glb'));self.assertEqual(len(models),1)
            self.assertEqual(result['members'][1]['mesh_sha256'],hashlib.sha256(models[0].read_bytes()).hexdigest())
