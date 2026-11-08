using System.Collections;
namespace UnityEngine.ResourceManagement.AsyncOperations {
 public enum AsyncOperationStatus{None,Succeeded,Failed}
 public sealed class Lease {public bool Valid=true,Complete=true;public int ResultReads,Releases;}
 public struct AsyncOperationHandle {
  public Lease Lease;
  public bool IsValid()=>Lease!=null&&Lease.Valid;
  public AsyncOperationStatus Status=>Lease.Complete?AsyncOperationStatus.Succeeded:AsyncOperationStatus.None;
  public UnityEngine.Object Result{get{Lease.ResultReads++;if(!Lease.Valid||!Lease.Complete)throw new InvalidOperationException("synthetic unavailable asset");return new UnityEngine.GameObject();}}
  public void Release(){Lease.Releases++;Lease.Valid=false;}
 }
}
namespace UnityEngine.AddressableAssets {
 public class AssetReference {public UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle OperationHandle;public UnityEngine.ResourceManagement.AsyncOperations.AsyncOperationHandle LoadAssetAsync<T>()=>OperationHandle;}
}
public class ShipStatus:UnityEngine.Object{}
public enum MapNames:byte {Skeld,Polus}
public static class EnumHelper {public static T[] GetAllValues<T>() where T:struct,Enum=>Enum.GetValues<T>();}
public static class PreloadExt {
 public static bool TryCast<T>(this UnityEngine.Object o,out T result)where T:UnityEngine.Object{result=o as T;return result!=null;}
 public static T Cast<T>(this UnityEngine.Object o)where T:UnityEngine.Object=>o as T;
 public static IEnumerator WrapToIl2Cpp(this IEnumerator e)=>e;
}
