using System.Collections.Generic;
using System.Linq;
using AmongUs.GameOptions;
using Hazel;
using TownOfHost.Roles.Core;
using TownOfHost.Roles.Core.Interfaces;
using UnityEngine;

namespace TownOfHost.Roles.Impostor;

public sealed class Destroyer : RoleBase, IKiller
{
    public static readonly SimpleRoleInfo RoleInfo =
        SimpleRoleInfo.Create(
            typeof(Destroyer),
            player => new Destroyer(player),
            CustomRoles.Destroyer,
            () => RoleTypes.Phantom,
            CustomRoleTypes.Impostor,
            39991,
            SetupOptionItem,
            "Ds",
            "#ff1919",
            (0, 8),
            from: From.NebulaontheShip
        );

    // ===== 設定 =====

    static OptionItem OptKillCooldown;
    static OptionItem OptShowKillFlash;
    static OptionItem OptShowCrushMark;
    static OptionItem OptCrushTime;
    static OverrideKilldistance KillDistanceOption;

    // ===== 押し潰し中の状態 =====

    private PlayerControl crushTarget;
    private float destroyerOldSpeed;
    private float targetOldSpeed;
    private bool isCrushing;
    private readonly List<GameObject> crushMarks = new();
    private Vector2 crushPosition;
    

    public Destroyer(PlayerControl player)
        : base(RoleInfo, player)
    {
        CustomRoleManager.MarkOthers.Add(GetMarkOthers);
    }

    public float CalculateKillCooldown()
    {
        return OptKillCooldown.GetFloat();
    }

    public bool CanUseSabotageButton() => true;

    

    
        public void OnCheckMurderAsKiller(MurderInfo info)
    {
        if (isCrushing)
        {
            info.DoKill = false;
            return;
        }

        if (!Is(info.AttemptKiller) || info.IsSuicide)
            return;

        info.DoKill = false;

        var (_, target) = info.AttemptTuple;

        if (target == null)
            return;

        LockPlayers(target);

        

        _ = new LateTask(() =>
        {
            if (!isCrushing)
                return;

            if (crushTarget == null)
                return;

            if (!crushTarget.IsAlive())
                return;

            var target = crushTarget;

            // キルする前に固定を解除する
            UnlockPlayers();

            // 押し潰し成功
            Player.RpcMurderPlayerV2(target);

            _ = new LateTask(() =>
            {
                Main.AllPlayerSpeed[Player.PlayerId] = destroyerOldSpeed;
                Main.AllPlayerSpeed[target.PlayerId] = targetOldSpeed;

                Player.MarkDirtySettings();
                target.MarkDirtySettings();
            },
0.2f,
"Destroyer.RestoreSpeed");

            if (OptShowKillFlash.GetBool())
            {
                target.KillFlash();
            }

            CreateCrushMark(target);

            if (ReportDeadBodyPatch.IgnoreBodyids != null)
                ReportDeadBodyPatch.IgnoreBodyids[target.PlayerId] = true;

            

        },
OptCrushTime.GetFloat(),
"Destroyer.Crush");
    }
    private void SendCrushRpc(bool crushing, byte targetId)
    {
        using var sender = CreateSender();
        sender.Writer.Write(crushing);
        sender.Writer.Write(targetId);
    }

    public override bool GetTemporaryName(
    ref string name,
    ref bool NoMarker,
    bool isForMeeting,
    PlayerControl seer,
    PlayerControl seen = null)
    {
        seen ??= seer;

        if (isForMeeting)
            return false;

        if (!isCrushing || crushTarget == null)
            return false;

        if (seen.PlayerId != crushTarget.PlayerId)
            return false;

        string destroyerColor =
            "#" + ColorUtility.ToHtmlStringRGB(
                Palette.PlayerColors[Player.Data.DefaultOutfit.ColorId]);

        name =
            $"<line-height=1200%>\n" +
            $"<size=500%><color={destroyerColor}>■</color></size>\n" +
            $"</line-height>" +
            name;

        NoMarker = true;
        return true;
    }

    public static string GetMarkOthers(
    PlayerControl seer,
    PlayerControl seen = null,
    bool isForMeeting = false)
    {
        seen ??= seer;

        if (isForMeeting)
            return "";

        var destroyer = PlayerCatch.AllPlayerControls
            .FirstOrDefault(p => p.GetRoleClass() is Destroyer d && d.isCrushing && d.crushTarget?.PlayerId == seen.PlayerId);

        if (destroyer == null)
            return "";

        string color =
            "#" + ColorUtility.ToHtmlStringRGB(
                Palette.PlayerColors[destroyer.Data.DefaultOutfit.ColorId]);

        return $"<color={color}>■</color>";
    }

    
    
