const searchInput = document.getElementById("searchInput");
const sortBySelect = document.getElementById("sortBy");
const orderBySelect = document.getElementById("orderBy");
const movieGrid = document.getElementById("movieGrid");

// Reusable function to fetch and load movies
const loadMovies = async () => {
    const query = searchInput.value.trim();
    const sortBy = sortBySelect.value;
    const order = orderBySelect.value;

    try {
        const response = await fetch(
            `/api/movies?search=${encodeURIComponent(query)}&sortBy=${encodeURIComponent(sortBy)}&order=${encodeURIComponent(order)}`
        );
        if (!response.ok) throw new Error("Failed to load movies");
        const html = await response.text();
        movieGrid.innerHTML = html;
    } catch (error) {
        console.error(error);
        movieGrid.innerHTML = "<p class='text-danger'>Error loading movies.</p>";
    }
};


// Load on input change, sort and order change
searchInput.addEventListener("input", loadMovies);
sortBySelect.addEventListener("change", loadMovies);
orderBySelect.addEventListener("change", loadMovies);

// Load initial data
window.addEventListener("DOMContentLoaded", loadMovies);
