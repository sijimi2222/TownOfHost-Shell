using UnityEngine;using TownOfHost.Modules;using TMPro;namespace TownOfHost {public static partial class RoleGuideButtonPatch {public static void UpdateTopRightButtonLayout()

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
        if (!HasGuideButton || guideFrameRenderer == null || !guideFrameRenderer.enabled
            || !HudManager.InstanceExists) return false;
        var camera = HudManager.Instance.UICamera;
        if (camera == null || !camera.orthographic) return false;
        // The guide's root SpriteRenderer has no sprite. Measure its visible native-style frame instead.
        var bounds = guideFrameRenderer.bounds;
        var left = camera.WorldToViewportPoint(new Vector3(bounds.min.x, bounds.center.y, bounds.center.z));
        if (left.z <= 0) return false;
        return HudHeaderLayout.TryRightOffset(camera.orthographicSize * 2 * camera.aspect, left.x, out offset);
    }
internal static bool TryGetHeaderLeftEdge(out float left)
    {
        left = 0;
        if (!HasGuideButton || guideFrameRenderer == null || !guideFrameRenderer.enabled
            || !guideFrameRenderer.gameObject.activeInHierarchy) return false;
        left = guideFrameRenderer.bounds.min.x;
        return !float.IsNaN(left) && !float.IsInfinity(left);
    }}public static partial class CredentialsPatch {private static readonly Vector3[] headerWorldCorners=new Vector3[4];private static bool headerPositioning,headerMissingGuideLogged,headerFailureLogged;private static int headerDiagnosticCount;private static void KeepHeaderClearOfGuide(bool refreshMesh = false)
        {
            if (headerPositioning || pingTrackerCredential == null || !pingTrackerCredential.gameObject.activeInHierarchy) return;
            if (!RoleGuideButtonPatch.TryGetHeaderLeftEdge(out var guideLeft))
            {
                if (refreshMesh && !headerMissingGuideLogged)
                {
                    headerMissingGuideLogged = true;
                    Logger.Info("HEADER_LAYOUT_V3_NO_GUIDE", "HeaderLayout");
                }
                return;
            }
            headerPositioning = true;
            try
            {
                // TMP ink can extend outside the RectTransform. Rebuild AFTER text and native anchors update.
                if (refreshMesh) pingTrackerCredential.ForceMeshUpdate();
                pingTrackerCredential.rectTransform.GetWorldCorners(headerWorldCorners);
                float rectangleRight = headerWorldCorners.Max(corner => corner.x);
                var bounds = pingTrackerCredential.textBounds;
                var transform = pingTrackerCredential.transform;
                float inkRight = Mathf.Max(
                    Mathf.Max(transform.TransformPoint(new Vector3(bounds.min.x, bounds.min.y, bounds.center.z)).x,
                        transform.TransformPoint(new Vector3(bounds.min.x, bounds.max.y, bounds.center.z)).x),
                    Mathf.Max(transform.TransformPoint(new Vector3(bounds.max.x, bounds.min.y, bounds.center.z)).x,
                        transform.TransformPoint(new Vector3(bounds.max.x, bounds.max.y, bounds.center.z)).x));
                float shift = HudHeaderLayout.RequiredLeftShift(Mathf.Max(rectangleRight, inkRight), guideLeft);
                if (shift > .001f)
                {
                    var position = transform.position;
                    transform.position = new Vector3(position.x - shift, position.y, position.z);
                }
                if (refreshMesh && headerDiagnosticCount < 3)
                {
                    headerDiagnosticCount++;
                    Logger.Info(System.FormattableString.Invariant($"HEADER_LAYOUT_V3 rect={rectangleRight:F3};ink={inkRight:F3};guide={guideLeft:F3};shift={shift:F3}"), "HeaderLayout");
                }
            }
            catch (System.Exception)
            {
                if (refreshMesh && !headerFailureLogged)
                {
                    headerFailureLogged = true;
                    Logger.Warn("HEADER_LAYOUT_V3_DEFERRED", "HeaderLayout");
                }
            }
            finally { headerPositioning = false; }
        }
private static void HeaderLateLayout()
        {
            // All Update handlers (including PingTracker text assignment and AspectPosition) have finished.
            KeepHeaderClearOfGuide(true);
        }
private static class HeaderAnchorAdjustedPatch
        {
            static void Postfix(AspectPosition __instance)
            {
                // Do not change any other label or UI anchor.
                if (__instance != null && __instance == pingTrackerCredentialAspectPos) KeepHeaderClearOfGuide();
            }
        }
private static class HeaderAnchorAspectAdjustedPatch
        {
            static void Postfix(AspectPosition __instance)
            {
                if (__instance != null && __instance == pingTrackerCredentialAspectPos) KeepHeaderClearOfGuide();
            }
        }public static void UpdateCredentialLayout(){

                    bool isChatButtonVisible = DestroyableSingleton<HudManager>.InstanceExists

                        && DestroyableSingleton<HudManager>.Instance.Chat.chatButton.gameObject.activeInHierarchy;

                    float rightOffset = isChatButtonVisible

                        ? 2.5f

                        : 1.8f;

                    // 役職ガイドボタンの左側へ上部表示を収める。

                    if (RoleGuideButtonPatch.HasGuideButton) rightOffset += 0.72f;

                    if (RoleGuideButtonPatch.TryGetHeaderRightOffset(out var measuredOffset))
                        rightOffset = measuredOffset;
                    if (Mathf.Abs(pingTrackerCredentialAspectPos.DistanceFromEdge.x - rightOffset) > .001f)
                    {
                        pingTrackerCredentialAspectPos.DistanceFromEdge = new(rightOffset, 0, 0);
                        pingTrackerCredentialAspectPos.AdjustPosition();
                    }
                    KeepHeaderClearOfGuide();

                }}}