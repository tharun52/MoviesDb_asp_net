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


// API for Querying movie cards
app.MapGet("/api/movies", async (MovieContext db, HttpContext http) =>
{
    var search = http.Request.Query["search"].ToString();
    var sortBy = http.Request.Query["sortBy"].ToString();
    var order = http.Request.Query["order"].ToString().ToLower();


    var query = db.Movies.AsQueryable();

    // Apply search filter
    if (!string.IsNullOrEmpty(search))
    {
        query = query.Where(m => EF.Functions.Like(m.Title, $"%{search}%") ||
                                 EF.Functions.Like(m.Overview, $"%{search}%") ||
                                 EF.Functions.Like(m.Genres, $"%{search}%") ||
                                 EF.Functions.Like(m.Language, $"%{search}%"));
    }

    // Apply sorting 
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


    // Fetch movies 
    var movies = await query.ToListAsync();

    // Round ratings to 2 decimal places
    foreach (var movie in movies)
    {
        movie.User_Rating = Math.Round(movie.User_Rating, 2);
    }

    // generate the movie cards and check if admin to show crud functions
    var movieCards = GenerateMovieCards(movies, http.Session.GetString("isAdmin"));

    return Results.Content(movieCards, "text/html");
});

// Generate the movie cards html content with admin check
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


// User Index Page
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


// admin login page route
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

    // read password from appsettings.json to check if admin
    var config = context.RequestServices.GetRequiredService<IConfiguration>();
    var storedPassword = config["Admin:Password"];

    if (password == storedPassword)
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

// route for admin index
app.MapGet("/adminindex", async (MovieContext db, HttpContext context) =>
{
    if (context.Session.GetString("isAdmin") == "true")
    {
        var movies = await db.Movies.ToListAsync();
        var template = await File.ReadAllTextAsync("wwwroot/IndexAdmin.html");


        var adminMovieCards = GenerateMovieCards(movies, "true");
        var finalHtml = template.Replace("{{MOVIES}}", adminMovieCards).Replace("{{SEARCH_TERM}}", "");

        return Results.Content(finalHtml, "text/html");
    }
    else
    {
        return Results.Redirect("/adminlogin");
    }
});

// route for edit movie
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
                // Fetch distinct language codes from the database for language drop down menu
                var languages = await db.Movies
                    .Select(m => m.Language)
                    .Where(lang => !string.IsNullOrEmpty(lang))
                    .Distinct()
                    .OrderBy(lang => lang)
                    .ToListAsync();

                // Build <option> elements and mark the current language as selected
                var languageOptions = string.Join("\n", languages.Select(lang =>
                    movie.Language == lang
                        ? $"<option value=\"{lang}\" selected>{lang}</option>"
                        : $"<option value=\"{lang}\">{lang}</option>"
                ));

                // Load HTML and inject dynamic content
                var html = await File.ReadAllTextAsync("wwwroot/editmovie.html");
                html = html.Replace("{{MOVIE_ID}}", movie.Id.ToString())
                           .Replace("{{MOVIE_TITLE}}", movie.Title)
                           .Replace("{{MOVIE_OVERVIEW}}", movie.Overview)
                           .Replace("{{MOVIE_RATING}}", movie.User_Rating.ToString())
                           .Replace("{{MOVIE_LANGUAGE}}", movie.Language)
                           .Replace("{{MOVIE_RELEASEDATE}}", movie.Release_Date)
                           .Replace("{{LANGUAGE_OPTIONS}}", languageOptions)
                           .Replace("{{MOVIE_POSTER}}", movie.Poster_Path ?? "");

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

