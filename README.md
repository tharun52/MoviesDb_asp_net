# MovieApp

A simple ASP.NET Core minimal‑API website for browsing and managing a collection of movies stored in a SQLite database.

---

## Features

* **750 movies** preloaded in `movies.db`
* **User‑facing index** (`/`) with live **search** and **sorting** by Title, Release Date or Rating
* **Admin area** behind a login (`/adminlogin`)

  * Password stored in **adminsettings.json** (default: `1234`)
* **CRUD operations** in the admin dashboard (`/adminindex`)

  * **Add** new movie (all fields including Genres, Poster Path, etc.)
  * **Edit** existing movie
  * **Delete** movie (and delete uploaded poster file if it exists)
* **Language** dropdown in Add/Edit forms populated dynamically from distinct DB values; “Custom” option to enter a new two‑letter code
* **Poster** field in Add/Edit supports either:

  1. **Link** to an external image URL
  2. **Upload** of an image file (saved under `wwwroot/uploads/`)

  * Live preview shown immediately upon selection
* Deleting a movie will also delete its uploaded poster file if it lives in `/uploads/`

---

## Installation

1. **Clone** the repo

   ```bash
   git clone https://github.com/tharun52/MovieApp.git
   cd MovieApp
   ```
2. **Configure**

   * Create `adminsettings.json` with:

     ```json
     {
       "Admin": { "Password": "1234" }
     }
     ```
   * Ensure `movies.db` (with 750 entries) is in the project root.
3. **Run**

   ```bash
   dotnet restore
   dotnet watch run
   ```
4. **Browse**

   * User index:  `http://localhost:5000/`
   * Admin login: `http://localhost:5000/adminlogin`

---

## Project Structure

```
MovieApp/
│
├─ wwwroot/  
│   ├─ index.html         ← User‑facing home template  
│   ├─ adminlogin.html    ← Admin login page  
│   ├─ IndexAdmin.html    ← Admin dashboard template  
│   ├─ editmovie.html     ← Admin edit form  
│   ├─ addmovie.html      ← Admin add form  
│   ├─ uploads/           ← Uploaded poster images  
│   └─ style.css          ← Custom styles  
│
├─ adminsettings.json     ← Admin password configuration  
├─ movies.db              ← SQLite DB with 750 movie records  
├─ Program.cs             ← All endpoints & business logic  
└─ Models/  
    ├─ Movie.cs           ← EF entity  
    └─ MovieContext.cs    ← EF DbContext  
```

---

## Routes & Screenshots

Below are the main workflows in the app, each combining both the display (GET) and action (POST) steps, along with a screenshot placeholder.

---

### Home & Search/Sort

**Endpoints:**

* **GET /** — Serves `index.html`, which includes a live search box and sort dropdowns.
* **GET /api/movies** — Returns an HTML fragment of movie cards filtered by search term and sorted by the chosen field (`title`, `releasedate`, `rating`). Invoked via AJAX by the home page script.

**Workflow:**

1. User navigates to `/`.
2. Page loads all movies sorted by title ascending.
3. As the user types or changes sort/order, JavaScript calls `/api/movies?search=...&sortBy=...&order=...`.
4. The server filters, sorts, rounds ratings, and returns only the cards HTML.
5. The UI updates in place—no full page reload.

**Screenshot:**
![Home & Search/Sort](/screenshots/home-search-sort.png)

---

### Admin Login

**Endpoints:**

* **GET /adminlogin** — Displays the login form (with any error injected).
* **POST /adminlogin** — Validates against the password in `adminsettings.json`, sets session if correct, and redirects to `/adminindex`; otherwise re-renders the form with a Bootstrap alert.

**Workflow:**

1. Admin navigates to `/adminlogin`.
2. Enters password and submits.
3. On success, session flag `isAdmin` is set and user is redirected; on failure, the same form displays an inline error message.

**Screenshot:**
![Admin Login](/screenshots/admin-login.png)

---

### Admin Dashboard (List, Edit, Delete)

**Endpoint:**

* **GET /adminindex** — Displays all movies with **Edit** and **Delete** buttons (only if `isAdmin` session flag is true).
* **POST /deletemovie** — Deletes the selected movie record and, if its poster was uploaded, removes the file from `wwwroot/uploads/` before redirecting back and showing a JavaScript alert.

**Workflow:**

1. Admin visits `/adminindex`.
2. The page lists movies; clicking **Delete** submits to `/deletemovie`.
3. The server removes the DB record and deletes the file if needed, then alerts and reloads the dashboard.

**Screenshot:**
![Admin Dashboard](/screenshots/admin-dashboard.png)

---

### Add Movie

**Endpoints:**

* **GET /addmovie** — Renders the add‑movie form with dynamic language dropdown (from DB) plus a “Custom” input option, and poster input (upload vs. link).
* **POST /addmovie** — Processes form data: saves new movie record, handles poster upload (to `wwwroot/uploads/`) or link, and redirects to `/adminindex`.

**Workflow:**

1. Admin clicks **Add Movie**.
2. Fills in Title, Overview, Release Date, Genres, Rating, selects or enters Language, and either uploads or links a poster.
3. Submits to `/addmovie`; server saves the image file if provided and creates a DB entry.

**Screenshot:**
![Add Movie](/screenshots/add-movie.png)

---

### Edit Movie

**Endpoints:**

* **GET /editmovie?id={id}** — Shows a pre‑filled edit form for the chosen movie, including current poster preview and controls to keep, upload, or link a new image.
* **POST /editmovie** — Updates the movie record; if a new poster is provided, replaces the old file or updates the link.

**Workflow:**

1. Admin clicks **Edit** on a movie.
2. The form loads with existing data and poster.
3. Admin modifies fields and chooses how to update the poster.
4. Submits; server replaces the poster file in `wwwroot/uploads/` if needed and saves changes to DB.

**Screenshot:**
![Edit Movie](/screenshots/edit-movie.png)

---
