using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using NBXplorer.Logging;


namespace NBXplorer
{
    public class EventHostedServiceBase : IHostedService
    {
        protected virtual string ServiceName => "DefaultName";
        
        private readonly EventAggregator _EventAggregator;

        private List<IEventAggregatorSubscription> _Subscriptions;
        private CancellationTokenSource _Cts;
        public CancellationToken CancellationToken => _Cts.Token;
        public EventHostedServiceBase(EventAggregator eventAggregator)
        {
	        _EventAggregator = eventAggregator;
        }

        protected readonly Channel<object> EventsChannel = Channel.CreateUnbounded<object>();

        public virtual async Task ProcessEvents(CancellationToken cancellationToken)
        {
            while (await EventsChannel.Reader.WaitToReadAsync(cancellationToken))
            {
                if (EventsChannel.Reader.TryRead(out var evt))
                {
                    try
                    {
                        await ProcessEvent(evt, cancellationToken);
                    }
                    catch when (cancellationToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception ex)
                    {
	                    Logs.Explorer.LogError(ex, $"Unhandled exception in {this.GetType().Name}");
                    }
                }
            }
        }

        protected virtual Task ProcessEvent(object evt, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }


        protected virtual void SubscribeToEvents()
        {

        }

        protected void Subscribe<T>()
        {
            _Subscriptions.Add(_EventAggregator.Subscribe<T>(e => EventsChannel.Writer.TryWrite(e)));
        }

        protected void PushEvent(object obj)
        {
            EventsChannel.Writer.TryWrite(obj);
        }

        public virtual Task StartAsync(CancellationToken cancellationToken)
        {
	        Logs.Explorer.LogInformation($"Start EventHostedService-[{this.ServiceName}]");
	        _Subscriptions = new List<IEventAggregatorSubscription>();
            SubscribeToEvents();
            _Cts = new CancellationTokenSource();
            _ProcessingEvents = ProcessEvents(_Cts.Token);
            return Task.CompletedTask;
        }
        Task _ProcessingEvents = Task.CompletedTask;

        public virtual async Task StopAsync(CancellationToken cancellationToken)
        {
            _Subscriptions?.ForEach(subscription => subscription.Dispose());
            _Cts?.Cancel();
            try
            {
                await _ProcessingEvents;
            }
            catch (OperationCanceledException)
            { }
        }
    }
}
