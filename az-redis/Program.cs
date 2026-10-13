
using StackExchange.Redis;

string connectionString =
    Environment.GetEnvironmentVariable("REDIS_CONNECTION")
    ?? throw new Exception("Set REDIS_CONNECTION first.");

using var redis =
    await ConnectionMultiplexer.ConnectAsync(connectionString);

IDatabase db = redis.GetDatabase();

string key = "ai:answer:password-reset";

// 1. STORE DATA WITH AN EXPIRATION TIME
await db.StringSetAsync(
    key,
    "Go to Settings > Security > Reset Password.",
    TimeSpan.FromSeconds(60));

Console.WriteLine("1. Data stored successfully.");

// 2. RETRIEVE DATA
RedisValue answer = await db.StringGetAsync(key);

if (answer.HasValue)
{
    Console.WriteLine($"2. Cached answer: {answer}");
}
else
{
    Console.WriteLine("2. Cache miss.");
}

// 3. CHECK IF THE KEY EXISTS
bool exists = await db.KeyExistsAsync(key);
Console.WriteLine($"3. Key exists: {exists}");

// 4. UPDATE THE CACHED ANSWER
await db.StringSetAsync(
    key,
    "Open Settings > Account > Reset Password.",
    TimeSpan.FromSeconds(60));

Console.WriteLine($"4. Updated answer: {await db.StringGetAsync(key)}");

// 5. SET A NEW EXPIRATION TIME
bool expirySet = await db.KeyExpireAsync(
    key,
    TimeSpan.FromSeconds(120));

Console.WriteLine($"5. Expiration updated: {expirySet}");

// 6. DELETE THE CACHED ANSWER
bool deleted = await db.KeyDeleteAsync(key);

Console.WriteLine($"6. Key deleted: {deleted}");
Console.WriteLine(
    $"7. Key exists after deletion: {await db.KeyExistsAsync(key)}");
