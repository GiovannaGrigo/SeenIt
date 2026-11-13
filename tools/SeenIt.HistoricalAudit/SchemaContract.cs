namespace SeenIt.HistoricalAudit;

internal static class SchemaContract
{
    internal static readonly string[] Tables = ["Users", "UserSeries", "EpisodiosAssistidos", "EpisodiosAssistidosSentimentos"];

    // Frozen from InitialPostgres + AddSeriesFavorites + AddSeriesAnimeClassification.
    // Compare structural signatures, not names of constraints or column physical order.
    internal static List<(string Kind, string Signature)> Expected(string schema)
    {
        List<(string, string)> expected = [];
        void Cols(string table, string spec)
        {
            foreach (var token in spec.Split(' '))
            {
                var p = token.Split(':');
                var type = p[1].TrimEnd('?');
                var nullable = p[1].EndsWith('?');
                var varchar = int.TryParse(type, out var limit);
                expected.Add(("column", $"{table}|{p[0]}|pg_catalog.{(varchar ? "varchar" : type)}|{(!nullable).ToString().ToLowerInvariant()}|{(varchar ? limit.ToString() : "-")}|||{(varchar || type=="text" ? "pg_catalog.default" : "-")}"));
            }
        }
        Cols("Users", "Id:uuid Name:120 Email:200 PasswordHash:text AvatarFileName:200? CreatedAtUtc:timestamptz");
        Cols("UserSeries", "Id:uuid UserId:uuid ExternalSeriesId:int4 Name:240 PosterUrl:600? Summary:3000? Status:int4 IsFavorite:bool IsAnime:bool? AddedAtUtc:timestamptz UpdatedAtUtc:timestamptz");
        Cols("EpisodiosAssistidos", "Id:uuid UserId:uuid ExternalSeriesId:int4 ExternalEpisodeId:int4 Temporada:int4 NumeroEpisodio:int4 NomeEpisodio:240 ExternalCharacterId:int4 PersonagemFavoritoNome:200 AssistidoEmUtc:timestamptz");
        Cols("EpisodiosAssistidosSentimentos", "EpisodioAssistidoId:uuid Sentimento:int4");
        void Index(string table, string columns, bool primary)
        {
            var keys=columns.Split(',');
            var collations=string.Join(',',keys.Select(k=>k=="Email" ? "pg_catalog.default" : "-"));
            var classes=string.Join(',',keys.Select(k=>"pg_catalog." + (k=="Email" ? "text_ops" : k is "Id" or "UserId" or "EpisodioAssistidoId" ? "uuid_ops" : "int4_ops")));
            expected.Add(("index",$"{table}|{columns}|true|{primary.ToString().ToLowerInvariant()}|true|true|false|false|true|btree|false|true|{collations}|{string.Join(',',keys.Select(_=>"0"))}|true|true|false|{classes}"));
        }
        foreach (var table in Tables)
        {
            expected.Add(("table", $"{table}|r|p|false|false|false|false|false"));
            var keys = table == "EpisodiosAssistidosSentimentos" ? "EpisodioAssistidoId,Sentimento" : "Id";
            expected.Add(("constraint", $"{table}|p|{keys}|||| | | |true|false|false"));
            Index(table, keys, true);
        }
        void Fk(string table, string column, string target)
        {
            expected.Add(("constraint",$"{table}|f|{column}|{schema}|{target}|Id|c|a|s|true|false|false"));
            foreach(var (owner,function,type) in new[]{(table,"RI_FKey_check_ins",5),(table,"RI_FKey_check_upd",17),
                (target,"RI_FKey_cascade_del",9),(target,"RI_FKey_noaction_upd",17)})
                expected.Add(("fk_trigger",$"{owner}|{table}|{target}|pg_catalog.{function}|{type}|O|false|false"));
        }
        Fk("UserSeries", "UserId", "Users");
        Fk("EpisodiosAssistidos", "UserId", "Users");
        Fk("EpisodiosAssistidosSentimentos", "EpisodioAssistidoId", "EpisodiosAssistidos");
        Index("Users", "Email", false);
        Index("UserSeries", "UserId,ExternalSeriesId", false);
        Index("EpisodiosAssistidos", "UserId,ExternalEpisodeId", false);
        expected.Add(("default", "UserSeries|IsFavorite|false"));
        return expected;
    }

    internal static List<string> Differences(List<(string Kind, string Signature)> actual, string schema)
    {
        var expected = Expected(schema);
        // Multisets also detect duplicated indexes/constraints with the same structure.
        var a = actual.GroupBy(x => x).ToDictionary(x => x.Key, x => x.Count());
        var e = expected.GroupBy(x => x).ToDictionary(x => x.Key, x => x.Count());
        return a.Keys.Union(e.Keys).Where(k => a.GetValueOrDefault(k) != e.GetValueOrDefault(k))
            .Select(k => k.Kind).Order().ToList();
    }
}
