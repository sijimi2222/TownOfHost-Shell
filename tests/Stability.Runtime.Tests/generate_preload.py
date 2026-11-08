from pathlib import Path
import hashlib
p=Path(__file__).resolve().parent
s=(p/'../../Patches/ClientPatch.cs').resolve().read_text(encoding='utf-8-sig')
start=s.index('    [HarmonyPatch(typeof(AmongUsClient))]\n    class PreloadMapPatch')
end=s.rfind('\n}')
body=s[start:end]
sha=hashlib.sha256(body.encode()).hexdigest().upper()
out='// Generated from actual ClientPatch.cs; do not hand edit.\nusing System;using System.Collections;using System.Collections.Generic;using HarmonyLib;using UnityEngine;using UnityEngine.ResourceManagement.AsyncOperations;\nnamespace TownOfHost {\ninternal static class PreloadBodyHash {internal const string Value="'+sha+'";}\n'+body+'\n}\n'
(p/'GeneratedPreload.cs').write_text(out,encoding='utf-8')
print('PRELOAD_BODY_SHA256',hashlib.sha256(body.encode()).hexdigest())
