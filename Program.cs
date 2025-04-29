using MovieApp.Models;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddDbContext<MovieContext>(options =>
    options.UseSqlite("Data Source=movies.db"));

var app = builder.Build();

app.UseStaticFiles();

// Main Page
app.MapGet("/", async (MovieContext db) =>
{
    var movies = await db.Movies.ToListAsync();
    var template = await File.ReadAllTextAsync("Views/index.html");

    var movieCards = GenerateMovieCards(movies);

    var finalHtml = template
        .Replace("{{MOVIES}}", movieCards)
        .Replace("{{SEARCH_TERM}}", "");

    return Results.Content(finalHtml, "text/html");
});

// API for AJAX Search
app.MapGet("/api/movies", async (MovieContext db, HttpContext http) =>
{
    var search = http.Request.Query["search"].ToString();
var sortBy = http.Request.Query["sortBy"].ToString();
var order = http.Request.Query["order"].ToString().ToLower();

// Set default sort by title ascending
if (string.IsNullOrEmpty(sortBy))
    sortBy = "title";
if (string.IsNullOrEmpty(order))
    order = "asc";

    var query = db.Movies.AsQueryable();

    // Apply search filter if needed
    if (!string.IsNullOrEmpty(search))
    {
        query = query.Where(m => EF.Functions.Like(m.Title, $"%{search}%"));
    }

    // Apply sorting (always)
    switch (sortBy)
    {
        case "title":
            query = order == "desc" ? query.OrderByDescending(m => m.Title) : query.OrderBy(m => m.Title);
            break;
        case "releasedate":
            query = order == "desc" ? query.OrderByDescending(m => m.Release_Date) : query.OrderBy(m => m.Release_Date);
            break;
        case "rating":
            query = order == "desc" ? query.OrderByDescending(m => m.User_Rating) : query.OrderBy(m => m.User_Rating);
            break;
        default:
            query = query.OrderBy(m => m.Title); // enforce default fallback
            break;
    }


    // Fetch movies and round ratings to 2 decimal places
    var movies = await query.ToListAsync();

    // Round ratings to 2 decimal places
    foreach (var movie in movies)
    {
        movie.User_Rating = Math.Round(movie.User_Rating, 2);
    }

    var movieCards = GenerateMovieCards(movies);

    return Results.Content(movieCards, "text/html");
});
static string GenerateMovieCards(List<Movie> movies)
{
    if (movies.Count == 0)
    {
        return "<p>No movies found.</p>";
    }

    return string.Join("", movies.Select(movie => $@"
        <div class='col'>
            <div class='card movie-card h-100'>
                <img src='{movie.Poster_Path}' class='card-img-top' alt='{movie.Title}'>
                <div class='card-body'>
                    <h5 class='card-title'>{movie.Title}</h5>
                    <p class='card-text'>{movie.Overview}</p>
                </div>
                <div class='card-footer'>
                    <small class='text-muted'>Rating: {movie.User_Rating} | Language: {movie.Language} | Released: {movie.Release_Date}</small>
                </div>
            </div>
        </div>
    "));
}

app.Run();
