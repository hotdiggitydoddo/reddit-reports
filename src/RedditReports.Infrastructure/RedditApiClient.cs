using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json;
using RedditReports.Application;
using RedditReports.Application.Abstractions;
using RedditReports.Application.DTOs.Posts;

namespace RedditReports.Infrastructure
{
	public class RedditApiClient : IRedditApiClient
	{
		private readonly SemaphoreSlim _concurrencySemaphore;
		private int _remainingRequests;
		private int _rateLimitResetTime;
		private AuthModel? _authModel;

		private const string RedditBaseUrl = "https://oauth.reddit.com";
		private const string UserAgent = "RedditReports/0.0.1";

		private int _processed = 0;

		public event EventHandler<RedditDataReceivedEventArgs> RedditDataReceived;

		public RedditApiClient()
		{
			_concurrencySemaphore = new SemaphoreSlim(10); // Adjust the concurrency limit as needed
			_remainingRequests = 60; // Initial value, adjust according to Reddit API rate limit
			_rateLimitResetTime = 600; // Initial value
		}

		public async Task FetchAsync(string subredditName)
		{
			if (_authModel == null || _authModel.IsExpired)
			{
				var result = await AuthenticateAsync();
				if (!result.Success)
				{
					//exception? //log?
					return;
				}
				_authModel = result.Result!;
			}

			var after = string.Empty;
			var desiredRate = 5;

			try
			{
				using (var httpClient = new HttpClient())
				{
					httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _authModel.AccessToken);
					httpClient.DefaultRequestHeaders.Add("User-Agent", UserAgent);

					while (after != null)
					{
						await WaitIfNeededAsync();

						var url = $"{RedditBaseUrl}/r/{subredditName}/new?limit={desiredRate}&after={after}"; // Adjust limit as needed
						var response = await httpClient.GetAsync(url);
						response.EnsureSuccessStatusCode(); // Check for successful response (200 OK)

						// Extract rate limit data from headers
						_remainingRequests = (int)float.Parse(response.Headers.GetValues("X-Ratelimit-Remaining").FirstOrDefault());
						_rateLimitResetTime = Convert.ToInt32(response.Headers.GetValues("X-Ratelimit-Reset").FirstOrDefault());

						var responseJsonString = await response.Content.ReadAsStringAsync();
						var postData = JsonConvert.DeserializeObject<PostContainer>(responseJsonString); // Assuming a Post class exists
						_processed += postData.Data.Children.Count;

						var eventArgs = new RedditDataReceivedEventArgs();
						foreach (var post in postData.Data.Children)
						{
							eventArgs.Posts.Add(post.Data);
						}
						OnThresholdReached(eventArgs);

						var delay = TimeSpan.FromSeconds(_rateLimitResetTime / (_remainingRequests + 1.0f));
						//Console.WriteLine($" requests remaining: {_remainingRequests} | time to reset: {_rateLimitResetTime} | delay: {delay}");

						// Wait before next request to avoid exceeding rate limit
						await Task.Delay(delay); // Convert delay to milliseconds
						
						if (!string.IsNullOrWhiteSpace(postData.Data.after))
						{
							after = postData.Data.after;
						}
						else
						{
							after = null;
						}
					}
				}
			}
			finally
			{
				_concurrencySemaphore.Release(); // Release semaphore slot
			}
		}

		public async Task<TResponse> MakeRequestAsync<TResponse>(HttpRequestMessage requestMessage)
		{
			await WaitIfNeededAsync();

			await _concurrencySemaphore.WaitAsync(); // Acquire semaphore slot
			try
			{
				using (var httpClient =  new HttpClient()) 
				{
					var response = await httpClient.SendAsync(requestMessage);
					response.EnsureSuccessStatusCode();


					throw new Exception(); // await response.Content.ReadAsStringAsync() as TResponse;
				}
				
			}
			finally
			{
				_concurrencySemaphore.Release(); // Release semaphore slot
			}
		}

		private async Task WaitIfNeededAsync()
		{
			var resetDateTime = DateTimeOffset.FromUnixTimeSeconds(_rateLimitResetTime).UtcDateTime;
			// Wait until the rate limit resets if necessary
			while (_remainingRequests <= 0 && DateTime.UtcNow < resetDateTime)
			{
				var delay = resetDateTime - DateTime.UtcNow;
				await Task.Delay(delay);
			}
		}

		public async Task ExecuteRequestsConcurrently(List<HttpRequestMessage> requestMessages)
		{
			var tasks = new List<Task>();
			foreach (var requestMessage in requestMessages)
			{
				tasks.Add(MakeRequestAsync<object>(requestMessage)); // Adjust the response type as needed
			}
			await Task.WhenAll(tasks);
		}

		private async Task<HttpResponseResult<AuthModel>> AuthenticateAsync()
		{
			var clientId = "IhxkX0NUrR0JKnMzRsfwnQ";
			var clientSecret = "AwQtGW4bucQRJmtxgL1-kNwyKM9S0A";

			using (var client = new HttpClient())
			{
				var formData = new Dictionary<string, string>
				{
					{ "grant_type", "password" },
					{ "username", "unscrypted" },
					{ "password", "Rll4ever" }
				};
				var content = new FormUrlEncodedContent(formData);

				client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
					"Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes($"{clientId}:{clientSecret}")));

				client.DefaultRequestHeaders.Add("User-Agent", "RedditReports/0.0.1");

				var response = await client.PostAsync("https://www.reddit.com/api/v1/access_token", content);

				if (!response.IsSuccessStatusCode)
				{
					return new HttpResponseResult<AuthModel>
					{
						ResponseStatusCode = (int)response.StatusCode,
					};
				}

				var responseContent = await response.Content.ReadAsStringAsync();
				var authModel = JsonConvert.DeserializeObject<AuthModel>(responseContent);

				return new HttpResponseResult<AuthModel>
				{
					ResponseStatusCode = (int)response.StatusCode,
					Result = authModel
				};
			};
		}

		private void OnThresholdReached(RedditDataReceivedEventArgs e)
		{
			if (RedditDataReceived != null)
			{
				RedditDataReceived(this, e);
			}
		}
	}
}
