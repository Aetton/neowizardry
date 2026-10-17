import hashlib
import json
import math
import struct
import unittest

from test_world_objects import fixture
from tools import world_doors, world_objects
from tools.slf import FormatError


class DoorTest(unittest.TestCase):
    def test_world_frames_uv_and_source_binding(self):
        data=fixture()
        payload, manifest, report=world_doors.build(data)
        self.assertEqual(manifest['pvl_sha256'],hashlib.sha256(data).hexdigest())
        self.assertEqual(manifest['mesh_sha256'],hashlib.sha256(payload).hexdigest())
        self.assertEqual((report['doors'],report['frames'],report['triangles']),(1,2,2))
        self.assertEqual(report['interactive'],0)
        self.assertIn('locked',manifest['doors'][0]['blocked_reason'])
        self.assertEqual(report['frame_bounds'][0],[[0.0,0.0,-0.65],[0.65,0.0,-0.0]])
        self.assertAlmostEqual(report['frame_bounds'][1][0][0],1.3)
        length=struct.unpack_from('<I',payload,12)[0]
        doc=json.loads(payload[20:20+length])
        self.assertEqual(len(doc['nodes']),2)
        self.assertEqual(len(doc['materials']),1)
        self.assertEqual(doc['accessors'][1]['count'],3)

    def test_axis_rotation_and_reject_unsupported_axes(self):
        result=world_doors.rotate([1,0,0],math.pi/2,[0,1,0])
        for actual,expected in zip(result,[0,0,-1]): self.assertAlmostEqual(actual,expected)
        with self.assertRaises(FormatError): world_doors.rotate([1,0,0],1,[0,0,0])

    def test_interaction_requirements_fail_closed(self):
        objects=world_objects.parse(fixture())
        trigger=objects['props'][0]['trigger']
        trigger['door']['locked']=False
        trigger['door']['linked_trigger']=''
        self.assertEqual(world_doors.restriction(trigger),'')
        for key,value in [('required_item','42'),('required_states','QUEST'),('modify_state','QUEST'),('recipients','OTHER')]:
            trigger[key]=value
            self.assertTrue(world_doors.restriction(trigger),key)
            trigger[key]=''
        for key in ('locked','jammed','one_way','auto_locking'):
            trigger['door'][key]=True
            self.assertTrue(world_doors.restriction(trigger),key)
            trigger['door'][key]=False
        trigger['door']['secret']=True;trigger['door']['found']=False
        self.assertTrue(world_doors.restriction(trigger))


if __name__=='__main__': unittest.main()
