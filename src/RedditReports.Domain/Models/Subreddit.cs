namespace RedditReports.Domain.Models
{
	public class Subreddit
	{
		public string Name { get; }
		public List<Post> Posts { get; }

		public Subreddit(string name, List<Post> posts)
		{
			Name = name;
			Posts = posts;
		}

		public Tuple<string, int> GetMostUpvotedPost()
		{
			var post = Posts.MaxBy(x => x.Ups)!;
			return new Tuple<string, int>(post.Title, post.Ups);
		}

		public Tuple<string, int> GetUserWithMostPosts()
		{
			var topUser = Posts.GroupBy(x => x.Author).MaxBy(x => x.Count())!;
			return new Tuple<string, int>(topUser.Key, topUser.Count());
		}
	}
}