    private void LockPlayers(PlayerControl target)
    {
        if (target == null)
            return;

        crushTarget = target;
        isCrushing = true;
        crushPosition = target.GetTruePosition();

        
        SendCrushRpc(true, target.PlayerId);

        foreach (var seer in PlayerCatch.AllPlayerControls)
        {
            NameColorManager.Add(seer.PlayerId, Player.PlayerId, "ff1919");
            NameColorManager.Add(seer.PlayerId, target.PlayerId, "ff1919");
        }

        UtilsOption.MarkEveryoneDirtySettings();
        UtilsNotifyRoles.NotifyRoles();

        if (Main.AllPlayerSpeed.TryGetValue(Player.PlayerId, out var mySpeed))
            destroyerOldSpeed = mySpeed;
        else
            destroyerOldSpeed = Main.RealOptionsData.GetFloat(FloatOptionNames.PlayerSpeedMod);

        if (Main.AllPlayerSpeed.TryGetValue(target.PlayerId, out var tSpeed))
            targetOldSpeed = tSpeed;
        else
            targetOldSpeed = Main.RealOptionsData.GetFloat(FloatOptionNames.PlayerSpeedMod);

        Main.AllPlayerSpeed[Player.PlayerId] = 0.0001f;
        Main.AllPlayerSpeed[target.PlayerId] = 0.0001f;

        Player.MarkDirtySettings();
        target.MarkDirtySettings();
    }

    private void CreateCrushMark(PlayerControl target)
    {
        if (!OptShowCrushMark.GetBool())
            return;

        if (target == null)
            return;

        try
        {
            var obj = new GameObject($"DestroyerCrushMark_{target.PlayerId}");
            obj.transform.position = new Vector3(
                crushPosition.x,
                crushPosition.y,
                1000f
            );
            obj.layer = 5;

            var sr = obj.AddComponent<SpriteRenderer>();

            var tex = new Texture2D(8, 8);
            var pixels = new Color[64];

            var playerColor =
                Palette.PlayerColors[target.Data.DefaultOutfit.ColorId];

            for (int i = 0; i < pixels.Length; i++)
                pixels[i] = playerColor;

            tex.SetPixels(pixels);
            tex.Apply();

            sr.sprite = Sprite.Create(
                tex,
                new Rect(0, 0, 8, 8),
                new Vector2(0.5f, 0.5f),
                100f
            );

            obj.transform.localScale = Vector3.one * 0.25f;

            crushMarks.Add(obj);
        }
        catch (System.Exception e)
        {
            Logger.Error(e.ToString(), "Destroyer.CrushMark");
        }
    }

    private void ClearCrushMarks()
    {
        foreach (var obj in crushMarks)
        {
            if (obj != null)
                UnityEngine.Object.Destroy(obj);
        }

        crushMarks.Clear();
    }

    public override void OnMurderPlayerAsTarget(MurderInfo info)
    {
        if (isCrushing)
            UnlockPlayers();
    }

    public override void OnStartMeeting()
    {
        ClearCrushMarks();

        if (isCrushing)
            UnlockPlayers();
    }

    private void UnlockPlayers()
    {
        if (crushTarget == null)
            return;

        Main.AllPlayerSpeed[Player.PlayerId] = destroyerOldSpeed;
        Main.AllPlayerSpeed[crushTarget.PlayerId] = targetOldSpeed;

        Player.MarkDirtySettings();
        crushTarget.MarkDirtySettings();

        foreach (var seer in PlayerCatch.AllPlayerControls)
        {
            NameColorManager.Remove(seer.PlayerId, Player.PlayerId);
        }

        if (crushTarget != null)
            SendCrushRpc(false, crushTarget.PlayerId);

        isCrushing = false;
        crushTarget = null;
    }
    private static void SetupOptionItem()
    {
        OptKillCooldown = FloatOptionItem.Create(
    RoleInfo,
    10,
    GeneralOption.KillCooldown,
    new(30f, 40f, 1f),
    30f,
    false
).SetValueFormat(OptionFormat.Seconds)
 .SetOptionName(() => "キルクール");

        OptShowKillFlash = BooleanOptionItem.Create(
    RoleInfo,
    11,
    GeneralOption.TaskAwakening,
    true,
    false
).SetOptionName(() => "押し潰しが成功した場合キルフラッシュを鳴らす");

        OptShowCrushMark = BooleanOptionItem.Create(
    RoleInfo,
    12,
    GeneralOption.TaskAwakening,
    true,
    false
).SetOptionName(() => "押し潰した地点にマークを付ける");

        OptCrushTime = FloatOptionItem.Create(
    RoleInfo,
    13,
    GeneralOption.TaskAwakening,
    new(3f, 15f, 1f),
    5f,
    false
).SetValueFormat(OptionFormat.Seconds)
 .SetOptionName(() => "押し潰すのに必要な時間");


    }
}