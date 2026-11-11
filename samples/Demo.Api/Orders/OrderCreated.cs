using CustomMediator;

namespace Demo.Api.Orders;

// Notification: published after an order is stored. One event, several independent handlers
// (see SendConfirmationEmail and UpdateAnalytics).
public record OrderCreated(Guid OrderId, string Customer) : INotification;
