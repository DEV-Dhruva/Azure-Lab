
using Microsoft.Azure.Cosmos;

string endpoint = Environment.GetEnvironmentVariable("COSMOS_ENDPOINT")
    ?? throw new Exception("Set COSMOS_ENDPOINT");

string key = Environment.GetEnvironmentVariable("COSMOS_KEY")
    ?? throw new Exception("Set COSMOS_KEY");

using CosmosClient client = new CosmosClient(endpoint, key);

Container students = client.GetContainer("LearningDB", "Students");

// 1. Insert or update students (upsert)
var student1 = new Student("s101", "Python", "Asha", 90);
var student2 = new Student("s102", "Python", "Rahul", 75);
var student3 = new Student("s103", "Azure", "Meera", 88);

await students.UpsertItemAsync(
    student1, new PartitionKey(student1.course));

await students.UpsertItemAsync(
    student2, new PartitionKey(student2.course));

await students.UpsertItemAsync(
    student3, new PartitionKey(student3.course));

Console.WriteLine("Students inserted or updated.");

// 2. Update Asha's score
student1 = student1 with { score = 95 };

await students.UpsertItemAsync(
    student1, new PartitionKey(student1.course));

Console.WriteLine("Asha's score updated.");

// 3. Query students using parameters
var query = new QueryDefinition(
    "SELECT * FROM c WHERE c.course = @course AND c.score >= @minScore")
    .WithParameter("@course", "Python")
    .WithParameter("@minScore", 80);

using FeedIterator<Student> iterator =
    students.GetItemQueryIterator<Student>(query);

while (iterator.HasMoreResults)
{
    FeedResponse<Student> page = await iterator.ReadNextAsync();

    foreach (Student student in page)
    {
        Console.WriteLine(
            $"{student.id} | {student.name} | " +
            $"{student.course} | {student.score}");
    }
}

public record Student(
    string id,
    string course,
    string name,
    int score);
