using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
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
                            for (var i = 0; i < 10; i++)
                            {
                                Thread.Sleep(1000);
                                await Send(socket, "Message from server");
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
                        var received = await Receive(socket);
                        WebSocketReceiveResult result = received.result;
                        string text = received.text;
                        while (!result.CloseStatus.HasValue)
                        {
                            await Send(socket, "Server echo back: " +  text);
                            received = await Receive(socket);
                            result = received.result;
                            text = received.text;
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
        private async Task<dynamic> Receive(WebSocket socket)
        {
            byte[] buffer = new byte[1 << 12];
            var result = await socket.ReceiveAsync(buffer, CancellationToken.None);
            var text = Encoding.UTF8.GetString(buffer);
            Logger.LogInformation($"Recieved: {text}");
            return new { result, text };
        }

        private async Task Send(WebSocket socket, string message)
        {
            byte[] buffer = new byte[1 << 12];
            var bytes = Encoding.UTF8.GetBytes(message);
            await socket.SendAsync(bytes, WebSocketMessageType.Text, true, CancellationToken.None);
            Logger.LogInformation($"Sended: {message}");
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
