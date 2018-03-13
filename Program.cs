using System;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;

namespace netcore_websocket_client
{
    class Program
    {
        const int bufferSize = 1024 * 4;

        static string webSocketUri = @"ws://localhost:5000/ws";

        static void Main(string[] args)
        {
            using (var cts = new CancellationTokenSource())
            {
                var token = cts.Token;

                var waiter = Connect(webSocketUri, token).ConfigureAwait(false).GetAwaiter();

                var stopKey = ConsoleKey.A;
                Console.WriteLine("press \"" + stopKey + "\" to exit.");
                while (!IsKeyPressed(stopKey))
                {
                    //loop 
                    Thread.Sleep(new TimeSpan(0, 0, 1));
                }
                cts.Cancel();
            }
            Console.WriteLine("Ending program, press any key to exit.");
            Console.ReadKey();
        }

        static async Task Connect(string uri, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();

            using (var webSocket = new ClientWebSocket())
            {
                try
                {
                    await webSocket.ConnectAsync(new Uri(uri), token);
                    await Task.WhenAll(Send(webSocket, new TimeSpan(0, 0, 1), token), Receive(webSocket, token));
                }
                catch (OperationCanceledException)
                {
                    Console.WriteLine("cancell recv and send tasks...");
                }
                catch (AggregateException ex) when (ex.InnerException is TaskCanceledException)
                {
                    //do nothing
                    Console.WriteLine("exit due to Task(s) canceled");
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine(ex);
                }
                finally
                {
                    Console.WriteLine("Closing connection...");
                    await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "client initiate disconnect", CancellationToken.None);
                }
            }
        }

        //send string periodically
        static async Task Send(ClientWebSocket webSocket, TimeSpan period, CancellationToken token)
        {
            while (webSocket.State == WebSocketState.Open)
            {
                if (token.IsCancellationRequested)
                {
                    Console.WriteLine("stop sending...");
                    token.ThrowIfCancellationRequested();
                }

                string sendStr = @"hello ClientWebSocket!";
                try
                {
                    var sendBuffer = StringToByteArray(sendStr);

                    await webSocket.SendAsync(new ArraySegment<byte>(sendBuffer), WebSocketMessageType.Text, true, CancellationToken.None);
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine(ex);
                }
                await Task.Delay(period);
            }
        }

        static async Task Receive(ClientWebSocket webSocket, CancellationToken token)
        {
            var recvBuffer = new byte[bufferSize];
            while (webSocket.State == WebSocketState.Open)
            {
                if (token.IsCancellationRequested)
                {
                    Console.WriteLine("stop receiving...");
                    token.ThrowIfCancellationRequested();
                }

                try
                {
                    var resultResult = await webSocket.ReceiveAsync(new ArraySegment<byte>(recvBuffer), CancellationToken.None);

                    if (resultResult.MessageType == WebSocketMessageType.Close)
                    {
                        Console.WriteLine("receive close connection request");
                        await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "server initiate closing", token);
                        break;
                    }

                    Console.WriteLine("Receive: " + GetReadableString(recvBuffer));
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine(ex);
                }
            }
        }

        #region Util functions

        static byte[] StringToByteArray(string input)
        {
            return System.Text.Encoding.Default.GetBytes(input);
        }

        static string GetReadableString(byte[] buffer)
        {
            var nullStart = Array.IndexOf(buffer, (byte)0);
            nullStart = (nullStart == -1) ? buffer.Length : nullStart;
            return System.Text.Encoding.Default.GetString(buffer, 0, nullStart);
        }

        static bool IsKeyPressed(ConsoleKey kbKey)
        {
            if (Console.KeyAvailable)
            {
                if (Console.ReadKey(true).Key == kbKey)
                {
                    return true;
                }
            }
            return false;
        }

        #endregion
    }
}
