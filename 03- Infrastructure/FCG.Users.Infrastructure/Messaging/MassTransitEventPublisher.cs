using FCG.Users.Application.Abstractions;
using MassTransit;

namespace FCG.Users.Infrastructure.Messaging;

public class MassTransitEventPublisher : IEventPublisher
{
    private readonly IPublishEndpoint _publishEndpoint;

    public MassTransitEventPublisher(IPublishEndpoint publishEndpoint)
    {
        _publishEndpoint = publishEndpoint;
    }

    public Task PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default) where TEvent : class
    {
        return _publishEndpoint.Publish(@event, cancellationToken);
    }
}
