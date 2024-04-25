using RedditReports.Application.DTOs;
using RedditReports.Domain.Results;

namespace RedditReports.Application.Abstractions
{
	public interface IRedditApiClient
	{
		event EventHandler<RedditDataReceivedEventArgs> RedditDataReceived;
		Task FetchAsync(string subredditName);
		Task<HttpResponseResult> AuthenticateAsync();
	}
}
