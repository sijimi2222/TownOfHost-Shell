namespace Il2CppInterop.Runtime.InteropTypes.Arrays{public class Il2CppReferenceArray<T>{readonly T[] data;public Il2CppReferenceArray(int size){data=new T[size];}public int Length=>data.Length;public T this[int i]{get=>data[i];set=>data[i]=value;}}}
namespace UnityEngine{
 public struct Vector2{public float x,y;public Vector2(float x,float y){this.x=x;this.y=y;}public float sqrMagnitude=>x*x+y*y;public static Vector2 operator+(Vector2 a,Vector2 b)=>new(a.x+b.x,a.y+b.y);public static Vector2 operator-(Vector2 a,Vector2 b)=>new(a.x-b.x,a.y-b.y);}
 public struct Vector3{public float x,y,z;public Vector3(float x,float y,float z){this.x=x;this.y=y;this.z=z;}public static implicit operator Vector2(Vector3 p)=>new(p.x,p.y);public static implicit operator Vector3(Vector2 p)=>new(p.x,p.y,0);}
 public class Transform{public Vector3 position;}
 public struct Bounds{public Vector3 extents,center;}
 public class Collider2D{public bool enabled=true,isTrigger;public Bounds bounds=new(){extents=new(.2f,.2f,0)};}
 public class Rigidbody2D{public Vector2 velocity=new(1,0);}
 public static class Mathf{public static float Max(float a,float b)=>Math.Max(a,b);public static float Abs(float a)=>Math.Abs(a);}
 public static class Physics2D{public static Func<Vector2,Collider2D[]> Results=_=>Array.Empty<Collider2D>();public static int OverlapCircleNonAlloc(Vector2 p,float radius,Il2CppInterop.Runtime.InteropTypes.Arrays.Il2CppReferenceArray<Collider2D> hits,int mask){var values=Results(p);int count=Math.Min(hits.Length,values.Length);for(int i=0;i<count;i++)hits[i]=values[i];return count;}}
}
public enum NetworkModes{OnlineGame,LocalGame,FreePlay}
public class PlayerControl{public bool AmOwner=true,Alive=true,CanMove=true,inVent,onLadder,inMovingPlat;public UnityEngine.Transform transform=new();public UnityEngine.Vector2 Position{get=>transform.position;set=>transform.position=value;}public UnityEngine.Collider2D Collider=new();public PlayerPhysics MyPhysics=new();public NetworkTransform NetTransform;public PlayerControl(){NetTransform=new(this);}public bool IsAlive()=>Alive;public UnityEngine.Vector2 GetTruePosition()=>new(Position.x,Position.y+.33f);}
public class PlayerPhysics{public UnityEngine.Rigidbody2D body=new();}
public class NetworkTransform{readonly PlayerControl owner;public int RpcCalls,LocalCalls;public bool ThrowRpc;public NetworkTransform(PlayerControl p){owner=p;}public void RpcSnapTo(UnityEngine.Vector2 p){RpcCalls++;if(ThrowRpc)throw new IOException("synthetic failure");owner.Position=p;}public void SnapTo(UnityEngine.Vector2 p){LocalCalls++;owner.Position=p;}}
public class AmongUsClient{public enum GameStates{Joined,Started,Ended}public static AmongUsClient Instance=new();public bool AmHost=true,IsGamePublic;public int GameId=1;public GameStates GameState=GameStates.Started;public NetworkModes NetworkMode=NetworkModes.OnlineGame;}
public static class Constants{public const int ShipAndObjectsMask=123;}
public class MeetingHud{public static MeetingHud Instance;}
namespace TownOfHost{public static class GameStates{public static bool IsInTask=true,IsMeeting,CalledMeeting;}}
