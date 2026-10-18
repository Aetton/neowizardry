import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
from tools import prepare_game,world_props,world_objects
from tools.slf import FormatError
from test_world_objects import fixture
from test_slf import fixture as archive_fixture

class PrepareGameTests(unittest.TestCase):
    def test_installation_case_matching_and_incomplete_rejection(self):
        with tempfile.TemporaryDirectory() as tmp:
            root=Path(tmp)/'Wizardry8';(root/'data'/'monsters').mkdir(parents=True);(root/'levels').mkdir()
            for path in ('data/data.slf','data/monsters/monsters.slf','levels/levels.slf'):(root/path).write_bytes(b'fixture')
            self.assertEqual(prepare_game.installation(root/'data')[0],root.resolve())
            (root/'levels'/'levels.slf').unlink()
            with self.assertRaises(FormatError):prepare_game.installation(root)
    def test_non_door_native_prop_pose_preserves_bounds_and_triangles(self):
        source=fixture(locked=False);objects=world_objects.parse(source,geometry=True)
        objects['props'][0]['trigger']=None
        with patch.object(world_objects,'parse',return_value=objects):payload,manifest=world_props.build(source)
        self.assertEqual((len(manifest['props']),manifest['triangles']),(1,1))
        self.assertTrue(payload.startswith(b'glTF'));self.assertFalse(manifest['blocked'])
    def test_prepared_cache_checks_hashes_and_refuses_empty_or_corrupt_markers(self):
        with tempfile.TemporaryDirectory() as tmp:
            root=Path(tmp);files={}
            for name in ('monastery1.glb','creature-282.nwcreature.json','items.json','monsters.json'):
                (root/name).write_bytes(b'authored');files[name]=prepare_game.file_hash(root/name)
            marker=root/'game-content.json';marker.write_text(json.dumps({'schema':1,'fingerprint':'key','files':files}))
            self.assertTrue(prepare_game.ready(root,'key'))
            (root/'monastery1.glb').write_bytes(b'tampered')
            self.assertFalse(prepare_game.ready(root,'key'))
            marker.write_text(json.dumps({'schema':1,'fingerprint':'key','files':{}}))
            self.assertFalse(prepare_game.ready(root,'key'))
            marker.write_text('{bad')
            self.assertFalse(prepare_game.ready(root,'key'))

    def test_door_geometry_is_not_duplicated_as_furniture(self):
        payload,manifest=world_props.build(fixture(locked=False))
        self.assertEqual(payload,b'');self.assertEqual(manifest['props'],[])

if __name__=='__main__':unittest.main()
