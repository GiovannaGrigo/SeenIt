namespace SeenIt.Api.Services.Avatars;

public static class AvatarServiceRegistration
{
    public static IServiceCollection AddAvatarStorage(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        services.AddOptions<AvatarStorageOptions>().Bind(configuration.GetSection(AvatarStorageOptions.Section))
            .PostConfigure(options => { if (environment.IsProduction()) options.Enabled = true; })
            .Validate(options => options.IsValid(), "SupabaseStorage inválido: configure HTTPS no projeto Supabase, SecretKey sb_secret_ e Bucket avatars; nenhum valor sensível é exibido.")
            .ValidateOnStart();
        services.AddHttpClient(SupabaseAvatarStorageService.ClientName, client => client.Timeout = TimeSpan.FromSeconds(20))
            .ConfigurePrimaryHttpMessageHandler(() => new SocketsHttpHandler { AllowAutoRedirect = false })
            .RedactLoggedHeaders(["apikey", "Authorization"])
            .RemoveAllLoggers();
        services.AddScoped<SupabaseAvatarStorageService>();
        services.AddScoped<IAvatarStorageService, AvatarStorageService>();
        services.AddScoped<IAvatarReferenceStore, AvatarReferenceStore>();
        services.AddScoped<AvatarProfileService>();
        services.AddSingleton<AvatarImageProcessor>();
        return services;
    }
}
