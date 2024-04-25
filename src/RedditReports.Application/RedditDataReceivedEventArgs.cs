using RedditReports.Domain.Models;

namespace RedditReports.Application
{
	public class RedditDataReceivedEventArgs
	{
		public List<Post> Posts { get; } = new();
	}
}
