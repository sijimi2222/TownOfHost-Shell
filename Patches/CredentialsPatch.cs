using System.Globalization;

using System.Text;

using HarmonyLib;

using TMPro;

using UnityEngine;



using TownOfHost.Modules;

using TownOfHost.Roles.Core;

using TownOfHost.Templates;

using static TownOfHost.Translator;

using System.Linq;

using System.Collections.Generic;

using Il2CppInterop.Runtime.InteropTypes.Arrays;



namespace TownOfHost

{

    [HarmonyPatch]

    public static class CredentialsPatch

    {

        public static SpriteRenderer TOHhmLogo { get; private set; }

        private static TextMeshPro pingTrackerCredential = null;

        private static AspectPosition pingTrackerCredentialAspectPos = null;

        private static float deltaTime = 0.0f;

        public static bool a = false;

        public static List<float> fpss = new();



        // GetWorldCorners writes an IL2CPP array. A managed Vector3[] would be copied
        // into a temporary native array, leaving the original managed corners at zero.
        private static Il2CppStructArray<Vector3> headerWorldCorners;
        private static bool headerPositioning, headerMissingGuideLogged, headerFailureLogged;
        private static int headerDiagnosticCount;
        private static bool headerLayoutValid, headerMeshPending, headerLastHasGuide;
        private static string headerLastText;
        private static float headerLastOffset, headerLastAspect, headerLastSize;
        private static Vector3 headerLastScale, headerLastEdge;
        private static int headerLastPhase = -1;
        private static HudManager headerLastHud;
        private static Camera headerLastCamera;
        private static Transform headerLastHudParent;
        private static Vector3 headerLastHudScale;
#if SHELL_DEBUG_CONTROLLER
        private static int headerTraceCount, headerTracedPhase = -1;
        private static int headerTracedHud, headerTracedCamera, headerTracedParent;
#endif

        private static bool BindHeaderToHud()
        {
            if (pingTrackerCredential == null || pingTrackerCredentialAspectPos == null
                || !HudManager.InstanceExists || HudManager.Instance.UICamera == null) return false;
            var hud = HudManager.Instance;
            // The label and HELP must share the HUD's coordinate space and lifetime.
            if (pingTrackerCredential.transform.parent != hud.transform)
            {
                pingTrackerCredential.transform.SetParent(hud.transform, false);
                headerLayoutValid = false;
            }
            if (pingTrackerCredentialAspectPos.parentCam != hud.UICamera)
            {
                pingTrackerCredentialAspectPos.parentCam = hud.UICamera;
                headerLayoutValid = false;
            }
            if (pingTrackerCredentialAspectPos.Alignment != AspectPosition.EdgeAlignments.RightTop)
            {
                pingTrackerCredentialAspectPos.Alignment = AspectPosition.EdgeAlignments.RightTop;
                headerLayoutValid = false;
            }
            // Native resolution notifications still update the anchor. Camera motion does not need polling.
            if (pingTrackerCredentialAspectPos.updateAlways) pingTrackerCredentialAspectPos.updateAlways = false;
            return true;
        }

#if SHELL_DEBUG_CONTROLLER
        private static void TraceHeaderSpace(int phase, HudManager hud, bool measured, float guideLeft, float rightOffset, float shift)
        {
            if (headerTraceCount >= 12) return;
            var camera = hud.UICamera;
            int hudId = hud.GetInstanceID(), cameraId = camera.GetInstanceID();
            int parentId = hud.transform.parent != null ? hud.transform.parent.GetInstanceID() : 0;
            if (headerTracedPhase == phase && headerTracedHud == hudId
                && headerTracedCamera == cameraId && headerTracedParent == parentId) return;
            headerTraceCount++; headerTracedPhase = phase; headerTracedHud = hudId;
            headerTracedCamera = cameraId; headerTracedParent = parentId;
            var text = pingTrackerCredential.transform;
            var local = text.localPosition; var world = text.position; var cam = camera.transform.position;
            var viewport = camera.WorldToViewportPoint(world); var scale = hud.transform.lossyScale;
            var edge = pingTrackerCredentialAspectPos.DistanceFromEdge;
            var corner = measured && headerWorldCorners != null ? headerWorldCorners[0] : Vector3.zero;
            Logger.Info(System.FormattableString.Invariant($"HEADER_SPACE_V6 phase={phase};hud={hudId};hudParent={parentId};labelParent={text.parent.GetInstanceID()};camera={cameraId};local={local.x:F3},{local.y:F3},{local.z:F3};world={world.x:F3},{world.y:F3},{world.z:F3};cameraWorld={cam.x:F3},{cam.y:F3},{cam.z:F3};hudScale={scale.x:F3},{scale.y:F3},{scale.z:F3};size={camera.orthographicSize:F3};aspect={camera.aspect:F3};viewport={viewport.x:F3},{viewport.y:F3};edge={edge.x:F3},{edge.y:F3},{edge.z:F3};measured={measured};corner0={corner.x:F3},{corner.y:F3},{corner.z:F3};guideLeft={guideLeft:F3};baseOffset={rightOffset:F3};shift={shift:F3}"), "HeaderLayout");
        }
#endif

