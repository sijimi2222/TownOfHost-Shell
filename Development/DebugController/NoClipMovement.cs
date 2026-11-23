using System;
using UnityEngine;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
namespace TownOfHost.Development;

// Changes only the local host's existing collider. No layer/global physics edits,
// no player-ID spoofing and no protection/anti-cheat patches.
internal static class NoClipMovement
{
    private static PlayerControl tracked;
    private static Collider2D collider;
    private static bool originalEnabled;
    private static Vector2 safe;
    private static Vector2 centerOffset;
    private static float radius;
    private static int game;
    internal static bool Active=>tracked!=null&&collider!=null&&!collider.enabled;
    private static readonly Il2CppReferenceArray<Collider2D> hits=new(32);
    private static bool Valid(Vector2 p)=>float.IsFinite(p.x)&&float.IsFinite(p.y)&&Mathf.Abs(p.x)<=50&&Mathf.Abs(p.y)<=50;
    private static bool Clear(Vector2 position,PlayerControl owner)
    {
        if(!Valid(position))return false;
        int count=Physics2D.OverlapCircleNonAlloc(position+centerOffset,radius,hits,Constants.ShipAndObjectsMask);
        try
        {
            if(count>=hits.Length)return false;
            for(int i=0;i<count;i++)if(hits[i]!=null&&hits[i]!=owner.Collider&&!hits[i].isTrigger)return false;
            return true;
        }
        finally{for(int i=0;i<count&&i<hits.Length;i++)hits[i]=null;}
    }
    internal static void Update(PlayerControl player,AmongUsClient client,bool allowed)
    {
        if(tracked!=null&&(tracked!=player||client==null||game!=client.GameId))Restore();
        if(!allowed||player==null||client==null||!client.AmHost||client.IsGamePublic||!player.AmOwner||!player.IsAlive()
            ||client.NetworkMode!=NetworkModes.OnlineGame||client.GameState!=AmongUsClient.GameStates.Started
            ||!GameStates.IsInTask||GameStates.IsMeeting||GameStates.CalledMeeting||MeetingHud.Instance!=null
            ||player.inVent||player.onLadder||player.inMovingPlat||!player.CanMove||player.MyPhysics==null||player.MyPhysics.body==null){Restore();return;}
        bool moving=player.MyPhysics.body.velocity.sqrMagnitude>.0001f;
        if(!moving){Restore();return;}
        if(tracked==null)
        {
            var candidate=player.Collider;
            if(candidate==null||!candidate.enabled)return; // Do not acquire another feature's disabled collider.
            radius=Mathf.Max(.05f,Mathf.Max(candidate.bounds.extents.x,candidate.bounds.extents.y));
            var position=(Vector2)player.transform.position;
            centerOffset=(Vector2)candidate.bounds.center-position;
            if(!Clear(position,player))return;
            tracked=player;collider=candidate;safe=position;game=client.GameId;originalEnabled=candidate.enabled;
        }
        var current=(Vector2)player.transform.position;if(!Valid(current)){Restore();return;}if(Clear(current,player))safe=current;
        collider.enabled=false;
    }
    internal static void Restore()
    {
        var player=tracked;var body=collider;var position=safe;bool enabled=originalEnabled;int session=game;
        tracked=null;collider=null;
        if(body==null)return;
        try
        {
            var client=AmongUsClient.Instance;
            // If stopped inside a wall, return to the last observed free point
            // before restoring collision. Standard owner SnapTo keeps peers in sync.
            if(player!=null&&client!=null&&client.GameId==session&&client.AmHost&&player.AmOwner
                &&client.GameState==AmongUsClient.GameStates.Started&&!Clear((Vector2)player.transform.position,player))
                player.NetTransform.RpcSnapTo(position);
        }
        catch
        {
            // A failed standard correction must still restore local collision.
            // No alternate packets, protection bypass or unbounded retry.
            try{if(player!=null&&AmongUsClient.Instance?.GameId==session)player.NetTransform.SnapTo(position);}catch{}
        }
        finally{try{if(body!=null)body.enabled=enabled;}catch{}}
    }
}
