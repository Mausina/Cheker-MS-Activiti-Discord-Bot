using MCQuery;
using Discord;
using Discord.WebSocket;

namespace Sample
{
    public class Program
    {
        private static string address = "147.185.221.19";
        private static int port = 41435;
        private DiscordSocketClient _client;
        private bool _isInitialized = false;
        private long onlinePlayer;

        static void Main(string[] args) => new Program().MainAsync().GetAwaiter().GetResult();

        public async Task MainAsync()
        {
            var config = new DiscordSocketConfig
            {
                GatewayIntents = GatewayIntents.Guilds | GatewayIntents.GuildMessages | GatewayIntents.GuildMembers
            };
            _client = new DiscordSocketClient(config);
            _client.Log += LogAsync;
            _client.Disconnected += DisconnectedAsync;

            string token = "MTIzODgyMjgwMzIyNzI4MzQ1Ng.GqL_dw.Mo1UgeqJ9hok-TXGvTRdvzQVjQFy6RRnhXe3dc";  
            if (string.IsNullOrEmpty(token))
            {
                Console.WriteLine("The bot token is not set properly.");
                return;
            }

            await _client.LoginAsync(TokenType.Bot, token);
            await _client.StartAsync();

            await InitializeServerConnection(); // Initial server connection attempt and setup
            Task.Run(() => UpdatePlayerCountPeriodically()); // Periodically update player count
            await Task.Delay(-1);
        }

        private async Task InitializeServerConnection()
        {
            try
            {
                MCServer server = new MCServer(address, port);
                ServerStatus status = server.Status();
                onlinePlayer = status.Players.Online;
                Console.WriteLine($"Server: {server.Address}:{server.Port} - Players Online: {status.Players.Online}/{status.Players.Max}");
                await _client.SetGameAsync($"with {onlinePlayer} players");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Initial server check failed: {ex.Message}");
                await _client.SetGameAsync("Server offline");
            }
        }

        private async Task UpdatePlayerCountPeriodically()
        {
            int retryDelay = 500;
            int maxRetryDelay = 10000;
            int offlineThreshold = 5;
            int failedAttempts = 0;
            ServerStatus status = null;  // Declare status outside of the try block to increase its scope

            while (true)
            {
                try
                {
                    MCServer server = new MCServer(address, port);
                    status = server.Status();  // Assign server status here
                    if (status != null)
                    {
                        if (onlinePlayer != status.Players.Online)
                        {
                            onlinePlayer = status.Players.Online;
                            await _client.SetGameAsync($"with {onlinePlayer} players");
                            Console.WriteLine($"Player count updated to {onlinePlayer}.");
                            failedAttempts = 0; // Reset failed attempts on successful response
                        }
                    }
                    else
                    {
                        throw new Exception("Failed to retrieve server status.");
                    }
                }
                catch (Exception ex)
                {
                    failedAttempts++;
                    Console.WriteLine($"Attempt {failedAttempts}: Server connection error: {ex.Message}");
                    if (failedAttempts >= offlineThreshold || status == null) // Use status in the conditional logic safely
                    {
                        await _client.SetGameAsync("Server offline");
                        Console.WriteLine("Server is offline.");
                        failedAttempts = 0; // Reset after reporting offline
                    }
                }

                retryDelay = Math.Min(retryDelay * 2, maxRetryDelay); // Exponential back-off
                await Task.Delay(retryDelay);
            }
        }
        


        private Task LogAsync(LogMessage log)
        {
            Console.WriteLine(log.ToString());
            return Task.CompletedTask;
        }

        private async Task DisconnectedAsync(Exception ex)
        {
            Console.WriteLine($"Bot disconnected: {ex.Message}");
            _isInitialized = false;
            await Task.Delay(5000); // Delay before attempting to reconnect
            await _client.StartAsync(); // Reconnect the bot
        }
    }
}
