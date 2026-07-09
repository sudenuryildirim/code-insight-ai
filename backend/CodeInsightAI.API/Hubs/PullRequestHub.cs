using Microsoft.AspNetCore.SignalR;

namespace CodeInsightAI.API.Hubs;

// Pure broadcast channel - clients only ever receive on this hub, they never call back into it.
// The GitHub webhook is the only thing that pushes messages through here (see GitHubWebhookController).
public class PullRequestHub : Hub
{
}
