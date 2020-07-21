using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace SampleAgonesPlayerTracking
{
    class Program
    {
        async static Task Main(string[] args)
        {
            Console.WriteLine("Start");
            var cts = new CancellationTokenSource();
            Console.CancelKeyPress += new ConsoleCancelEventHandler((_, e) =>
            {
                e.Cancel = true;
                Console.WriteLine("Stopping");
                cts.Cancel();
            });
            var agones = new AgonesSystem();
            agones.Start();
            var listener = new TcpListener(IPAddress.Parse("0.0.0.0"), 8000);
            listener.Start();
            var ct = cts.Token;
            var id = 0;
            while (true)
            {
                try
                {
                    var client = await listener.AcceptTcpClientAsync().ContinueWith(
                        completedTask => completedTask.GetAwaiter().GetResult(),
                        ct,
                        TaskContinuationOptions.NotOnCanceled,
                        TaskScheduler.Default).ConfigureAwait(false);
                    var _ = HandleAsync(id.ToString(), client, agones.PlayerConnect, agones.PlayerDisconnect, cts.Token);
                    id++;
                }
                catch (OperationCanceledException)
                {
                    await agones.StopAsync();
                    return;
                }
            }
        }

        private async static Task HandleAsync(string id, TcpClient client, Func<string, Task> onConnectedAsync, Func<string, Task> onDisconnectedAsync, CancellationToken ct = default)
        {
            await onConnectedAsync(id).ConfigureAwait(false);
            client.Client.SetSocketOption(SocketOptionLevel.Tcp, SocketOptionName.NoDelay, true);
            try
            {
                using (var s = client.GetStream())
                {
                    await RunEcho(s, ct).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Operation canceled");
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            finally
            {
                client.Close();
                await onDisconnectedAsync(id);
                Console.WriteLine("Disconnected");
            }
        }

        private async static Task RunEcho(NetworkStream stream, CancellationToken ct = default)
        {
            var bufSize = 1024;
            var readBuf = new byte[bufSize];
            var sendBuf = new List<byte>();
            while (true)
            {
                if (ct.IsCancellationRequested)
                {
                    throw new OperationCanceledException();
                }
                try
                {
                    var length = await stream.ReadAsync(readBuf, 0, readBuf.Length, ct).ConfigureAwait(false);
                    if (length == 0)
                    {
                        return;
                    }
                    sendBuf.AddRange(readBuf);
                    readBuf = new byte[bufSize];
                    if (stream.DataAvailable)
                    {
                        continue;
                    }
                    Console.WriteLine("Received message: " + System.Text.Encoding.UTF8.GetString(sendBuf.ToArray(), 0, sendBuf.Count));
                    await stream.WriteAsync(sendBuf.ToArray()).ConfigureAwait(false);
                    sendBuf.Clear();
                }
                catch (OperationCanceledException)
                {
                    stream.Close();
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                    stream.Close();
                    throw;
                }
            }
        }
    }
}
