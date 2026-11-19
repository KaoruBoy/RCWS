#if UNITY_EDITOR
using System.Linq;

public static class RCWSUtils
{
    public static bool IsVRCFuryInstalled => UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages().Any(p=>p.packageId.StartsWith("com.vrcfury.vrcfury@"));
    public static bool IsNDMFInstalled => UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages().Any(p=>p.packageId.StartsWith("nadena.dev.ndmf@"));
    public static bool IsModularAvatarInstalled => UnityEditor.PackageManager.PackageInfo.GetAllRegisteredPackages().Any(p=>p.packageId.StartsWith("nadena.dev.modular-avatar@"));
}
#endif