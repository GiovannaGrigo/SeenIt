CREATE TABLE IF NOT EXISTS "__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260915120902_InitialPostgres') THEN
    CREATE TABLE "Users" (
        "Id" uuid NOT NULL,
        "Name" character varying(120) NOT NULL,
        "Email" character varying(200) NOT NULL,
        "PasswordHash" text NOT NULL,
        "AvatarFileName" character varying(200),
        "CreatedAtUtc" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_Users" PRIMARY KEY ("Id")
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260915120902_InitialPostgres') THEN
    CREATE TABLE "EpisodiosAssistidos" (
        "Id" uuid NOT NULL,
        "UserId" uuid NOT NULL,
        "ExternalSeriesId" integer NOT NULL,
        "ExternalEpisodeId" integer NOT NULL,
        "Temporada" integer NOT NULL,
        "NumeroEpisodio" integer NOT NULL,
        "NomeEpisodio" character varying(240) NOT NULL,
        "ExternalCharacterId" integer NOT NULL,
        "PersonagemFavoritoNome" character varying(200) NOT NULL,
        "AssistidoEmUtc" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_EpisodiosAssistidos" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_EpisodiosAssistidos_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260915120902_InitialPostgres') THEN
    CREATE TABLE "UserSeries" (
        "Id" uuid NOT NULL,
        "UserId" uuid NOT NULL,
        "ExternalSeriesId" integer NOT NULL,
        "Name" character varying(240) NOT NULL,
        "PosterUrl" character varying(600),
        "Summary" character varying(3000),
        "Status" integer NOT NULL,
        "AddedAtUtc" timestamp with time zone NOT NULL,
        "UpdatedAtUtc" timestamp with time zone NOT NULL,
        CONSTRAINT "PK_UserSeries" PRIMARY KEY ("Id"),
        CONSTRAINT "FK_UserSeries_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260915120902_InitialPostgres') THEN
    CREATE TABLE "EpisodiosAssistidosSentimentos" (
        "EpisodioAssistidoId" uuid NOT NULL,
        "Sentimento" integer NOT NULL,
        CONSTRAINT "PK_EpisodiosAssistidosSentimentos" PRIMARY KEY ("EpisodioAssistidoId", "Sentimento"),
        CONSTRAINT "FK_EpisodiosAssistidosSentimentos_EpisodiosAssistidos_Episodio~" FOREIGN KEY ("EpisodioAssistidoId") REFERENCES "EpisodiosAssistidos" ("Id") ON DELETE CASCADE
    );
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260915120902_InitialPostgres') THEN
    CREATE UNIQUE INDEX "IX_EpisodiosAssistidos_UserId_ExternalEpisodeId" ON "EpisodiosAssistidos" ("UserId", "ExternalEpisodeId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260915120902_InitialPostgres') THEN
    CREATE UNIQUE INDEX "IX_Users_Email" ON "Users" ("Email");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260915120902_InitialPostgres') THEN
    CREATE UNIQUE INDEX "IX_UserSeries_UserId_ExternalSeriesId" ON "UserSeries" ("UserId", "ExternalSeriesId");
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20260915120902_InitialPostgres') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20260915120902_InitialPostgres', '10.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261105172758_AddSeriesFavorites') THEN
    ALTER TABLE "UserSeries" ADD "IsFavorite" boolean NOT NULL DEFAULT FALSE;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261105172758_AddSeriesFavorites') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261105172758_AddSeriesFavorites', '10.0.0');
    END IF;
END $EF$;
COMMIT;

START TRANSACTION;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261106114244_AddSeriesAnimeClassification') THEN
    ALTER TABLE "UserSeries" ADD "IsAnime" boolean;
    END IF;
END $EF$;

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM "__EFMigrationsHistory" WHERE "MigrationId" = '20261106114244_AddSeriesAnimeClassification') THEN
    INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
    VALUES ('20261106114244_AddSeriesAnimeClassification', '10.0.0');
    END IF;
END $EF$;
COMMIT;