        private static void KeepHeaderClearOfGuide(bool refreshMesh = false)
        {
            if (headerPositioning || !BindHeaderToHud() || !pingTrackerCredential.gameObject.activeInHierarchy) return;
            var hud = HudManager.Instance;
            var camera = hud.UICamera;
            int phase = GameStates.IsLobby ? 1 : GameStates.Intro ? 2 : GameStates.IsMeeting ? 3 : GameStates.IsInGame ? 4 : 0;
            bool hasGuide = RoleGuideButtonPatch.TryGetHeaderLeftEdge(hud.transform, out var guideLeft);
            float rightOffset = hud.Chat != null && hud.Chat.chatButton.gameObject.activeInHierarchy ? 2.5f : 1.8f;
            if (RoleGuideButtonPatch.HasGuideButton) rightOffset += .72f;
            if (RoleGuideButtonPatch.TryGetHeaderRightOffset(out var measuredOffset)) rightOffset = measuredOffset;
            var edge = pingTrackerCredentialAspectPos.DistanceFromEdge;
            bool changed = !headerLayoutValid || headerLastPhase != phase || headerLastHud != hud
                || headerLastCamera != camera || headerLastHudParent != hud.transform.parent || headerLastHudScale != hud.transform.lossyScale
                || headerLastHasGuide != hasGuide || headerLastText != pingTrackerCredential.text
                || Mathf.Abs(headerLastOffset - rightOffset) > .001f
                || headerLastAspect != camera.aspect || headerLastSize != camera.orthographicSize
                || headerLastScale != pingTrackerCredential.transform.localScale || headerLastEdge != edge;
            if (!changed && !(refreshMesh && (headerMeshPending || pingTrackerCredential.havePropertiesChanged))) return;
            headerPositioning = true;
            try
            {
                // Rebase only when text/geometry changes. Never subtract from the previous frame's world position.
                if (refreshMesh) pingTrackerCredential.ForceMeshUpdate();
                // Preserve Shell's original top/depth offsets (0, 0), not PingTracker's lobby/game gaps.
                pingTrackerCredentialAspectPos.DistanceFromEdge = new(rightOffset, 0, 0);
                pingTrackerCredentialAspectPos.AdjustPosition();
                float shift = 0;
                if (hasGuide)
                {
                    var space = hud.transform;
                    headerWorldCorners ??= new Il2CppStructArray<Vector3>(4);
                    pingTrackerCredential.rectTransform.GetWorldCorners(headerWorldCorners);
                    float right = float.NegativeInfinity;
                    for (int i = 0; i < 4; i++) right = Mathf.Max(right, space.InverseTransformPoint(headerWorldCorners[i]).x);
                    var bounds = pingTrackerCredential.textBounds;
                    var transform = pingTrackerCredential.transform;
                    for (int i = 0; i < 4; i++)
                    {
                        var point = new Vector3((i & 2) == 0 ? bounds.min.x : bounds.max.x,
                            (i & 1) == 0 ? bounds.min.y : bounds.max.y, bounds.center.z);
                        right = Mathf.Max(right, space.InverseTransformPoint(transform.TransformPoint(point)).x);
                    }
                    shift = HudHeaderLayout.RequiredLeftShift(right, guideLeft);
                    if (shift > .001f)
                    {
                        // Store the correction in the native anchor, not a persistent world-space displacement.
                        pingTrackerCredentialAspectPos.DistanceFromEdge = new(rightOffset + shift, 0, 0);
                        pingTrackerCredentialAspectPos.AdjustPosition();
                    }
                }
                headerLastText = pingTrackerCredential.text;
                headerLastOffset = rightOffset; headerLastAspect = camera.aspect; headerLastSize = camera.orthographicSize;
                headerLastScale = pingTrackerCredential.transform.localScale;
                headerLastEdge = pingTrackerCredentialAspectPos.DistanceFromEdge;
                headerLastPhase = phase; headerLastHud = hud; headerLastCamera = camera;
                headerLastHudParent = hud.transform.parent; headerLastHudScale = hud.transform.lossyScale;
                headerLayoutValid = true; headerMeshPending = !refreshMesh; headerLastHasGuide = hasGuide;
                if (refreshMesh && !hasGuide && !headerMissingGuideLogged)
                {
                    headerMissingGuideLogged = true;
                    Logger.Info("HEADER_LAYOUT_V6_NO_GUIDE", "HeaderLayout");
                }
                if (refreshMesh && hasGuide && headerDiagnosticCount < 3)
                {
                    headerDiagnosticCount++;
                    Logger.Info(System.FormattableString.Invariant($"HEADER_LAYOUT_V6 hudLocal=true;offset={rightOffset:F3};shift={shift:F3};top={headerLastEdge.y:F3};depth={headerLastEdge.z:F3}"), "HeaderLayout");
                }
#if SHELL_DEBUG_CONTROLLER
                if (refreshMesh) TraceHeaderSpace(phase, hud, hasGuide, guideLeft, rightOffset, shift);
#endif
            }
            catch (System.Exception)
            {
                headerLayoutValid = false;
                if (refreshMesh && !headerFailureLogged)
                {
                    headerFailureLogged = true;
                    Logger.Warn("HEADER_LAYOUT_V6_DEFERRED", "HeaderLayout");
                }
            }
            finally { headerPositioning = false; }
        }

