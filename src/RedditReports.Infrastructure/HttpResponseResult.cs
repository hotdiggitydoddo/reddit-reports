namespace RedditReports.Infrastructure
{
	public class HttpResponseResult<T>
	{
		public bool Success => ResponseStatusCode >= 200 && ResponseStatusCode < 400;
		public T? Result { get; init; }
		public int ResponseStatusCode { get; init; }
		public string ErrorMessage { get; init; }
	}
}
