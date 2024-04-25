using RedditReports.Domain.Results;

namespace RedditReports.Application.Results
{
	public class RedditApiResponseResult<T> : HttpResponseResult<T> where T : class
	{
		public int RemainingRequests { get; init; }
		public int RequestsUsed { get; init; }
		public int ResetTimeInSeconds { get; init; }
		public string? After { get; init; }
	}
}