        [HarmonyPatch(typeof(ModManager), nameof(ModManager.LateUpdate)), HarmonyPostfix, HarmonyPriority(Priority.Last)]
        private static void HeaderLateLayout()
        {
            // Resolve changed text/geometry after native updates; an unchanged header needs no mesh or position write.
            KeepHeaderClearOfGuide(true);
        }

        [HarmonyPatch(typeof(AspectPosition), nameof(AspectPosition.AdjustPosition), new System.Type[] { })]
        private static class HeaderAnchorAdjustedPatch
        {
            static void Postfix(AspectPosition __instance)
            {
                // Do not change any other label or UI anchor.
                if (__instance != null && __instance == pingTrackerCredentialAspectPos && !headerPositioning)
                {
                    headerLayoutValid = false;
                    KeepHeaderClearOfGuide();
                }
            }
        }

        [HarmonyPatch(typeof(AspectPosition), nameof(AspectPosition.AdjustPosition), new System.Type[] { typeof(float) })]
        private static class HeaderAnchorAspectAdjustedPatch
        {
            static void Postfix(AspectPosition __instance)
            {
                if (__instance != null && __instance == pingTrackerCredentialAspectPos && !headerPositioning)
                {
                    headerLayoutValid = false;
                    KeepHeaderClearOfGuide();
                }
            }
        }

        [HarmonyPatch(typeof(PingTracker), nameof(PingTracker.Update))]

        class PingTrackerUpdatePatch

        {

            static StringBuilder sb = new();

            static StringBuilder exSb = new();

            static void Postfix(PingTracker __instance)

