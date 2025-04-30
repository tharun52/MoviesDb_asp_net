namespace MovieApp.Models;

public class Movie
{
    public int Id { get; set; }
    public string ?Title { get; set; }
    public string ?Overview { get; set; }
    public string ?Release_Date { get; set; }
    public string ?Poster_Path { get; set; }
    public double User_Rating { get; set; }
    public string ?Genres { get; set; }
    public string ?Language { get; set; }
    public bool Adult { get; set; }
}
