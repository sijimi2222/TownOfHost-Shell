using System;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace TownOfHost.Modules;

// hamo's TextDummy adapted to Shell's existing CNO appearance transaction.
// Never SetName using the dummy's borrowed GameData.NetId. All temporary host outfit
// fields are restored in finally, including when sending throws.
internal sealed class TextDummy : CustomNetObject
{
    static readonly Vector2 ParkPosition = new(50f, 50f);
    static readonly RoleVisualBudget moves = new(16f, 4f), names = new(8f, 4f);
    readonly RoleAbilityContext context;
    readonly int game;
    bool removed, ready, failed, parked = true;
    string art = "", shown;
    Vector2 wanted = ParkPosition, point;
    float nextMove;
    public bool IsReady => ready && !removed && !failed && PlayerControl != null;
    protected override bool CanCreate => !removed && !failed && context.CanAct;
    protected override bool DelayPlayerIdReset => true;
    public TextDummy(RoleAbilityContext current)
    { context = current; game = AmongUsClient.Instance.GameId; CreateNetObject(ParkPosition); }
    protected override void OnCreated()
    {
        // ID reset is at 1.1s after spawn; do not show before its binding completes.
        _ = new LateTask(() =>
        {
            if (!CanCreate || PlayerControl == null) { Remove(); return; }
            ready = true; Apply(); CommitPosition(); HideBody();
        }, 0.8f, "TextDummy.Ready", true);
    }
    public void Show(string text)
    {
        if (removed) return;
        text ??= "";
        if (Encoding.UTF8.GetByteCount(text) > NetworkSafety.NameByteLimit)
        { failed = true; Logger.Warn("Visual exceeds Shell name budget; removing visual.", "TextDummy"); Remove(); return; }
        art = text;
        if (!parked) wanted = point - MeasureOffset();
        Apply();
    }
    public void MoveTo(Vector2 position)
    {
        if (removed || !float.IsFinite(position.x) || !float.IsFinite(position.y)) return;
        parked = false; point = position; wanted = point - MeasureOffset(); CommitPosition();
    }
    public void Park()
    { if (removed) return; parked = true; art = ""; wanted = ParkPosition; Apply(); CommitPosition(); }
    public void Remove()
    { if (removed) return; removed = true; if (PlayerControl != null) Despawn(); }
    public override void OnMeeting() => Remove();
    protected override void OnFixedUpdate()
    {
        if (!CanCreate) { Remove(); return; }
        Apply(); CommitPosition();
    }
    Vector2 MeasureOffset()
    {
        var label = PlayerControl?.cosmetics?.nameText;
        if (label == null) return new Vector2(0f, 0.6f);
        string before = label.text;
        try
        {
            label.text = art; label.ForceMeshUpdate(true, true);
            return (Vector2)label.transform.TransformPoint(label.textBounds.center) - (Vector2)PlayerControl.transform.position;
        }
        catch { return new Vector2(0f, 0.6f); }
        finally { label.text = before; }
    }
    void Apply()
    {
        if (!IsReady || !CanCreate || art == shown || !names.Take(game, Time.time)) return;
        var host = global::PlayerControl.LocalPlayer;
        if (host?.Data == null) { Remove(); return; }
        var outfit = host.Data.Outfits[PlayerOutfitType.Default];
        string oldName = outfit.PlayerName, oldHat = outfit.HatId, oldSkin = outfit.SkinId,
            oldPet = outfit.PetId, oldVisor = outfit.VisorId;
        int oldColor = outfit.ColorId;
        try
        {
            SetAppearance(0, "", "", "", "", displayName: art);
            shown = art;
            if (PlayerControl.cosmetics?.nameText != null) PlayerControl.cosmetics.nameText.text = art;
            if (!parked) wanted = point - MeasureOffset();
            HideBody();
        }
        catch (Exception e) { failed = true; Logger.Warn("Visual send failed: " + e.GetType().Name, "TextDummy"); Remove(); }
        finally
        {
            outfit.PlayerName = oldName; outfit.ColorId = oldColor; outfit.HatId = oldHat;
            outfit.SkinId = oldSkin; outfit.PetId = oldPet; outfit.VisorId = oldVisor;
        }
    }
    void CommitPosition()
    {
        if (!IsReady || !CanCreate || Time.time < nextMove || Vector2.Distance(Position, wanted) < 0.02f) return;
        if (!moves.Take(game, Time.time)) return;
        nextMove = Time.time + 0.2f;
        try { SnapToPosition(wanted); }
        catch (Exception e) { failed = true; Logger.Warn("Visual move failed: " + e.GetType().Name, "TextDummy"); Remove(); }
    }
    void HideBody()
    {
        if (PlayerControl == null) return;
        foreach (var renderer in PlayerControl.GetComponentsInChildren<SpriteRenderer>(true)) renderer.enabled = false;
        PlayerControl.cosmetics.colorBlindText.color = Color.clear;
    }
}
