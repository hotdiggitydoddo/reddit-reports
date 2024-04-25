namespace RedditReports.Domain.Models
{
    public class Post
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string Author { get; set; }
        public int Ups { get; set; }
        public string Subreddit { get; set; }
	}
}