            {

                if (!HudManager.InstanceExists || HudManager.Instance.UICamera == null) return;

                if (pingTrackerCredential == null)

                {

                    var uselessPingTracker = Object.Instantiate(__instance, HudManager.Instance.transform);

                    pingTrackerCredential = uselessPingTracker.GetComponent<TextMeshPro>();

                    Object.Destroy(uselessPingTracker);

                    pingTrackerCredential.alignment = TextAlignmentOptions.TopRight;

                    pingTrackerCredential.rectTransform.pivot = new(1f, 0.7f);  // 中心を右上角に設定

                    pingTrackerCredentialAspectPos = pingTrackerCredential.GetComponent<AspectPosition>();

                    pingTrackerCredentialAspectPos.Alignment = AspectPosition.EdgeAlignments.RightTop;

                    pingTrackerCredential.gameObject.name = "CredentialText";
                    headerDiagnosticCount = 0; headerMissingGuideLogged = headerFailureLogged = false;
                    headerLayoutValid = false; headerMeshPending = true;
#if SHELL_DEBUG_CONTROLLER
                    headerTraceCount = 0; headerTracedPhase = -1;
#endif



                    if (GameStates.IsFreePlay && !uselessPingTracker.gameObject.active)

                        uselessPingTracker.gameObject.SetActive(true);

                }

                KeepHeaderClearOfGuide();

                if ((FixedUpdatePatch.timer is 1 && GameStates.InGame) || GameStates.IsLobby || GameStates.IsFreePlay)

                {

                    sb.Clear();



                    sb.Append("\r\n").Append($"<color=#aa00ff>TownOfHost-Shell</color> v{Main.PluginShowVersion}");

                    if (Main.DebugVersion) sb.Append($"<{Main.ModColor}>☆Debug☆</color>");



                    if (!GameStates.IsFreePlay)

                    {

                        if ((Options.NoGameEnd.OptionMeGetBool() && GameStates.IsLobby) || (Main.DontGameSet && !GameStates.IsLobby)) sb.Append($"\r\n").Append(Utils.ColorString(Color.red, GetString("NoGameEnd")));



                        switch (Options.CurrentGameMode)

                        {

                            case CustomGameMode.StandardHAS: sb.Append($"\r\n").Append(Utils.ColorString(Color.yellow, GetString("StandardHAS"))); break;

                            case CustomGameMode.HideAndSeek: sb.Append($"\r\n").Append(Utils.ColorString(Color.red, GetString("HideAndSeek"))); break;

                            case CustomGameMode.TaskBattle: sb.Append($"\r\n").Append(Utils.ColorString(Color.cyan, GetString("TaskBattle"))); break;

                            case CustomGameMode.SuddenDeath: sb.Append("\r\n").Append(Utils.ColorString(UtilsRoleText.GetRoleColor(CustomRoles.Comebacker), GetString("SuddenDeathMode"))); break;

                            case CustomGameMode.MurderMystery: sb.Append("\r\n").Append($"<#1a389c>{GetString("MurderMystery")}"); break;

                        }

                        if (Options.EnableGM.OptionMeGetBool()) sb.Append($"\r\n").Append(Utils.ColorString(UtilsRoleText.GetRoleColor(CustomRoles.GM), GetString("GM")));

                        if (!GameStates.IsModHost) sb.Append($"\r\n").Append(Utils.ColorString(Color.red, GetString("Warning.NoModHost")));

                        if (DebugModeManager.IsDebugMode)

                        {

                            sb.Append("\r\n");

                            sb.Append(DebugModeManager.EnableTOHhmDebugMode.OptionMeGetBool() ? "<#0066de>DebugMode</color>" : Utils.ColorString(Color.green, "デバッグモード"));

                        }



                        exSb.Clear();



                        // #ffef39

                        if (Options.ExHideChatCommand.GetBool())

                            exSb.Append($"<#ffdfaf>Ⓗ</color> ");

                        if (Options.ExAftermeetingflash.GetBool())

                            exSb.Append($"<#d62c12>Ⓚ</color> ");

                        if (Options.FixSpawnPacketSize.GetBool())

                            exSb.Append($"<#ffef39>Ⓟ</color> ");

                        if (Options.ExRpcWeightR.GetBool())

                            exSb.Append($"<#3d83c5>Ⓡ</color> ");



                        if (exSb.Length > 0)

                        {

                            sb.Append("\r\n<size=50%>").Append(exSb).Append("</size>");

                        }

                    }

                    else if (CustomSpawnEditor.ActiveEditMode)

                    {

                        sb.Append($"\r\n").Append(Utils.ColorString(Color.cyan, GetString("ED.CSE")));

                    }



                    if (GameStates.IsLobby)

                    {

                        if (Options.IsStandardHAS && !CustomRoles.Sheriff.IsEnable() && !CustomRoles.SerialKiller.IsEnable() && CustomRoles.Egoist.IsEnable())

                            sb.Append($"\r\n").Append(Utils.ColorString(Color.red, GetString("Warning.EgoistCannotWin")));

                    }



                    pingTrackerCredential.text = sb.ToString();

                }

#if DEBUG

                if (Main.ViewPingDetails.Value)

                {

                    var serverName = GameStates.IsOnlineGame ? (Main.IsCs() ? ServerManager.Instance.CurrentRegion.Name : GetString(ServerManager.Instance.CurrentRegion.TranslateName)) : "ローカル";

                    deltaTime += (Time.unscaledDeltaTime - deltaTime) * 0.1f;

                    float fps = 1.0f / deltaTime;

                    __instance.text.text += $"({AmongUsClient.Instance.Ping / 1000f}秒/{serverName}/FPS: {Mathf.Ceil(fps)})\n";

                    __instance.text.alignment = TextAlignmentOptions.Top;

                    if (a) fpss.Add(fps);

                }

                else __instance.text.alignment = TextAlignmentOptions.TopLeft;

#endif

            }

        }

