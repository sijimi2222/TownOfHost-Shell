"""Extract unchanged production pet dispatch and movement composition for Idol tests.
Run after changing the corresponding production code; generated files are committed fixtures.
"""
from pathlib import Path
import hashlib, json
ROOT = Path(__file__).resolve().parents[2]
HERE = Path(__file__).resolve().parent

def block(source, marker):
    start = source.index(marker)
    opening = source.index("{", start)
    depth = 1
    end = opening + 1
    while depth:
        depth += (source[end] == "{") - (source[end] == "}")
        end += 1
    return source[start:end]

pet_path = ROOT / "Patches/Petactionpatch.cs"
speed_path = ROOT / "Modules/GameOptionsSender/PlayerGameOptionsSender.cs"
pet = pet_path.read_text(encoding="utf-8-sig")
speed = speed_path.read_text(encoding="utf-8-sig")
method = block(pet, "private static void OnPetUse(PlayerControl pc)")
method = method.replace("private static", "internal static", 1)
(HERE / "GeneratedPetDispatch.cs").write_text("// Extracted by RefreshIntegrationFixtures.py; do not edit.\nusing TownOfHost.Patches;namespace TownOfHost;internal static class RealPetDispatch{\n" + method + "\n}\n", encoding="utf-8-sig")
movement = block(speed, "if (Main.AllPlayerSpeed.TryGetValue(player.PlayerId, out var speed))")
(HERE / "GeneratedSpeedPipeline.cs").write_text("// Extracted by RefreshIntegrationFixtures.py; do not edit.\nusing TownOfHost.Roles.Core;using TownOfHost.Roles.AddOns.Common;using Mathf=UnityEngine.Mathf;namespace TownOfHost;internal static class RealSpeedPipeline{internal static float Build(PlayerControl player){var state=player.GetPlayerState();bool HasRoleAddon=false;var data=new RoleAddonStub();AURoleOptions.PlayerSpeedMod=1f;\n" + movement + "\nreturn AURoleOptions.PlayerSpeedMod;}}\n", encoding="utf-8-sig")
(HERE / "FixtureSources.json").write_text(json.dumps({str(path.relative_to(ROOT)).replace("\\", "/"): hashlib.sha256(path.read_bytes()).hexdigest().upper() for path in [pet_path, speed_path]}, indent=2) + "\n", encoding="utf-8")
