using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();
// builder.AddSqlServerDbContext<HeroDbContext>("sql-db");
builder.Services.AddDbContext<HeroDbContext>();
builder.Services.AddSingleton(new OllamaEmbeddingGenerator(new Uri("http://localhost:11434"), "all-minilm:33m"));

var app = builder.Build();

var dbContext = app.Services.GetRequiredService<HeroDbContext>();
await dbContext.Database.EnsureCreatedAsync();

var generator = app.Services.GetRequiredService<OllamaEmbeddingGenerator>();
var query = "hero with super speed";
var embedding = await generator.GenerateEmbeddingVectorAsync(query);
var heroes = await dbContext.Heroes
    .OrderBy(h => EF.Functions.VectorDistance("cosine",h.Embeddings, embedding.ToArray()))
    .Take(2)
    .ToListAsync();

foreach (var hero in heroes)
{
    Console.WriteLine($"Name: {hero.Name}");
    Console.WriteLine($"Description: {hero.Description}");
    Console.WriteLine();
}

public static class HeroFactory
{
    public static Hero[] Heroes =>
    [
        new Hero
        {
            Id = Guid.NewGuid(),
            Name = "Superman",
            Description = "The Man of Steel, Superman is an alien from Krypton with superhuman strength, speed, flight, and near invincibility. A symbol of hope, he protects Earth with his moral compass and his commitment to justice.",
            // Powers = ["Superhuman strength", "Flight", "Heat vision", "Freezing breath", "Super speed", "Enhanced hearing", "Healing factor"]
        },
        new Hero
        {
            Id = Guid.NewGuid(),
            Name = "Batman",
            Description = "The Dark Knight of Gotham, Batman is a billionaire vigilante who uses his intellect, martial arts expertise, and advanced technology to fight crime. Driven by the loss of his parents, he embodies justice through fear and strategy. He is also a billionaire with lots of money.I ne",
        },
        new Hero
        {
            Id = Guid.NewGuid(),
            Name = "Wonder Woman",
            Description = "An Amazonian warrior princess, Wonder Woman possesses superhuman strength, agility, and combat skills. Armed with the Lasso of Truth and her indomitable spirit, she fights for peace and equality as a champion of Themyscira.",
        },
        new Hero
        {
            Id = Guid.NewGuid(),
            Name = "Flash",
            Description = "The Scarlet Speedster, Flash is a hero with the ability to move at incredible speeds, thanks to his connection to the Speed Force. Known for his quick wit and big heart, he races to save lives and outpace evil.",
        }
    ];
}

public class HeroDbContext : DbContext
{
    private readonly OllamaEmbeddingGenerator _generator;
    private readonly IConfiguration _config;

    public DbSet<Hero> Heroes { get; set; }

    public HeroDbContext(OllamaEmbeddingGenerator generator, IConfiguration config)
    {
        _generator = generator;
        _config = config;
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        var connectionString = _config.GetConnectionString("sql-db");
        optionsBuilder.UseSqlServer(connectionString, o => o.UseVectorSearch());
        optionsBuilder.UseAsyncSeeding(async (ctx, _, ct) =>
        {
            if (await ctx.Set<Hero>().AnyAsync())
                return;

            foreach (var hero in HeroFactory.Heroes)
            {
                Console.Write(".");
                var embedding = await _generator.GenerateEmbeddingVectorAsync(hero.Description);
                hero.Embeddings = embedding.ToArray();
                ctx.Set<Hero>().Add(hero);
            }

            await ctx.SaveChangesAsync();
        });
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Hero>(b =>
        {
            // TODO: Need to use Azure SQL to get support for vector columns
            b.Property(b => b.Embeddings)
                .HasColumnType("vector(1536)");
            b.ToTable("HeroesVector");
        });
    }
}

public class Hero
{
    public Guid Id { get; set; }

    public string Name { get; set; }

    public string Description { get; set; }

    public float[] Embeddings { get; set; } = [];
}