        public static string Subver;

        [HarmonyPatch(typeof(VersionShower), nameof(VersionShower.Start))]

        class VersionShowerStartPatch

        {

            static TextMeshPro SpecialEventText;

            static void Postfix(VersionShower __instance)

            {

                if (!__instance) return;



                TMPTemplate.SetBase(__instance.text);



                CreateText();



                ErrorText.Create(__instance.text);

                if (Main.hasArgumentException && ErrorText.Instance != null)

                {

                    ErrorText.Instance.AddError(ErrorCode.Main_DictionaryError);

                }



                VersionChecker.Check();

#if DEBUG

                if (OptionItem.IdDuplicated)

                {

                    // ErrorText.Instance.AddError(ErrorCode.OptionIDDuplicate);

                }

#endif



                if (SpecialEventText == null && TOHhmLogo != null)

                {

                    SpecialEventText = TMPTemplate.Create(

                        "SpecialEventText",

                        "",

                        Color.white,

                        alignment: TextAlignmentOptions.Center,

                        parent: TOHhmLogo.transform);

                    SpecialEventText.name = "SpecialEventText";

                    SpecialEventText.fontSizeMin = 3f;

                    SpecialEventText.transform.localPosition = new Vector3(0f, 0.8f, 0f);

                    SpecialEventText?.gameObject?.SetActive(true);

                }

                if (!SpecialEventText) return;

                SpecialEventText.enabled = TitleLogoPatch.amongUsLogo != null;

                if (Event.IsInitialRelease)

                {

                    SpecialEventText.text = $"Happy Birthday to {Main.ModName}!";

                    if (ColorUtility.TryParseHtmlString(Main.ModColor, out var col))

                    {

                        SpecialEventText.color = col;

                    }

                }

                if (Event.IsChristmas && CultureInfo.CurrentCulture.Name == "ja-JP")

                {

                    //このソースコ―ドを見た人へ。口外しないでもらえると嬉しいです...

                    //To anyone who has seen this source code. I would appreciate it if you would keep your mouth shut...

                    SpecialEventText.text = $"何とは言いませんが、特別な日ですね。\n<size=15%>\n\n末永く爆発しろ</size>";

                    SpecialEventText.color = UtilsRoleText.GetRoleColor(CustomRoles.Lovers);

                }

                MainMenuManagerPatch.Statistics_TMP = TMPTemplate.Create(

                "Statistisc",

                "",

                Color.white,

                3f,

                TextAlignmentOptions.TopLeft,

                false,

                null

                );

                {

                    MainMenuManagerPatch.Statistics_TMP.transform.localPosition = new Vector3(0.8f, 1.7f);

                }

                CreateStreameMenu.CreateText();

            }

        }



