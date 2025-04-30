using MovieApp.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddDbContext<MovieContext>(options =>
    options.UseSqlite("Data Source=movies.db"));
builder.Services.AddDistributedMemoryCache();
builder.Services.AddSession();
builder.Services.AddHttpContextAccessor();

var app = builder.Build();

app.UseStaticFiles();
app.UseSession();

// Main Page
app.MapGet("/", async (MovieContext db) =>
{
    var movies = await db.Movies.ToListAsync();
    var template = await File.ReadAllTextAsync("wwwroot/index.html");

    var movieCards = GenerateMovieCards(movies);

    var finalHtml = template
        .Replace("{{MOVIES}}", movieCards)
        .Replace("{{SEARCH_TERM}}", "");

    return Results.Content(finalHtml, "text/html");
});

// API for Query in index page
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
        <div class='col-md-4 mb-4'>
            <div class='card movie-card h-100'>
                <img src='{movie.Poster_Path}' class='card-img-top' alt='{movie.Title}'>
                <div class='card-body'>
                    <h5 class='card-title'>{movie.Title}</h5>
                    <p class='card-text'>{movie.Overview}</p>
                    <a href='/edit/{movie.Id}' id='editbutton' class='btn btn-primary'>Edit</a>
                </div>
                <div class='card-footer'>
                    <small class='text-muted'>Rating: {movie.User_Rating} | Language: {movie.Language} | Released: {movie.Release_Date}</small>
                </div>
            </div>
        </div>
    "));
}


// login page Route
app.MapGet("/adminlogin", async context =>
{
    var html = await File.ReadAllTextAsync("wwwroot/adminlogin.html");
    html = html.Replace("<!--ERROR_PLACEHOLDER-->", " "); // clear any old alert
    await context.Response.WriteAsync(html);
});

// Handle login
app.MapPost("/adminlogin", async context =>
{
    var form = await context.Request.ReadFormAsync();
    var password = form["password"];
    var html = await File.ReadAllTextAsync("wwwroot/adminlogin.html");
    if (password == "1234")
    {
        context.Session.SetString("isAdmin", "true");
        context.Response.Redirect("/adminindex");
    }
    else
    {
        var alert = @"<div class='alert alert-danger' role='alert'>Invalid password. Please try again.</div>";
        html = html.Replace("<!--ERROR_PLACEHOLDER-->", alert);
        await context.Response.WriteAsync(html);
    }
});


app.MapGet("/adminindex", async (MovieContext db, HttpContext context) =>
{
    if (context.Session.GetString("isAdmin") == "true")
    {
        var movies = await db.Movies.ToListAsync();
        var template = await File.ReadAllTextAsync("wwwroot/IndexAdmin.html");

        var adminMovieCards = GenerateMovieCards(movies); // See Step 3
        var finalHtml = template.Replace("{{MOVIES}}", adminMovieCards).Replace("{{SEARCH_TERM}}", "");

        
        return Results.Content(finalHtml, "text/html");
    }
    else
    {
        context.Response.Redirect("/adminlogin");
        return Results.StatusCode(403);
    }
});


app.Run();
