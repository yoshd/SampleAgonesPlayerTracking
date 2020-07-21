using System;
using System.Threading;
using System.Threading.Tasks;
using Agones;
using Grpc.Core;

namespace SampleAgonesPlayerTracking
{
    public class AgonesSystem
    {
        private readonly AgonesSDK _agonesSdk = new AgonesSDK();
        private Task _task;
        private Task _checkTask;
        private CancellationTokenSource _cts = new CancellationTokenSource();

        public void Start()
        {
            var ct = _cts.Token;
            _task = RunAsync(ct);
        }

        /// <summary>
        /// gRPC client is thread-safe
        /// </summary>
        public async Task PlayerConnect(string id)
        {
            await _agonesSdk.Alpha().PlayerConnectAsync(id);
            // experimental
            if (await _agonesSdk.Alpha().IsPlayerConnectedAsync(id))
            {
                Console.WriteLine($"{id} is connected");
            }
        }

        /// <summary>
        /// gRPC client is thread-safe
        /// </summary>
        public async Task PlayerDisconnect(string id)
        {
            await _agonesSdk.Alpha().PlayerDisconnectAsync(id);
            // experimental
            if (!await _agonesSdk.Alpha().IsPlayerConnectedAsync(id))
            {
                Console.WriteLine($"{id} is disconnected");
            }
        }

        public async Task StopAsync()
        {
            _cts.Cancel();
            try
            {
                if (_task == null)
                {
                    return;
                }

                await _task;
            }
            catch (OperationCanceledException)
            {
                Console.WriteLine("Canceled");
            }
            finally
            {
                _cts.Dispose();
                _cts = null;
            }
        }

        private async Task RunAsync(CancellationToken ct)
        {
            try
            {
                if (!await _agonesSdk.ConnectAsync().ConfigureAwait(false))
                {
                    Console.WriteLine("Unable to connect to Agones SDK");
                    System.Environment.Exit(1);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                System.Environment.Exit(1);
            }

            var result = await _agonesSdk.ReadyAsync().ConfigureAwait(false);
            if (result.StatusCode != StatusCode.OK)
            {
                Console.WriteLine("Unable to set status to Ready");
                System.Environment.Exit(1);
            }

            _agonesSdk.WatchGameServer((server =>
           {
               switch (server.Status.State)
               {
                   case "Allocated":
                       Console.WriteLine("Allocated");
                       if (_checkTask != null && !_checkTask.IsCompleted)
                       {
                           return;
                       }
                       _checkTask = RunCheck(ct);
                       break;
                   case "Ready":
                       Console.WriteLine("Ready");
                       break;
                   default:
                       break;
               }
           }));

            await _agonesSdk.Alpha().SetPlayerCapacityAsync(100);
            var capacity = await _agonesSdk.Alpha().GetPlayerCapacityAsync();
            Console.WriteLine($"capacity: {capacity}");

            // run health check
            while (true)
            {
                if (ct.IsCancellationRequested)
                {
                    throw new OperationCanceledException();
                }
                await Task.Delay(1000, ct).ConfigureAwait(false);
                var status = await _agonesSdk.HealthAsync().ConfigureAwait(false);
                if (status.StatusCode != StatusCode.OK)
                {
                    Console.WriteLine("Health check failed");
                }
            }
        }

        private async Task RunCheck(CancellationToken ct)
        {
            while (true)
            {
                if (ct.IsCancellationRequested)
                {
                    throw new OperationCanceledException();
                }

                await Task.Delay(15000, ct).ConfigureAwait(false);
                var playerCount = await _agonesSdk.Alpha().GetPlayerCountAsync();
                if (playerCount <= 0)
                {
                    await _agonesSdk.ShutDownAsync();
                    return;
                }
                // experimental
                var playerIds = await _agonesSdk.Alpha().GetConnectedPlayersAsync();
                Console.WriteLine($"Number of players: {playerIds.Count}");
            }
        }
    }
}
