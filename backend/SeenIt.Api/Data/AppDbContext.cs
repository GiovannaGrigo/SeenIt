using Microsoft.EntityFrameworkCore;
using SeenIt.Api.Domain;

namespace SeenIt.Api.Data;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<UserSeries> UserSeries => Set<UserSeries>();
    public DbSet<EpisodioAssistido> EpisodiosAssistidos => Set<EpisodioAssistido>();
    public DbSet<EpisodioAssistidoSentimento> EpisodiosAssistidosSentimentos => Set<EpisodioAssistidoSentimento>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(120).IsRequired();
            entity.Property(x => x.Email).HasMaxLength(200).IsRequired();
            entity.HasIndex(x => x.Email).IsUnique();
        });

        modelBuilder.Entity<UserSeries>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(240).IsRequired();
            entity.Property(x => x.PosterUrl).HasMaxLength(600);
            entity.Property(x => x.Summary).HasMaxLength(3000);
            entity.HasIndex(x => new { x.UserId, x.ExternalSeriesId }).IsUnique();
            entity.HasOne(x => x.User)
                .WithMany(x => x.Series)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<EpisodioAssistido>(entity =>
        {
            entity.HasKey(x => x.Id);

            entity.Property(x => x.NomeEpisodio)
                .HasMaxLength(240)
                .IsRequired();

            entity.Property(x => x.PersonagemFavoritoNome)
                .HasMaxLength(200)
                .IsRequired();

            entity.HasIndex(x => new
            {
                x.UserId,
                x.ExternalEpisodeId
            }).IsUnique();

            entity.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<EpisodioAssistidoSentimento>(entity =>
        {
            entity.HasKey(x => new
            {
                x.EpisodioAssistidoId,
                x.Sentimento
            });

            entity
                .HasOne(x => x.EpisodioAssistido)
                .WithMany(x => x.Sentimentos)
                .HasForeignKey(
                    x => x.EpisodioAssistidoId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