        public static TextMeshPro CreateText()

        {

            var Debugver = "";

            if (Main.DebugVersion) Debugver = $"<{Main.ModColor}>☆Debug☆</color>";

            Subver = "";

            Main.credentialsText = $"<{Main.ModColor}>{Main.ModName}</color> v{Main.PluginShowVersion}" + Debugver;

#if DEBUG

            // if (!GameStates.InGame) Main.credentialsText += $"\n<{Main.ModColor}>{ThisAssembly.Git.Branch}({ThisAssembly.Git.Commit})</color>";

#endif

            var credentials = TMPTemplate.Create(

                "TOHCredentialsText",

                Main.credentialsText,

                fontSize: 2f,

                alignment: TextAlignmentOptions.Right,

                setActive: true);

            credentials.transform.position = new Vector3(2.3419f, 2.29f, -5f);

#if DEBUG

            if (!GameStates.InGame) credentials.transform.position -= new Vector3(0f, 0.1218f, 0f);

#endif

            if (FindAGameManager._instance)

            {

                credentials.transform.position = new Vector3(2.5f, -2.858f, 5f);

#if DEBUG

                credentials.transform.position += new Vector3(0, 0.185f);

#endif

            }

            return credentials;

        }



        [HarmonyPatch(typeof(MainMenuManager), nameof(MainMenuManager.Start))]

        class TitleLogoPatch

        {

            public static GameObject amongUsLogo;



            [HarmonyPriority(Priority.VeryHigh)]

            static void Postfix(MainMenuManager __instance)

            {

                amongUsLogo = GameObject.Find("LOGO-AU");



                var rightpanel = __instance.gameModeButtons.transform.parent;

                var logoObject = new GameObject("titleLogo_TOHhm");

                var logoTransform = logoObject.transform;

                TOHhmLogo = logoObject.AddComponent<SpriteRenderer>();

                logoTransform.parent = rightpanel;

                logoTransform.localPosition = new(0f, 0.15f, 1f);

                logoTransform.localScale *= 1.0f;

                TOHhmLogo.sprite = UtilsSprite.LoadSprite(Event.April || Event.Special ? "TownOfHost.Resources.TownOfHost-Shell-logo2.png" : "TownOfHost.Resources.TownOfHost-Shell-logo.png", 175f);

            }

        }

        [HarmonyPatch(typeof(ModManager), nameof(ModManager.LateUpdate))]

        class ModManagerLateUpdatePatch

        {

            static int oldcount;

            static float olddeltimer;

            static float timer = 0;

            public static void Prefix(ModManager __instance)

            {

                __instance.ShowModStamp();



                LateTask.Update(Time.deltaTime);

                CheckMurderPatch.Update();



                if (Main.MegCount > 49)

                {

                    timer += Time.deltaTime;



                    if (timer > 1)

                    {

                        timer = 0;

                        olddeltimer = 0;

                        Main.MegCount = 0;

                    }

                }

                else if (Main.MegCount == oldcount)

                {

                    olddeltimer += Time.deltaTime;



                    if (olddeltimer > 1.3f)

                    {

                        timer = 0;

                        olddeltimer = 0;

                        Main.MegCount = 0;

                    }

                }



                oldcount = Main.MegCount;

            }

            public static void Postfix(ModManager __instance)

            {

                var offset_y = HudManager.InstanceExists ? 1.6f : 0.9f;

                __instance.ModStamp.transform.position = AspectPosition.ComputeWorldPosition(

                    __instance.localCamera, AspectPosition.EdgeAlignments.RightTop,

                    new Vector3(0.4f, offset_y, __instance.localCamera.nearClipPlane + 0.1f));

            }

        }

    }

}

