using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using RedditReports.Application;
using RedditReports.Application.Abstractions;
using RedditReports.Domain.Abstractions;
using RedditReports.Infrastructure;

namespace RedditReports.ConsoleApp
{
	internal class Program
	{
		static async Task Main(string[] args)
		{
			var builder = new ConfigurationBuilder();
			BuildConfig(builder);

			var host = Host.CreateDefaultBuilder()
				.ConfigureServices((context, services) =>
				{
					services.AddTransient<IRedditReporterService, RedditReporterService>();
					services.AddTransient<IRedditApiClient, RedditApiClient>();
					services.AddSingleton<IRedditReporterServiceSettings, RedditReporterServiceSettings>(resolver =>
						resolver.GetRequiredService<IOptions<RedditReporterServiceSettings>>().Value);
					services.Configure<RedditReporterServiceSettings>(context.Configuration
						.GetRequiredSection(nameof(RedditReporterServiceSettings)));
				})
				.Build();

			var svc = ActivatorUtilities.CreateInstance<RedditReporterService>(host.Services);
			await svc.StartAsync();
		}

		private static void BuildConfig(IConfigurationBuilder builder)
		{
			builder.SetBasePath(Directory.GetCurrentDirectory())
				.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
				.AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Production"}.json")
				.AddEnvironmentVariables();
		}
	}
}