// handle edit
app.MapPost("/editmovie", async (MovieContext db, HttpContext context) =>
{
    // Ensure the user is an admin
    if (context.Session.GetString("isAdmin") != "true")
    {
        context.Response.Redirect("/adminlogin");
        return;
    }

    var form = await context.Request.ReadFormAsync();
    int movieId = int.Parse(form["id"]);

    var movie = await db.Movies.FindAsync(movieId);

    var title = form["title"];
    var overview = form["overview"];
    var rating = float.Parse(form["rating"]);
    var language = form["language"];
    var releaseDate = form["releaseDate"];
    string posterPath = movie.Poster_Path;

    if (movie != null)
    {

        // for handling poster upload
        if (form["posterOption"] == "upload" && context.Request.Form.Files.Count > 0)
        {
            var file = context.Request.Form.Files["posterUpload"];
            if (file != null && file.Length > 0)
            {
                var uploadsPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot/uploads");
                // Directory.CreateDirectory(uploadsPath); // Ensure folder exists

                var fileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
                var filePath = Path.Combine(uploadsPath, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                posterPath = "/uploads/" + fileName;
            }
        }
        // for handling poster as link
        else if (form["posterOption"] == "link")
        {
            posterPath = form["posterLink"].ToString();
        }

        // Update fields
        movie.Title = title;
        movie.Overview = overview;
        movie.User_Rating = rating;
        movie.Language = language;
        movie.Release_Date = releaseDate;
        movie.Poster_Path = posterPath;

        await db.SaveChangesAsync();
    }

    context.Response.Redirect("/adminindex");
});

// route for adding movies
app.MapGet("/addmovie", async (MovieContext db, HttpContext context) =>
{
    if (context.Session.GetString("isAdmin") == "true")
    {
        // Fetch distinct language codes from the database for language drop down menu
        var languages = await db.Movies
           .Select(m => m.Language)
           .Where(lang => !string.IsNullOrEmpty(lang))
           .Distinct()
           .OrderBy(lang => lang)
           .ToListAsync();

        var languageOptions = string.Join("\n", languages.Select(lang =>
            $"<option value=\"{lang}\">{lang}</option>"
        ));

        var html = await File.ReadAllTextAsync("wwwroot/addmovie.html");
        html = html.Replace("{{LANGUAGE_OPTIONS}}", languageOptions);
        await context.Response.WriteAsync(html);
    }
    else
    {
        context.Response.Redirect("/adminlogin");
    }
});

app.MapPost("/addmovie", async (MovieContext db, HttpContext context) =>
{
    if (context.Session.GetString("isAdmin") != "true")
    {
        context.Response.Redirect("/adminlogin");
        return;
    }

    var form = await context.Request.ReadFormAsync();

    // check if the title already existis
    var title = form["title"].ToString().Trim();
    if (await db.Movies.AnyAsync(m => m.Title == title))
    {
        // tiny HTML page that for alert
        var alertHtml = $@"<!DOCTYPE html>
            <html lang=""en"">
            <head>
            <meta charset=""utf-8"">
            <title>Duplicate Title</title>
            </head>
            <body>
            <script>
                alert('Movie \'{title}\' is already present.');
                window.location = '/addmovie';
            </script>
            </body>
            </html>";

        context.Response.ContentType = "text/html; charset=utf-8";
        await context.Response.WriteAsync(alertHtml);
        return;
    }


    // get rest of the fields
    var overview = form["overview"];
    var releaseDate = form["releaseDate"];
    var rating = double.TryParse(form["rating"], out double r) ? r : 0;
    var genres = form["genres"];
    var language = form["language"] == "custom"
                        ? form["customLanguage"].ToString()
                        : form["language"].ToString();

    string posterPath = form["posterLink"];

    // Handle poster upload
    var file = form.Files["posterUpload"];
    if (file != null && file.Length > 0)
    {
        var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
        Directory.CreateDirectory(uploadsFolder);

        var fileName = Guid.NewGuid().ToString() + Path.GetExtension(file.FileName);
        var filePath = Path.Combine(uploadsFolder, fileName);

        using var stream = new FileStream(filePath, FileMode.Create);
        await file.CopyToAsync(stream);

        posterPath = "/uploads/" + fileName;
    }

    // Create and save the new movie
    var movie = new Movie
    {
        Title = title,
        Overview = overview,
        Release_Date = releaseDate,
        Poster_Path = posterPath,
        User_Rating = rating,
        Genres = genres,
        Language = language
    };

    db.Movies.Add(movie);
    await db.SaveChangesAsync();

    context.Response.Redirect("/adminindex");
});


// route for deleting movies
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
        // Delete poster image if it was uploaded
        if (!string.IsNullOrEmpty(movie.Poster_Path) && movie.Poster_Path.StartsWith("/uploads/"))
        {
            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", movie.Poster_Path.TrimStart('/'));

            if (System.IO.File.Exists(filePath))
            {
                System.IO.File.Delete(filePath);
            }
        }

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

app.Run();
