using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

internal static class Certificates
{
    internal static void Generate(string directory)
    {
        Directory.CreateDirectory(directory);
        if(Directory.EnumerateFiles(directory).Any()) throw new InvalidOperationException("synthetic_certificate_directory_must_be_empty");
        using var caKey=RSA.Create(3072);
        var caRequest=new CertificateRequest("CN=SeenIt Synthetic Test CA",caKey,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);
        caRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true,false,0,true));
        caRequest.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.KeyCertSign|X509KeyUsageFlags.CrlSign,true));
        using var ca=caRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-30),DateTimeOffset.UtcNow.AddDays(30));
        File.WriteAllText(Path.Combine(directory,"root.crt"),ca.ExportCertificatePem());
        foreach(var variant in new[]{"server","expired"})
        {
            using var serverKey=RSA.Create(3072);
            var request=new CertificateRequest("CN=localhost",serverKey,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);
            var san=new SubjectAlternativeNameBuilder();san.AddDnsName("localhost");
            request.CertificateExtensions.Add(san.Build());
            request.CertificateExtensions.Add(new X509BasicConstraintsExtension(false,false,0,true));
            request.CertificateExtensions.Add(new X509KeyUsageExtension(X509KeyUsageFlags.DigitalSignature|X509KeyUsageFlags.KeyEncipherment,true));
            request.CertificateExtensions.Add(new X509EnhancedKeyUsageExtension(new OidCollection {new("1.3.6.1.5.5.7.3.1")},true));
            using var cert=request.Create(ca,DateTimeOffset.UtcNow.AddDays(-20),
                variant=="expired" ? DateTimeOffset.UtcNow.AddDays(-1) : DateTimeOffset.UtcNow.AddDays(10),RandomNumberGenerator.GetBytes(16));
            File.WriteAllText(Path.Combine(directory,variant+".crt"),cert.ExportCertificatePem());
            File.WriteAllText(Path.Combine(directory,variant+".key"),serverKey.ExportPkcs8PrivateKeyPem());
        }
        using var otherKey=RSA.Create(2048);
        var otherRequest=new CertificateRequest("CN=Untrusted Synthetic CA",otherKey,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1);
        otherRequest.CertificateExtensions.Add(new X509BasicConstraintsExtension(true,false,0,true));
        using var other=otherRequest.CreateSelfSigned(DateTimeOffset.UtcNow.AddDays(-1),DateTimeOffset.UtcNow.AddDays(30));
        File.WriteAllText(Path.Combine(directory,"untrusted.crt"),other.ExportCertificatePem());
        File.WriteAllText(Path.Combine(directory,"malformed.crt"),"not a certificate");
        // Synthetic password is provisioned by the checks process; never part of remote source or configuration.
        File.WriteAllText(Path.Combine(directory,"reader.secret"),Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));
    }
}
