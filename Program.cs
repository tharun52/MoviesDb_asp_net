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


    var query = db.Movies.AsQueryable();

    // Apply search filter if needed
    if (!string.IsNullOrEmpty(search))
    {
        query = query.Where(m => EF.Functions.Like(m.Title, $"%{search}%") ||
                                 EF.Functions.Like(m.Overview, $"%{search}%") ||
                                 EF.Functions.Like(m.Genres, $"%{search}%") ||
                                 EF.Functions.Like(m.Language, $"%{search}%"));
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

    var movieCards = GenerateMovieCards(movies, http.Session.GetString("isAdmin"));

    return Results.Content(movieCards, "text/html");
});

static string GenerateMovieCards(List<Movie> movies, string isAdmin = "false")
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

                    {(isAdmin == "true" ? $@"
                        <div class='mt-2 d-flex justify-content-between'>
                            <form method='post' action='/deletemovie' onsubmit='return confirm(""Are you sure you want to delete this movie?"")'>
                                <input type='hidden' name='id' value='{movie.Id}' />
                                <button type='submit' class='btn btn-danger btn-sm'>Delete</button>
                            </form>
                            <form method='get' action='/editmovie'>
                                <input type='hidden' name='id' value='{movie.Id}' />
                                <button type='submit' class='btn btn-warning btn-sm'>Edit</button>
                            </form>
                        </div>
                    " : "")}
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
        return Results.Redirect("/adminlogin");
    }
});

app.MapPost("/deletemovie", async (MovieContext db, HttpContext context) =>
{
    if (context.Session.GetString("isAdmin") != "true")
    {
        context.Response.Redirect("/adminlogin");
        return;
    }

    var form = await context.Request.ReadFormAsync();
    var id = int.Parse(form["id"]);

    var movie = await db.Movies.FindAsync(id);
    if (movie != null)
    {
        db.Movies.Remove(movie);
        await db.SaveChangesAsync();

        var alert = $"<script>alert('Movie \"{movie.Title}\" has been deleted.'); window.location='/adminindex';</script>";
        await context.Response.WriteAsync(alert);
    }
    else
    {
        var alert = "<script>alert('Movie not found.'); window.location='/adminindex';</script>";
        await context.Response.WriteAsync(alert);
    }
});

app.MapGet("/editmovie", async (MovieContext db, HttpContext context) =>
{
    if (context.Session.GetString("isAdmin") == "true")
    {
        var id = context.Request.Query["id"];
        if (int.TryParse(id, out int movieId))
        {
            var movie = await db.Movies.FindAsync(movieId);
            if (movie != null)
            {
                var html = await File.ReadAllTextAsync("wwwroot/editmovie.html");
                html = html.Replace("{{MOVIE_ID}}", movie.Id.ToString())
                           .Replace("{{MOVIE_TITLE}}", movie.Title)
                           .Replace("{{MOVIE_OVERVIEW}}", movie.Overview)
                           .Replace("{{MOVIE_RATING}}", movie.User_Rating.ToString())
                           .Replace("{{MOVIE_LANGUAGE}}", movie.Language)
                           .Replace("{{MOVIE_RELEASEDATE}}", movie.Release_Date);
                await context.Response.WriteAsync(html);
            }
            else
            {
                context.Response.Redirect("/adminindex");
            }
        }
        else
        {
            context.Response.Redirect("/adminindex");
        }
    }
    else
    {
        context.Response.Redirect("/adminlogin");
    }
});

app.MapPost("/editmovie", async (MovieContext db, HttpContext context) =>
{
    if (context.Session.GetString("isAdmin") != "true")
    {
        context.Response.Redirect("/adminlogin");
        return;
    }

    var form = await context.Request.ReadFormAsync();
    var movieId = int.Parse(form["id"]);
    var title = form["title"];
    var overview = form["overview"];
    var rating = float.Parse(form["rating"]);
    var language = form["language"];
    
    // Convert the release date from the form into a string
    var releaseDate = form["releaseDate"];  // this is a string in "yyyy-MM-dd" format

    var movie = await db.Movies.FindAsync(movieId);
    if (movie != null)
    {
        movie.Title = title;
        movie.Overview = overview;
        movie.User_Rating = rating;
        movie.Language = language;
        movie.Release_Date = releaseDate; // storing it as a string

        await db.SaveChangesAsync();

        context.Response.Redirect("/adminindex");
    }
    else
    {
        context.Response.Redirect("/adminindex");
    }
});


app.Run();
