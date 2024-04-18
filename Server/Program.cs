using Microsoft.AspNetCore.Identity;
using Newtonsoft.Json;
using System;
using System.Net;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace Server
{
    class Program
    {
        static Dictionary<string, User> users = new Dictionary<string, User>();
        static Queue<User> quickQueue = new Queue<User>();
        static Queue<User> rankedQueue = new Queue<User>();
        static async Task Main(string[] args)
        {

            var listener = new HttpListener();
            listener.Prefixes.Add("http://localhost:8080/");
            listener.Start();
            Console.WriteLine("Server started. Listening for connections...");

            while (true)
            {
                var context = await listener.GetContextAsync();
                var request = context.Request;
                var response = context.Response;
                var path = request.Url.AbsolutePath;

                switch (path)
                {
                    case "/create-account":
                        await CreateAccount(request, response);
                        break;
                    case "/login":
                        await Login(request, response);
                        break;
                    case "/get-account-info":
                        await GetAccountInfo(request, response);
                        break;
                    case "/logout":
                        await Logout(request, response);
                        break;
                    default:
                        response.StatusCode = (int)HttpStatusCode.NotFound;
                        break;

                }
            }
        }

        static async Task CreateAccount(HttpListenerRequest request, HttpListenerResponse response)
        {
            try
            {
                //Read user data from request body
                var body = await ReadRequestBodyAsync(request.InputStream);
                // Assuming formet: username,password,region,mmr

                var data = JsonConvert.DeserializeObject<Dictionary<string, object>>(body);

                if (data == null || data.Count != 4)
                {
                    response.StatusCode = (int)HttpStatusCode.BadRequest;
                    return;
                }

                var username = data["username"].ToString();
                var password = data["password"].ToString();
                var region = data["region"].ToString();
                var mmrObject = data["mmr"];
                int mmr;

                if (mmrObject is int)
                {
                    mmr = (int)mmrObject;
                }
                else if (int.TryParse(mmrObject.ToString(), out mmr))
                {
                    Console.WriteLine("MMR parsed successfully: {0}", mmr);
                }
                else
                {
                    Console.WriteLine("Inavlid MMR value: {0}", mmrObject);
                    mmr = 0;
                }

                //Check is user already exists
                if (users.ContainsKey(username))
                {
                    response.StatusCode = (int)HttpStatusCode.Conflict;
                    return;
                }

                //Security improvement made, Hash password before storing
                string hashedPassword = HashPassword(password);

                //Create new user and add to dictionary
                var user = new User(username, hashedPassword, region, mmr);
                users.Add(username, user);

                Console.WriteLine(response.StatusCode = (int)HttpStatusCode.OK);
            }
            catch (Exception ex)
            {
                Console.WriteLine("An error occured whilst processing the request: {0}", ex.Message);
                response.StatusCode = (int)HttpStatusCode.InternalServerError;
            }
        }

        static async Task Login(HttpListenerRequest request, HttpListenerResponse response)
        {
            try
            {
                //Read user credentials from request body 
                var body = await ReadRequestBodyAsync(request.InputStream);
                //Assuming format: username,password
                var data = body.Split(',');

                if (data.Length != 2)
                {
                    response.StatusCode = (int)HttpStatusCode.BadRequest;
                    return;
                }

                var username = data[0];
                var password = data[1];

                //Check if user exists and credentails match
                if (users.TryGetValue(username, out var user) && VerifyPassword(password, user.Password))
                {
                    var token = GenerateToken(username);
                    response.Headers.Add("Authorization", $"Bearer {token}");
                    Console.WriteLine(response.StatusCode = (int)HttpStatusCode.OK);
                }
                else
                {
                    Console.WriteLine(response.StatusCode = (int)HttpStatusCode.Unauthorized);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("An error occured whilst processing the request: {0}", ex.Message);
                response.StatusCode = (int)HttpStatusCode.InternalServerError;
            }
        }

        static async Task GetAccountInfo(HttpListenerRequest request, HttpListenerResponse response)
        {
            try
            {
                // Read username from request headers (assuming its sent as a haeder)
                var username = request.Headers.Get("username");

                if (string.IsNullOrEmpty(username))
                {
                    Console.WriteLine(response.StatusCode = (int)HttpStatusCode.BadRequest);
                    return;
                }

                // Check if user exists
                if (users.TryGetValue(username, out var user))
                {
                    // Return user information
                    var responseData = Encoding.UTF8.GetBytes($"Username: {user.Username}, Region: {user.Region}, MMR: {user.MMR}");
                    response.OutputStream.Write(responseData, 0, responseData.Length);
                    Console.WriteLine(response.StatusCode = (int)(HttpStatusCode)HttpStatusCode.OK);
                }
                else
                {
                    Console.Write(response.StatusCode = (int)HttpStatusCode.NotFound);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("An error occured whilst processing the request: {0}", ex.Message);
                response.StatusCode = (int)HttpStatusCode.InternalServerError;

            }
        }

        static async Task Logout(HttpListenerRequest request, HttpListenerResponse response)
        {
            try
            {
                // Read username from request headers (assuming its sent as a haeder)
                var username = request.Headers.Get("username");

                // Check if user exists
                if (users.ContainsKey(username))
                {
                    response.StatusCode = (int)HttpStatusCode.OK;
                }
                else
                {
                    response.StatusCode = (int)HttpStatusCode.NotFound;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("An error occured whilst processing the request: {0}", ex.Message);
                response.StatusCode = (int)HttpStatusCode.InternalServerError;

            }
        }

        static void Matchmaking(string username1, string username2)
        {
            // Matchmaking logic for quick queue
            while (quickQueue.Count >= 2)
            {
                var player11 = quickQueue.Dequeue();
                var player2 = quickQueue.Dequeue();

                //check if both players are logged in
                if (!IsLoggedIn(player11) || !IsLoggedIn(player2))
                {
                    Console.WriteLine("both players must be logged in to participate in matchmaking");
                    continue;
                }

                //Check if players have the same region
                if (player11.Region != player2.Region)
                {
                    Console.WriteLine($"Players {player11.Username} and {player2.Username} cannot be matched due to the region mismatch.");
                    continue;
                }

                //Check MMR difference for quick match 
                if (Math.Abs(player11.MMR - player2.MMR) > 10)
                {
                    Console.WriteLine($"Players {player11.Username} and {player2.Username} cannot be matched due to MMR difference for quick match.");
                    continue;
                }
                Console.WriteLine($"Match found: {player11.Username} vs {player2.Username}");
            }

            //Matchmaking logic for ranked queue
            while (rankedQueue.Count >= 2)
            {
                var player1 = rankedQueue.Dequeue();
                var player2 = rankedQueue.Dequeue();

                //Check if both players are logged in
                if (!IsLoggedIn(player1) || !IsLoggedIn(player2))
                {
                    Console.WriteLine("Both Players must be logged in to participate in matchmaking");
                    continue;
                }

                //Check if players have the same region
                if (player1.Region != player2.Region)
                {
                    Console.WriteLine($"Players {player1.Username} and {player2.Username} cannot be matched due to region mismatch.");
                    continue;               
                }

                //Chcek MRR difference for ranked match
                int mmrDifference = Math.Abs(player1.MMR - player2.MMR);
                if (mmrDifference < 10 || mmrDifference > 25)
                {
                    Console.WriteLine($"Players {player1.Username} and {player2.Username} cannot be matched due to MMR difference for ranked match.");
                    continue;
                }
                Console.WriteLine($"Match found: {player1.Username} vs {player2.Username}");
            }
        }
        static bool IsLoggedIn(User user)
        {
            return true;
        }

        static async Task<string> ReadRequestBodyAsync(Stream inputStream)
        {
            using (var reader = new StreamReader(inputStream))
            {
                return await reader.ReadToEndAsync();
            }
        }

        static string GenerateToken(string username)
        {
            return $"{username}_token";
        }

        static string HashPassword(string password)
        {
            using (var sha256 = SHA256.Create())
            {
                var hashedBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(password));
                return Convert.ToBase64String(hashedBytes);
            }
        }

        private static bool VerifyPassword(string password, string hashedPassword)
        {
            var inputHash = HashPassword(password);
            return inputHash == hashedPassword;
        }
    }

    class User
    {
        public string Username { get; }
        public string Password { get; }
        public string Region { get; }
        public int MMR { get; }

        public User(string username, string password, string region, int mmr)
        {
            Username = username;
            Password = password;
            Region = region;
            MMR = mmr;
        }
    }


}
