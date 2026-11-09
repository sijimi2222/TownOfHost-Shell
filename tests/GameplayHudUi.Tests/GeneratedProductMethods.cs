using UnityEngine;using TownOfHost.Modules;using TMPro;using Il2CppInterop.Runtime.InteropTypes.Arrays;namespace TownOfHost {public static partial class RoleGuideButtonPatch {public static void UpdateTopRightButtonLayout()

    {

        if (!DestroyableSingleton<HudManager>.InstanceExists) return;
        // Correct visibility before measuring chat's position; native options-close can reactivate it.
        GameplayHudVisibilityPatch.Apply(DestroyableSingleton<HudManager>.Instance);
        if (_btnRenderer == null) return;



        var hud = DestroyableSingleton<HudManager>.Instance;

        var settingsButton = hud.SettingsButton;

        var chatButton = hud.Chat?.chatButton;

        if (settingsButton == null || chatButton == null) return;



        if (ShouldShowTopRightChatButton() && !chatButton.gameObject.activeSelf)

            chatButton.gameObject.SetActive(true);



        var settingsPos = hud.transform.InverseTransformPoint(settingsButton.transform.position);

        var chatPos = hud.transform.InverseTransformPoint(chatButton.transform.position);

        float buttonSpacing = Mathf.Abs(settingsPos.x - chatPos.x);

        if (buttonSpacing < 0.45f || buttonSpacing > 1.5f) buttonSpacing = 0.78f;



        float guideX = chatButton.gameObject.activeInHierarchy

            ? chatPos.x - buttonSpacing - GuideFrameGap

            : settingsPos.x - buttonSpacing - GuideFrameGap;

        _btnRenderer.transform.localPosition = new Vector3(

            guideX,

            settingsPos.y,

            settingsPos.z - 0.1f);

    }
public static void UpdateButtonVisibility()

    {

        if (_btnRenderer == null) return;



        bool shouldShow = !GameSettingMenu.Instance
            && (!HudManager.InstanceExists || !GameplayHudVisibilityPatch.OptionsOpen(HudManager.Instance));

        var buttonObject = _btnRenderer.gameObject;

        if (buttonObject != null && buttonObject.activeSelf != shouldShow)

            buttonObject.SetActive(shouldShow);



        if (!shouldShow && isPanelOpen) ClosePanel();

    }
internal static bool TryGetHeaderRightOffset(out float offset)
    {
        offset = 0;
        if (!HudManager.InstanceExists) return false;
        var hud = HudManager.Instance;
        var camera = hud.UICamera;
        if (camera == null || !camera.orthographic || !TryGetHeaderLeftEdge(hud.transform, out var left)) return false;
        float width = camera.orthographicSize * 2 * camera.aspect;
        // Convert HUD-local geometry to a normalized viewport coordinate, without world-camera translation.
        return HudHeaderLayout.TryRightOffset(width, .5f + left / width, out offset);
    }
internal static bool TryGetHeaderLeftEdge(Transform space, out float left)
    {
        left = float.PositiveInfinity;
        if (space == null || !HasGuideButton || guideFrameRenderer == null || !guideFrameRenderer.enabled
            || !guideFrameRenderer.gameObject.activeInHierarchy) return false;
        // Renderer.bounds is world-space and can be refreshed at a different point in the frame.
        // Use stable local sprite bounds and current transforms within the same HUD instead.
        var bounds = guideFrameRenderer.localBounds;
        var transform = guideFrameRenderer.transform;
        for (int i = 0; i < 4; i++)
        {
            var point = new Vector3((i & 2) == 0 ? bounds.min.x : bounds.max.x,
                (i & 1) == 0 ? bounds.min.y : bounds.max.y, bounds.center.z);
            left = Mathf.Min(left, space.InverseTransformPoint(transform.TransformPoint(point)).x);
        }
        return !float.IsNaN(left) && !float.IsInfinity(left);
    }}public static partial class CredentialsPatch {        private static Il2CppStructArray<Vector3> headerWorldCorners;
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
private static void HeaderLateLayout()
        {
            // Resolve changed text/geometry after native updates; an unchanged header needs no mesh or position write.
            KeepHeaderClearOfGuide(true);
        }
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
        }public static void UpdateCredentialLayout()=>KeepHeaderClearOfGuide();}}