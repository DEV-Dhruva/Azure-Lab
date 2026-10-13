
using System.Net.Http.Json;
using System.Text.Json;
using Npgsql;
using Pgvector;
using Pgvector.Npgsql;

var endpoint = Environment.GetEnvironmentVariable("AZURE_AI_ENDPOINT")
    ?? throw new Exception("Set AZURE_AI_ENDPOINT");

var apiKey = Environment.GetEnvironmentVariable("AZURE_AI_KEY")
    ?? throw new Exception("Set AZURE_AI_KEY");

var deployment = Environment.GetEnvironmentVariable("AZURE_AI_DEPLOYMENT")
    ?? throw new Exception("Set AZURE_AI_DEPLOYMENT");

var connectionString =
    Environment.GetEnvironmentVariable("AZURE_POSTGRES_CONNECTION")
    ?? throw new Exception("Set AZURE_POSTGRES_CONNECTION");

using var http = new HttpClient();
http.DefaultRequestHeaders.Add("api-key", apiKey);

async Task<float[]> Embed(string text)
{
    var url = $"{endpoint.TrimEnd('/')}/embeddings";

    using var response = await http.PostAsJsonAsync(url, new
    {
        model = deployment,
        input = text
    });

    response.EnsureSuccessStatusCode();

    using var json = JsonDocument.Parse(
        await response.Content.ReadAsStringAsync());

    return json.RootElement
        .GetProperty("data")[0]
        .GetProperty("embedding")
        .EnumerateArray()
        .Select(x => x.GetSingle())
        .ToArray();
}

var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
dataSourceBuilder.UseVector();

await using var dataSource = dataSourceBuilder.Build();
await using var db = await dataSource.OpenConnectionAsync();

var notes = new[]
{
    ("Azure PostgreSQL",
     "Azure provides a managed PostgreSQL database service."),
    ("Vector Search",
     "Vector search retrieves semantically similar text using embeddings."),
    ("C# Database Access",
     "Npgsql allows a .NET application to connect to PostgreSQL.")
};

foreach (var (title, content) in notes)
{
    var embedding = await Embed(content);

    if (embedding.Length != 1536)
        throw new Exception(
            $"Expected 1536 dimensions, received {embedding.Length}.");

    await using var insert = new NpgsqlCommand("""
        INSERT INTO ai_notes (title, content, embedding)
        VALUES (@title, @content, @embedding)
        """, db);

    insert.Parameters.AddWithValue("title", title);
    insert.Parameters.AddWithValue("content", content);
    insert.Parameters.AddWithValue("embedding", new Vector(embedding));

    await insert.ExecuteNonQueryAsync();

    Console.WriteLine($"Embedded and saved: {title}");
}

Console.Write("\nAsk a question: ");
var question = Console.ReadLine();

if (string.IsNullOrWhiteSpace(question))
    return;

var queryEmbedding = await Embed(question);

await using var search = new NpgsqlCommand("""
    SELECT title, content,
           embedding <=> @query AS distance
    FROM ai_notes
    ORDER BY embedding <=> @query
    LIMIT 2
    """, db);

search.Parameters.AddWithValue("query", new Vector(queryEmbedding));

await using var reader = await search.ExecuteReaderAsync();

Console.WriteLine("\nMost relevant notes:");

while (await reader.ReadAsync())
{
    Console.WriteLine($"\n{reader["title"]}");
    Console.WriteLine(reader["content"]);
    Console.WriteLine($"Distance: {reader.GetDouble(2):0.0000}");
}
