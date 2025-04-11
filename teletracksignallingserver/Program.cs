using System.Net.WebSockets;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks.Dataflow;




const int BufferSize = 1024 * 4;

List<ClientConnection> connectedClients = [];



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
    var self_connection = new ClientConnection(webSocket, "");
    connectedClients.Add(self_connection);
    
    var buffer = new byte[BufferSize];

    try
    {
        while (webSocket.State == WebSocketState.Open)
        {
            var result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), CancellationToken.None);
            //Console.WriteLine(result);
            var messageJson = Encoding.UTF8.GetString(buffer, 0, result.Count);
            //Console.WriteLine("messageJson: ",messageJson);
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            };
            var message = JsonSerializer.Deserialize<Message>(messageJson, options);
            //Console.WriteLine("message: ", message);
            //Console.WriteLine("ConnectedClients list: ",connectedClients);
            //foreach (var client in connectedClients)
            //{
            //    Console.WriteLine($"websocket: {client.WebSocket}, roomID: {client.RoomID}");
            //}

            if (message != null)
            {
                if (string.IsNullOrEmpty(self_connection.RoomID))
                {
                    self_connection.RoomID = message.Room; // This will now update the object in the list
                    
                    //Console.WriteLine($"self_connection roomID: {self_connection.RoomID}");
                }

                //Console.WriteLine($"Type: {message.Type}, Room: {message.Room}");

                // Here you would parse the signaling message (e.g., SDP, ICE) and forward it to the appropriate peer
                foreach (var connection in connectedClients)
                {
                
                    if (connection.WebSocket != webSocket && connection.WebSocket.State == WebSocketState.Open && connection.RoomID == message.Room)
                    {
                        if (result.MessageType == WebSocketMessageType.Close)
                        {
                            //connectedClients.Remove(self_connection);
                            //await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                            return;
                            //await webSocket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Closed in server by the clinet", CancellationToken.None);
                        }

                        await connection.WebSocket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(messageJson)),
                            result.MessageType,
                            result.EndOfMessage,
                            CancellationToken.None);

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
        //connectedClients.Remove(self_connection);
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

public class Message
{
    public required string Type { get; set; }
    public required string Room { get; set; }
    public string? SDP { get; set; }

}
public class ClientConnection
{
    public WebSocket WebSocket { get; set; }
    public string RoomID { get; set; }

    public ClientConnection(WebSocket webSocket, string roomID)
    {
        WebSocket = webSocket;
        RoomID = roomID;
    }
}