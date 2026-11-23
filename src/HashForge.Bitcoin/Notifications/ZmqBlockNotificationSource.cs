using System.Runtime.CompilerServices;
using System.Threading.Channels;
using Microsoft.Extensions.Options;
using NetMQ;
using NetMQ.Sockets;

namespace HashForge.Bitcoin.Notifications;

/// <summary>
/// Subscribes to Bitcoin Core's ZeroMQ "hashblock" feed. The node pushes a message the instant it accepts
/// a block, which is far quicker than asking it repeatedly. ZeroMQ reconnects by itself if the node restarts.
/// </summary>
public sealed class ZmqBlockNotificationSource(IOptions<BitcoinNodeOptions> options) : IBlockNotificationSource
{
    public const string Topic = "hashblock";

    private const int TopicFrame = 0;
    private const int BodyFrame = 1;
    private const int MinimumFrames = 2;

    /// <summary>How often the receive loop pauses to notice a shutdown request.</summary>
    private static readonly TimeSpan ReceiveTimeout = TimeSpan.FromMilliseconds(250);

    private readonly string _url = options.Value.ZmqBlockUrl;

    public bool IsEnabled => !string.IsNullOrWhiteSpace(_url);

    public async IAsyncEnumerable<string> ListenAsync([EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (!IsEnabled)
        {
            yield break;
        }

        var hashes = Channel.CreateUnbounded<string>();

        // NetMQ sockets are blocking and tied to one thread, so they get a dedicated one.
        var pump = Task.Factory.StartNew(
            () => Pump(hashes.Writer, cancellationToken), cancellationToken, TaskCreationOptions.LongRunning, TaskScheduler.Default);

        await foreach (var hash in hashes.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            yield return hash;
        }

        await pump.ConfigureAwait(false);
    }

    private void Pump(ChannelWriter<string> hashes, CancellationToken cancellationToken)
    {
        try
        {
            using var socket = new SubscriberSocket();
            socket.Connect(_url);
            socket.Subscribe(Topic);

            var message = new NetMQMessage();
            while (!cancellationToken.IsCancellationRequested)
            {
                if (socket.TryReceiveMultipartMessage(ReceiveTimeout, ref message)
                    && message.FrameCount >= MinimumFrames
                    && message[TopicFrame].ConvertToString() == Topic)
                {
                    hashes.TryWrite(Convert.ToHexStringLower(message[BodyFrame].ToByteArray()));
                }
            }

            hashes.TryComplete();
        }
#pragma warning disable CA1031 // Any socket failure is handed to the listener, which logs it and reconnects.
        catch (Exception exception)
#pragma warning restore CA1031
        {
            hashes.TryComplete(exception);
        }
    }
}