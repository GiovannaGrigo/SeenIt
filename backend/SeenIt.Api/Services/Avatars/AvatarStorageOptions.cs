using System.Text.RegularExpressions;

namespace SeenIt.Api.Services.Avatars;

public sealed class AvatarStorageOptions
{
    public const string Section = "SupabaseStorage";
    // Development is explicitly opt-in; production always uses persistent Storage.
    public bool Enabled { get; set; }
    public string Url { get; set; } = "";
    public string SecretKey { get; set; } = "";
    public string Bucket { get; set; } = "avatars";

    public static bool IsValidUrl(string value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
        uri.Scheme == "https" && uri.Port == 443 && uri.UserInfo == "" &&
        uri.AbsolutePath == "/" && uri.Query == "" && uri.Fragment == "" &&
        Regex.IsMatch(uri.IdnHost, @"\A[a-z0-9]{10,32}\.supabase\.co\z", RegexOptions.CultureInvariant);

    public bool IsValid() =>
        (string.IsNullOrEmpty(Url) ? !Enabled : IsValidUrl(Url)) && Bucket == "avatars" &&
        (string.IsNullOrEmpty(SecretKey) ? !Enabled : Regex.IsMatch(SecretKey, @"\Asb_secret_[A-Za-z0-9_-]{10,}\z"));
}
