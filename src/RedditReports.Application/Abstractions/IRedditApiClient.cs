using RedditReports.Application.DTOs;
using RedditReports.Application.Results;
using RedditReports.Domain.Models;
using RedditReports.Domain.Results;

namespace RedditReports.Application.Abstractions
{
	public interface IRedditApiClient
	{
		Task<RedditApiResponseResult<List<Post>>> FetchPostsAsync(string subredditName, string after = null);
		Task<HttpResponseResult> AuthenticateAsync();
	}
}
