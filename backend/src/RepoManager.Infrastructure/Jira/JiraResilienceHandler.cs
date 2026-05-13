using System.Net;
using Polly;
using Polly.Retry;

namespace RepoManager.Infrastructure.Jira;

internal sealed class JiraResilienceHandler : DelegatingHandler
{
    private static readonly ResiliencePipeline<HttpResponseMessage> Pipeline =
        new ResiliencePipelineBuilder<HttpResponseMessage>()
            .AddRetry(new RetryStrategyOptions<HttpResponseMessage>
            {
                ShouldHandle = new PredicateBuilder<HttpResponseMessage>()
                    .Handle<HttpRequestException>()
                    .HandleResult(r => r.StatusCode == HttpStatusCode.TooManyRequests || (int)r.StatusCode >= 500),
                MaxRetryAttempts = 3,
                BackoffType = DelayBackoffType.Exponential,
                Delay = TimeSpan.FromSeconds(2),
            })
            .Build();

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) =>
        await Pipeline.ExecuteAsync(
            token => new ValueTask<HttpResponseMessage>(base.SendAsync(request, token)), ct);
}
