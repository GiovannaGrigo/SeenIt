CREATE SCHEMA IF NOT EXISTS "{{schema}}";
CREATE TABLE "{{schema}}"."Users" (
 "Id" uuid NOT NULL PRIMARY KEY, "Name" varchar(120) NOT NULL,
 "Email" varchar(200) NOT NULL, "PasswordHash" text NOT NULL,
 "AvatarFileName" varchar(200), "CreatedAtUtc" timestamptz NOT NULL);
CREATE UNIQUE INDEX "IX_Users_Email" ON "{{schema}}"."Users" ("Email");
CREATE TABLE "{{schema}}"."UserSeries" (
 "Id" uuid NOT NULL PRIMARY KEY, "UserId" uuid NOT NULL REFERENCES "{{schema}}"."Users"("Id") ON DELETE CASCADE,
 "ExternalSeriesId" integer NOT NULL, "Name" varchar(240) NOT NULL,
 "PosterUrl" varchar(600), "Summary" varchar(3000), "Status" integer NOT NULL,
 "AddedAtUtc" timestamptz NOT NULL, "UpdatedAtUtc" timestamptz NOT NULL,
 "IsFavorite" boolean NOT NULL DEFAULT false, "IsAnime" boolean);
CREATE UNIQUE INDEX "IX_UserSeries_UserId_ExternalSeriesId" ON "{{schema}}"."UserSeries" ("UserId","ExternalSeriesId");
CREATE TABLE "{{schema}}"."EpisodiosAssistidos" (
 "Id" uuid NOT NULL PRIMARY KEY, "UserId" uuid NOT NULL REFERENCES "{{schema}}"."Users"("Id") ON DELETE CASCADE,
 "ExternalSeriesId" integer NOT NULL, "ExternalEpisodeId" integer NOT NULL,
 "Temporada" integer NOT NULL, "NumeroEpisodio" integer NOT NULL,
 "NomeEpisodio" varchar(240) NOT NULL, "ExternalCharacterId" integer NOT NULL,
 "PersonagemFavoritoNome" varchar(200) NOT NULL, "AssistidoEmUtc" timestamptz NOT NULL);
CREATE UNIQUE INDEX "IX_EpisodiosAssistidos_UserId_ExternalEpisodeId" ON "{{schema}}"."EpisodiosAssistidos" ("UserId","ExternalEpisodeId");
CREATE TABLE "{{schema}}"."EpisodiosAssistidosSentimentos" (
 "EpisodioAssistidoId" uuid NOT NULL REFERENCES "{{schema}}"."EpisodiosAssistidos"("Id") ON DELETE CASCADE,
 "Sentimento" integer NOT NULL, PRIMARY KEY("EpisodioAssistidoId","Sentimento"));
