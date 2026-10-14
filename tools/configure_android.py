"""Configure temporary export-machine paths without committing credentials."""
import argparse
import json
from pathlib import Path

parser=argparse.ArgumentParser(description=__doc__)
for key in ("java","sdk","templates","keystore"):
    parser.add_argument("--"+key,required=True,type=Path)
args=parser.parse_args()
quote=lambda value: json.dumps(str(value.resolve()))
settings=Path.home()/".config/godot/editor_settings-4.5.tres"
settings.parent.mkdir(parents=True,exist_ok=True)
settings.write_text('[gd_resource type="EditorSettings" format=3]\n\n[resource]\n'+
    'export/android/java_sdk_path = '+quote(args.java)+'\n'+
    'export/android/android_sdk_path = '+quote(args.sdk)+'\n',encoding="utf-8")
preset=Path("client/export_presets.cfg")
text=preset.read_text()
text=text.replace('custom_template/debug=""','custom_template/debug='+quote(args.templates/"android_debug.apk"))
text=text.replace('custom_template/release=""','custom_template/release='+quote(args.templates/"android_release.apk"))
text+='\nkeystore/debug='+quote(args.keystore)+'\nkeystore/debug_user="androiddebugkey"\nkeystore/debug_password="android"\n'
preset.write_text(text,encoding="utf-8")
