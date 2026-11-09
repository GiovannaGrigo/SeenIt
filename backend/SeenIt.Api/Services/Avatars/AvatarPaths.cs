using System.Text.RegularExpressions;

namespace SeenIt.Api.Services.Avatars;

public static class AvatarPaths
{
    public static bool IsLegacy(string path) => Regex.IsMatch(path, @"\A[0-9a-f]{32}\.(jpg|png|webp)\z");
    public static bool IsLocal(string path) => path.StartsWith("local/", StringComparison.Ordinal);
    public static bool IsOwned(Guid userId, string path)
    {
        if (userId == Guid.Empty || path.Length > 200) return false;
        var relative = IsLocal(path) ? path[6..] : path;
        return Regex.IsMatch(relative, @"\A" + userId.ToString("D") + @"/[0-9a-f]{32}\.webp\z");
    }
    public static string New(Guid userId, bool local = false) =>
        (local ? "local/" : "") + $"{userId:D}/{Guid.NewGuid():N}.webp";
}
