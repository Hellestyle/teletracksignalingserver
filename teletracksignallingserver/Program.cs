using System.Net.WebSockets;
using System.Text;

const int BufferSize = 1024 * 4;

List<WebSocket> connectedClients = new List<WebSocket>();

async Task HandleWebSocketRequest(HttpContext context)
{
    if (context.WebSockets.IsWebSocketRequest)
    {
        var webSocket = await context.WebSockets.AcceptWebSocketAsync();
        await HandleWebSocketSignaling(webSocket);
    }
    else
    {
        context.Response.StatusCode = 400;
    }
}

async Task HandleWebSocketSignaling(WebSocket webSocket)
{
    connectedClients.Add(webSocket);
    var buffer = new byte[BufferSize];

    try
    {
        while (webSocket.State == WebSocketState.Open)
        {
            var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            var message = Encoding.UTF8.GetString(buffer, 0, result.Count);

            Console.WriteLine($"Received: {message}");

            // Here you would parse the signaling message (e.g., SDP, ICE) and forward it to the appropriate peer
            foreach (var client in connectedClients)
            {
                if (client != webSocket && client.State == WebSocketState.Open)
                {
                    await client.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(message)),
                        result.MessageType,
                        result.EndOfMessage,
                        CancellationToken.None);

                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await webSocket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Closed in server by the clinet", CancellationToken.None);
                    }
                }
            }
        }
    }
    catch (Exception ex)
    {
        Console.WriteLine($"Error: {ex.Message}");
    }
    finally
    {
        connectedClients.Remove(webSocket);
        await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
    }
}


var builder = WebApplication.CreateBuilder(args);
var app = builder.Build();


app.UseWebSockets();
app.UseRouting();

app.UseEndpoints(endpoints =>
{
    endpoints.MapGet("/signal", async context =>
    {
        await HandleWebSocketRequest(context);
    });
});

app.Run();

