namespace RedditReports.Application.Abstractions
{
	public interface IRedditApiClient
	{
		event EventHandler<RedditDataReceivedEventArgs> RedditDataReceived;
		Task FetchAsync(string subredditName);
	}
}
