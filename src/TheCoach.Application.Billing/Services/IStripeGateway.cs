namespace TheCoach.Application.Billing.Services;

public interface IStripeGateway
{
    Task<string> CreateCustomerAsync(string tenantName, CancellationToken ct = default);
    Task<string> CreateBillingPortalSessionAsync(string customerId, string returnUrl, CancellationToken ct = default);
    Task SuspendSubscriptionAsync(string subscriptionId, CancellationToken ct = default);
    Task CancelSubscriptionAsync(string subscriptionId, CancellationToken ct = default);
}
