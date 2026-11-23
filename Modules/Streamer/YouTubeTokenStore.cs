using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Text.Json;

namespace TownOfHost;

internal interface IYouTubeTokenStore
{
    YouTubeOAuthTokens Load(string clientId);
    void Save(string clientId, YouTubeOAuthTokens tokens);
    void Delete();
}
internal sealed class YouTubeCredentialStorageException : Exception
{ internal YouTubeCredentialStorageException() : base("YouTube credential storage unavailable") { } }
internal interface IYouTubeDataProtection { byte[] Protect(byte[] input); byte[] Unprotect(byte[] input); }
internal sealed class YouTubeWindowsDpapi : IYouTubeDataProtection
{
    [StructLayout(LayoutKind.Sequential)] private struct Blob { internal int Length; internal IntPtr Data; }
    [DllImport("crypt32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CryptProtectData(ref Blob input, string description, ref Blob entropy, IntPtr reserved, IntPtr prompt, uint flags, out Blob output);
    [DllImport("crypt32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)] private static extern bool CryptUnprotectData(ref Blob input, IntPtr description, ref Blob entropy, IntPtr reserved, IntPtr prompt, uint flags, out Blob output);
    [DllImport("kernel32.dll")] private static extern IntPtr LocalFree(IntPtr memory);
    private static byte[] Transform(byte[] bytes, bool protect)
    {
        if (!OperatingSystem.IsWindows() || bytes == null || bytes.Length == 0 || bytes.Length > 65536) throw new YouTubeCredentialStorageException();
        byte[] entropyBytes = Encoding.UTF8.GetBytes("TOH-Shell.YouTube.DesktopOAuth.v1");
        var sourcePin = GCHandle.Alloc(bytes, GCHandleType.Pinned); var entropyPin = GCHandle.Alloc(entropyBytes, GCHandleType.Pinned);
        var source = new Blob { Length = bytes.Length, Data = sourcePin.AddrOfPinnedObject() };
        var entropy = new Blob { Length = entropyBytes.Length, Data = entropyPin.AddrOfPinnedObject() }; Blob output = default;
        try
        {
            // UI_FORBIDDEN only. Never CRYPTPROTECT_LOCAL_MACHINE: bind to this Windows user.
            bool ok = protect ? CryptProtectData(ref source, "TOH-Shell YouTube OAuth", ref entropy, IntPtr.Zero, IntPtr.Zero, 1, out output)
                : CryptUnprotectData(ref source, IntPtr.Zero, ref entropy, IntPtr.Zero, IntPtr.Zero, 1, out output);
            if (!ok || output.Data == IntPtr.Zero || output.Length <= 0 || output.Length > 65536) throw new YouTubeCredentialStorageException();
            byte[] result = new byte[output.Length]; Marshal.Copy(output.Data, result, 0, output.Length); return result;
        }
        finally
        {
            if (output.Data != IntPtr.Zero)
            {
                // Clear plaintext native output before returning its allocation to Windows.
                if (!protect && output.Length > 0 && output.Length <= 65536) for (int i = 0; i < output.Length; i++) Marshal.WriteByte(output.Data, i, 0);
                LocalFree(output.Data);
            }
            sourcePin.Free(); entropyPin.Free(); CryptographicOperations.ZeroMemory(entropyBytes);
        }
    }
    public byte[] Protect(byte[] input) => Transform(input, true);
    public byte[] Unprotect(byte[] input) => Transform(input, false);
}
// Disk contains only DPAPI ciphertext and a non-secret fail-closed marker.
// No credentials are stored under the game/plugin directory or in BepInEx's cfg.
internal sealed class YouTubeProtectedTokenStore : IYouTubeTokenStore
{
    private readonly string folder, path, disabled;
    private readonly IYouTubeDataProtection protection;
    internal YouTubeProtectedTokenStore(string directory, IYouTubeDataProtection dataProtection = null)
    {
        if (!Path.IsPathFullyQualified(directory)) throw new YouTubeCredentialStorageException();
        folder = Path.GetFullPath(directory); path = Path.Combine(folder, "oauth.dpapi"); disabled = Path.Combine(folder, "oauth.disabled"); protection = dataProtection ?? new YouTubeWindowsDpapi();
    }
    private void Prepare()
    {
        if (!OperatingSystem.IsWindows()) throw new YouTubeCredentialStorageException();
        Directory.CreateDirectory(folder);
        if ((File.GetAttributes(folder) & FileAttributes.ReparsePoint) != 0) throw new YouTubeCredentialStorageException();
        using var identity = WindowsIdentity.GetCurrent(); var sid = identity.User ?? throw new YouTubeCredentialStorageException();
        var security = new DirectorySecurity(); security.SetAccessRuleProtection(true, false);
        security.AddAccessRule(new FileSystemAccessRule(sid, FileSystemRights.FullControl, InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit, PropagationFlags.None, AccessControlType.Allow));
        new DirectoryInfo(folder).SetAccessControl(security);
    }
    private static void CheckFile(string file)
    {
        if (File.Exists(file) && (File.GetAttributes(file) & FileAttributes.ReparsePoint) != 0) throw new YouTubeCredentialStorageException();
    }
    public YouTubeOAuthTokens Load(string clientId)
    {
        byte[] plain = null;
        try
        {
            if (!OperatingSystem.IsWindows()) throw new YouTubeCredentialStorageException();
            if (!Directory.Exists(folder)) return null;
            if ((File.GetAttributes(folder) & FileAttributes.ReparsePoint) != 0) throw new YouTubeCredentialStorageException();
            if (File.Exists(disabled)) throw new YouTubeCredentialStorageException();
            CheckFile(path); if (!File.Exists(path)) return null;
            if (new FileInfo(path).Length > 65536) throw new YouTubeCredentialStorageException();
            plain = protection.Unprotect(File.ReadAllBytes(path));
            using var doc = JsonDocument.Parse(plain, new JsonDocumentOptions { MaxDepth = 8 }); var data = doc.RootElement;
            if (OfficialYouTubeJson.Text(data, "client_id", 1024) != clientId || OfficialYouTubeJson.Text(data, "scope", 2048) != YouTubePkceSession.Scope) throw new YouTubeCredentialStorageException();
            string access = OfficialYouTubeJson.Text(data, "access_token", 8192), refresh = OfficialYouTubeJson.Text(data, "refresh_token", 8192);
            if (!data.TryGetProperty("expires", out var expiry) || !expiry.TryGetInt64(out long ticks)) throw new YouTubeCredentialStorageException();
            var expires = new DateTimeOffset(ticks, TimeSpan.Zero);
            if (string.IsNullOrWhiteSpace(access) || string.IsNullOrWhiteSpace(refresh) || refresh.Length > 8192 || access.Length > 8192 || HasControl(refresh) || HasControl(access)) throw new YouTubeCredentialStorageException();
            return new(new YouTubeCredential(access, false, expires), refresh);
        }
        catch (Exception e) when (e is not OutOfMemoryException) { throw new YouTubeCredentialStorageException(); }
        finally { if (plain != null) CryptographicOperations.ZeroMemory(plain); }
    }
    internal static bool HasControl(string value) { foreach (char c in value) if (char.IsControl(c)) return true; return false; }
    public void Save(string clientId, YouTubeOAuthTokens tokens)
    {
        byte[] plain = null, encrypted = null; string temp = null;
        try
        {
            if (tokens?.Access?.Valid != true || tokens.Access.IsApiKey || string.IsNullOrWhiteSpace(tokens.RefreshToken) || tokens.RefreshToken.Length > 8192 || HasControl(tokens.RefreshToken)) throw new YouTubeCredentialStorageException();
            Prepare(); CheckFile(path); CheckFile(disabled);
            // A failed update cannot silently restore an older credential after restart.
            File.WriteAllText(disabled, "disabled-v1", Encoding.ASCII);
            plain = JsonSerializer.SerializeToUtf8Bytes(new { client_id = clientId, scope = YouTubePkceSession.Scope, access_token = tokens.Access.Secret, refresh_token = tokens.RefreshToken, expires = tokens.Access.ExpiresAt.UtcTicks });
            encrypted = protection.Protect(plain); if (encrypted.Length > 65536) throw new YouTubeCredentialStorageException();
            temp = Path.Combine(folder, ".oauth-" + Guid.NewGuid().ToString("N") + ".tmp");
            using (var stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None)) { stream.Write(encrypted); stream.Flush(true); }
            File.Move(temp, path, true); temp = null; File.Delete(disabled);
        }
        catch (Exception e) when (e is not OutOfMemoryException) { throw new YouTubeCredentialStorageException(); }
        finally
        {
            if (plain != null) CryptographicOperations.ZeroMemory(plain);
            if (encrypted != null) CryptographicOperations.ZeroMemory(encrypted);
            if (temp != null) { try { File.Delete(temp); } catch { } }
        }
    }
    public void Delete()
    {
        try
        {
            if (!OperatingSystem.IsWindows()) throw new YouTubeCredentialStorageException();
            if (!Directory.Exists(folder)) return;
            Prepare(); CheckFile(path); CheckFile(disabled);
            File.WriteAllText(disabled, "disabled-v1", Encoding.ASCII);
            File.Delete(path); // Leave the non-secret marker if deletion fails.
            File.Delete(disabled);
        }
        catch (Exception e) when (e is not OutOfMemoryException) { throw new YouTubeCredentialStorageException(); }
    }
}

// Separate from user OAuth tokens: optional Desktop client configuration, bound to
// both the current Windows user (DPAPI) and the exact configured client ID.
internal sealed class YouTubeProtectedClientSecret
{
    internal const string FileName = "client-secret.dpapi";
    private readonly string folder, path;
    private readonly IYouTubeDataProtection protection;
    internal YouTubeProtectedClientSecret(string directory, IYouTubeDataProtection dataProtection = null)
    {
        if (string.IsNullOrWhiteSpace(directory) || !Path.IsPathFullyQualified(directory)) throw new YouTubeCredentialStorageException();
        folder = Path.GetFullPath(directory); path = Path.Combine(folder, FileName); protection = dataProtection ?? new YouTubeWindowsDpapi();
    }
    internal string Load(string clientId)
    {
        byte[] encrypted = null, plain = null;
        try
        {
            if (!OperatingSystem.IsWindows()) throw new YouTubeCredentialStorageException();
            if (!Directory.Exists(folder)) return null;
            if ((File.GetAttributes(folder) & FileAttributes.ReparsePoint) != 0) throw new YouTubeCredentialStorageException();
            if (!File.Exists(path)) return null;
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0 || new FileInfo(path).Length is <= 0 or > 65536) throw new YouTubeCredentialStorageException();
            encrypted = File.ReadAllBytes(path); plain = protection.Unprotect(encrypted);
            if (plain.Length > 8192) throw new YouTubeCredentialStorageException();
            using var document = JsonDocument.Parse(plain, new JsonDocumentOptions { MaxDepth = 4 }); var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object || System.Linq.Enumerable.Count(root.EnumerateObject()) != 3
                || OfficialYouTubeJson.Text(root, "kind", 64) != "desktop-client-secret-v1"
                || OfficialYouTubeJson.Text(root, "client_id", 1024) != clientId) throw new YouTubeCredentialStorageException();
            // Count and exact names also reject duplicates and unrelated token payloads.
            var keys = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            foreach (var property in root.EnumerateObject())
                if (!keys.Add(property.Name) || property.Name is not ("kind" or "client_id" or "client_secret")) throw new YouTubeCredentialStorageException();
            string secret = OfficialYouTubeJson.Text(root, "client_secret", 4096);
            if (!YouTubeOAuthSettings.ValidClientSecret(secret)) throw new YouTubeCredentialStorageException();
            return secret;
        }
        catch (Exception error) when (error is not OutOfMemoryException) { throw new YouTubeCredentialStorageException(); }
        finally
        {
            if (plain != null) CryptographicOperations.ZeroMemory(plain);
            if (encrypted != null) CryptographicOperations.ZeroMemory(encrypted);
        }
    }
    public override string ToString() => "YouTubeProtectedClientSecret[redacted]";
}
