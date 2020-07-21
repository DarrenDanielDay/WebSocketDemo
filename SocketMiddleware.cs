using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace WebSocketDemo
{
    // You may need to install the Microsoft.AspNetCore.Http.Abstractions package into your project
    public class SocketMiddleware
    {
        private readonly RequestDelegate _next;

        public SocketMiddleware(RequestDelegate next, ILogger<SocketMiddleware> logger)
        {
            _next = next;
            Logger = logger;
        }

        public ILogger<SocketMiddleware> Logger { get; }

        public async Task Invoke(HttpContext httpContext)
        {
            try
            {
                if (httpContext.WebSockets.IsWebSocketRequest)
                {
                    var socket = await httpContext.WebSockets.AcceptWebSocketAsync();
                    var consumer = new Thread(async () =>
                    {
                        try
                        {
                            byte[] buffer = System.Text.Encoding.UTF8.GetBytes("Message from consumer");
                            for (var i = 0; i < 10; i++)
                            {
                                Thread.Sleep(1000);
                                await socket.SendAsync(buffer, WebSocketMessageType.Text, true, CancellationToken.None);
                                Logger.LogInformation("Consumer Send Message");
                            }
                        }
                        catch (Exception e)
                        {
                            Logger.LogWarning("Exception: {0}", e?.Message ?? "no further infomation");
                        }
                    });
                    consumer.Start();
                    while (!socket.CloseStatus.HasValue)
                    {
                        var buffer = new byte[1024 * 4];
                        WebSocketReceiveResult result = await Recieve(socket, buffer);
                        while (!result.CloseStatus.HasValue)
                        {
                            await Send(socket, buffer, result);
                            result = await Recieve(socket, buffer);
                        }
                        await socket.CloseAsync(result.CloseStatus.Value, result.CloseStatusDescription, CancellationToken.None);
                        Logger.LogCritical("Connection closed");
                    }
                }
                else
                {
                    await _next(httpContext);
                }
            }
            catch (Exception e)
            {
                Logger.LogWarning("Exception: {0}", e?.Message ?? "no further infomation");
            }
        }

        private async Task<WebSocketReceiveResult> Recieve(WebSocket socket, byte[] buffer)
        {
            var result = await socket.ReceiveAsync(buffer, CancellationToken.None);
            Logger.LogInformation($"Recieved: {System.Text.Encoding.UTF8.GetString(buffer)}");
            return result;
        }

        private async Task Send(WebSocket socket, byte[] buffer, WebSocketReceiveResult result)
        {
            await socket.SendAsync(new ArraySegment<byte>(buffer, 0, result.Count), result.MessageType, result.EndOfMessage, CancellationToken.None);
            Logger.LogInformation($"Sended: {System.Text.Encoding.UTF8.GetString(buffer)}");
        }
    }

    // Extension method used to add the middleware to the HTTP request pipeline.
    public static class MiddlewareExtensions
    {
        public static IApplicationBuilder UseMiddleware(this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<SocketMiddleware>();
        }
    }
}
