using RedditReports.Domain.Models;

namespace RedditReports.Application
{
	public class RedditPostDataReceivedEventArgs
	{
		public List<Post> Posts { get; } = new();
		public bool AdditionalPostsAvailable { get; init; }
		public string SubredditName { get; init; }
	}
}
