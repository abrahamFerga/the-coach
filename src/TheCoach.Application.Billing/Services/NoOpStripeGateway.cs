namespace TheCoach.Application.Billing.Services;

public sealed class NoOpStripeGateway : IStripeGateway
{
    public Task<string> CreateCustomerAsync(string tenantName, CancellationToken ct = default) =>
        Task.FromResult($"cus_stub_{tenantName[..8]}");

    public Task<string> CreateBillingPortalSessionAsync(string customerId, string returnUrl, CancellationToken ct = default) =>
        Task.FromResult($"https://billing.stripe.com/session/stub_{customerId}");

    public Task SuspendSubscriptionAsync(string subscriptionId, CancellationToken ct = default) =>
        Task.CompletedTask;

    public Task CancelSubscriptionAsync(string subscriptionId, CancellationToken ct = default) =>
        Task.CompletedTask;
}
