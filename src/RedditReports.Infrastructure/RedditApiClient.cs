using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using RedditReports.Application;
using RedditReports.Application.Abstractions;
using RedditReports.Application.DTOs;
using RedditReports.Application.DTOs.Posts;
using RedditReports.Domain.Results;

namespace RedditReports.Infrastructure
{
	public class RedditApiClient : IRedditApiClient
	{
		private readonly ILogger<RedditApiClient> _logger;
		private readonly IRedditReporterServiceSettings _config;
		
		private int _remainingRequests;
		private int _rateLimitResetTime;

		private AuthModel? _authModel;
		private readonly SemaphoreSlim _concurrencySemaphore;

		public event EventHandler<RedditDataReceivedEventArgs> RedditDataReceived;

		public RedditApiClient(ILogger<RedditApiClient> logger, IRedditReporterServiceSettings config)
		{
			_logger = logger;
			_config = config;
			_concurrencySemaphore = new SemaphoreSlim(1); // Adjust the concurrency limit as needed
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
			}

			var after = string.Empty;

			try
			{
				using (var httpClient = new HttpClient())
				{
					httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _authModel.AccessToken);
					httpClient.DefaultRequestHeaders.Add("User-Agent", _config.UserAgent);

					while (after != null)
					{
						await _concurrencySemaphore.WaitAsync();

						var url = $"{_config.RedditBaseUri}/r/{subredditName}/new?limit={_config.NumPostsPerRequest}&after={after}";
						var response = await httpClient.GetAsync(url);
						response.EnsureSuccessStatusCode(); // Check for successful response (200 OK)

						// Extract rate limit data from headers
						_remainingRequests = (int)float.Parse(response.Headers.GetValues("X-Ratelimit-Remaining").FirstOrDefault());
						_rateLimitResetTime = Convert.ToInt32(response.Headers.GetValues("X-Ratelimit-Reset").FirstOrDefault());

						var responseJsonString = await response.Content.ReadAsStringAsync();
						var postData = JsonConvert.DeserializeObject<PostContainer>(responseJsonString); // Assuming a Post class exists
						after = postData?.Data.after ?? null;
						
						var eventArgs = new RedditDataReceivedEventArgs()
						{
							AdditionalPostsAvailable = after != null,
							SubredditName = subredditName
						};
						foreach (var post in postData.Data.Children)
						{
							eventArgs.Posts.Add(post.Data);
						}
						OnThresholdReached(eventArgs);
					
						_concurrencySemaphore.Release(); // Release semaphore slot

						var delay = TimeSpan.FromSeconds((float)_rateLimitResetTime / _remainingRequests); //CalculateWaitTime(_rateLimitUsed, _remainingRequests, _rateLimitResetTime, 1);
						_logger.LogInformation($"subreddit: {subredditName} | requests remaining: {_remainingRequests} | time to reset: {_rateLimitResetTime} | delay: {delay}");
						await Task.Delay(delay);
					}
				}
			}
			catch (Exception ex)
			{
				//await DecreaseDispatchedRequestsAsync();
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
				using (var httpClient = new HttpClient())
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

		public float CalculateWaitTime(int rateLimitUsed, int rateLimitRemaining, int rateLimitReset, int initialWaitTime)
		{
			int currentTime = (int)DateTimeOffset.UtcNow.ToUnixTimeSeconds();

			// Calculate the time until the rate limit resets (in seconds)
			int resetTimeInSeconds = rateLimitReset - currentTime;

			// If the reset time is in the past, consider it as 0
			resetTimeInSeconds = Math.Max(resetTimeInSeconds, 0);

			// Calculate the remaining requests per second until the rate limit resets
			float requestsPerSecond = rateLimitRemaining / (float)resetTimeInSeconds;

			// If there are enough remaining requests to stay under the rate limit
			if (requestsPerSecond > 1.0)
			{
				// Calculate the time interval between each request to evenly distribute them
				float timeBetweenRequests = 1.0f / requestsPerSecond;

				// Convert the time interval to milliseconds and add a small buffer
				int waitTime = (int)(timeBetweenRequests * 1000) + 100; // Adding a 100ms buffer

				// Return the calculated wait time
				return waitTime;
			}
			else
			{
				// If remaining requests are too low, wait until the rate limit resets
				return resetTimeInSeconds * 1000; // Convert seconds to milliseconds
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

		public async Task<HttpResponseResult> AuthenticateAsync()
		{
			var sb = new StringBuilder(_config.AuthenticationUriTemplate);
			var replacements = new List<(string key, string value)>
			{
			  ("clientId", _config.ClientId),
			  ("redirectUri", Uri.EscapeDataString(_config.RedirectUri))
			};
			var authUrl = replacements.Aggregate(sb, (s, t) => s.Replace($"{{{t.key}}}", t.value)).ToString();

			Console.WriteLine("Please visit the following URL to authorize the application:");
			Console.ForegroundColor = ConsoleColor.DarkYellow;
			Console.WriteLine(authUrl);
			Console.ForegroundColor = ConsoleColor.Gray;
			Console.WriteLine("After authorization, paste the redirect URL here:");

			// Wait for user to paste the redirect URL containing the authorization code
			var redirectUrl = Console.ReadLine();

			// Extract authorization code from redirect URL
			var authorizationCode = System.Web.HttpUtility.ParseQueryString(new Uri(redirectUrl).Query).Get("code");

			// Exchange authorization code for access token
			using (HttpClient client = new HttpClient())
			{
				var requestParams = new FormUrlEncodedContent(new[]
				{
				new KeyValuePair<string, string>("grant_type", "authorization_code"),
				new KeyValuePair<string, string>("code", authorizationCode),
				new KeyValuePair<string, string>("redirect_uri", _config.RedirectUri)
			});

				// Add authentication header
				client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
					"Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes($"{_config.ClientId}:{_config.ClientSecret}")));

				// Set user agent
				client.DefaultRequestHeaders.Add("User-Agent", _config.UserAgent);

				// Make request to exchange code for access token
				var tokenResponse = await client.PostAsync($"{_config.RedditBaseUri}/api/v1/access_token", requestParams);
				if (!tokenResponse.IsSuccessStatusCode)
				{
					return new HttpResponseResult
					{
						ResponseStatusCode = (int)tokenResponse.StatusCode,
					};
				}

				var responseContent = await tokenResponse.Content.ReadAsStringAsync();
				_authModel = JsonConvert.DeserializeObject<AuthModel>(responseContent);
				
				return new HttpResponseResult
				{
					ResponseStatusCode = (int)tokenResponse.StatusCode,
				};
			}
		}

		//private async Task<HttpResponseResult<AuthModel>> AuthenticateAsync()
		//{
		//	var clientId = "IhxkX0NUrR0JKnMzRsfwnQ";
		//	var clientSecret = "AwQtGW4bucQRJmtxgL1-kNwyKM9S0A";

		//	using (var client = new HttpClient())
		//	{
		//		var formData = new Dictionary<string, string>
		//		{
		//			{ "grant_type", "password" },
		//			{ "username", "unscrypted" },
		//			{ "password", "Rll4ever" }
		//		};
		//		var content = new FormUrlEncodedContent(formData);

		//		client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(
		//			"Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes($"{clientId}:{clientSecret}")));

		//		client.DefaultRequestHeaders.Add("User-Agent", "RedditReports/0.0.1");

		//		var response = await client.PostAsync("https://www.reddit.com/api/v1/access_token", content);

		//		if (!response.IsSuccessStatusCode)
		//		{
		//			return new HttpResponseResult<AuthModel>
		//			{
		//				ResponseStatusCode = (int)response.StatusCode,
		//			};
		//		}

		//		var responseContent = await response.Content.ReadAsStringAsync();
		//		var authModel = JsonConvert.DeserializeObject<AuthModel>(responseContent);

		//		return new HttpResponseResult<AuthModel>
		//		{
		//			ResponseStatusCode = (int)response.StatusCode,
		//			Result = authModel
		//		};
		//	};
		//}

		private void OnThresholdReached(RedditDataReceivedEventArgs e)
		{
			if (RedditDataReceived != null)
			{
				RedditDataReceived(this, e);
			}
		}
	}
}
