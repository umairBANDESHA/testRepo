using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

class Program
{
    static async Task Main(string[] args)
    {
        // Load configuration
        var config = new ConfigurationBuilder()
            .AddJsonFile("appsettings.json")
            .Build();

        var connectionString = config.GetConnectionString("DefaultConnection");

        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseSqlServer(connectionString);

        using var context = new AppDbContext(optionsBuilder.Options);

        // Ensure database is created (use migrations in prod)
        await context.Database.MigrateAsync();

        // Add a blog if none exist
        if (!await context.Blogs.AnyAsync())
        {
            context.Blogs.Add(new Blog { Url = "https://example.com" });
            await context.SaveChangesAsync();
        }

        // EF Core query: Include posts (eager loading), AsNoTracking
        var blogs = await context.Blogs
            .Include(b => b.Posts)
            .AsNoTracking()
            .ToListAsync();

        foreach (var blog in blogs)
        {
            Console.WriteLine($"Blog: {blog.Url}, Posts Count: {blog.Posts.Count}");
        }

        // ADO.NET transaction example for inserting a post
        using var sqlConnection = new SqlConnection("Data Source=(localdb)\\MSSQLLocalDB;Initial Catalog=GetItDonedb;Integrated Security=True;Connect Timeout=30;Encrypt=False;Trust Server Certificate=False;Application Intent=ReadWrite;Multi Subnet Failover=False");
        await sqlConnection.OpenAsync();

        using var transaction = sqlConnection.BeginTransaction();
        try
        {
            var command = sqlConnection.CreateCommand();
            command.Transaction = transaction;
            command.CommandText = "INSERT INTO Posts (Title, Content, BlogId) VALUES (@title, @content, @blogId)";
            command.Parameters.Add(new SqlParameter("@title", "ADO.NET Post"));
            command.Parameters.Add(new SqlParameter("@content", "Content via ADO.NET"));
            command.Parameters.Add(new SqlParameter("@blogId", blogs[0].BlogId));

            await command.ExecuteNonQueryAsync();

            transaction.Commit();
            Console.WriteLine("Inserted post via ADO.NET transaction");
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            Console.WriteLine($"Transaction rolled back: {ex.Message}");
        }
    }
}
