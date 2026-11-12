using System.Net;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging.Abstractions;
using SeenIt.Api.External;

internal static class AnimeClassificationChecks
{
    public static async Task RunAsync()
    {
        using var handler = new ShowHandler();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://api.tvmaze.com/") };
        using var cache = new MemoryCache(new MemoryCacheOptions());
        using var shared = new CatalogCache();
        var client = new TvMazeClient(http, cache, NullLogger<TvMazeClient>.Instance, shared);

        async Task Expect(int id, bool? expected)
        {
            if (await client.IsAnimeAsync(id, CancellationToken.None) != expected)
                throw new InvalidOperationException($"Incorrect anime classification for {id}.");
        }

        await Expect(1, true);
        await Expect(2, false); // Western animation is not an anime.
        await Expect(3, false); // A Japanese live-action show is not an anime.
        await Expect(4, false); // Missing genres must be safe.
        var requests = handler.Requests;
        await Expect(1, true);
        await Expect(2, false);
        if (handler.Requests != requests)
            throw new InvalidOperationException("Both anime and series classifications must be cached.");

        await Expect(5, null); // Rate limiting must not persist a false classification.
        await Expect(5, true); // A later load retries unknown shows.
        await Expect(6, null); // A deleted title must leave the saved collection accessible.
        Console.WriteLine("Anime classification checks passed: genres, animation, live action, missing metadata, cache, rate limiting and retries.");
    }

    private sealed class ShowHandler : HttpMessageHandler
    {
        public int Requests { get; private set; }
        private bool rateLimited;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests++;
            var id = int.Parse(request.RequestUri!.Segments.Last());
            if (id == 6) return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            if (id == 5 && !rateLimited)
            {
                rateLimited = true;
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.TooManyRequests));
            }

            var genres = id switch
            {
                1 => "[\"Action\",\"anime\"]",
                2 => "[\"Comedy\"]",
                3 => "[\"Drama\"]",
                4 => "null",
                _ => "[\"Anime\"]"
            };
            var type = id == 3 ? "Scripted" : "Animation";
            var language = id == 2 ? "English" : "Japanese";
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent($$"""{"id":{{id}},"name":"Example","type":"{{type}}","language":"{{language}}","genres":{{genres}}} """,
                    System.Text.Encoding.UTF8, "application/json")
            });
        }
    }
}
