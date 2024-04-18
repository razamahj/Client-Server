using System.Text;

namespace Client
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var client = new HttpClient();
            client.BaseAddress = new Uri("http://localhost:8080/");

            //Example usage of HTTPClient to interact with server
            var createdAccountResponse = await CreateAccount(client,"username","password","region",1000);
            Console.WriteLine($"Create Account Status Code: {createdAccountResponse.StatusCode}");

            var loginResponse = await Login(client,"username","password");
            Console.WriteLine($"Login Status Code: {loginResponse.StatusCode}");

            if (loginResponse.StatusCode == System.Net.HttpStatusCode.OK)
            {
                var token = loginResponse.Headers.GetValues("Authorization").ToString();
                var accountInfoResponse = await GetAccountInfo(client, token ?? "defaultToken");
                Console.WriteLine($"Get Account Info Status Code: {accountInfoResponse.StatusCode}");

                if (accountInfoResponse.StatusCode == System.Net.HttpStatusCode.OK)
                {
                    var accountInfo = await accountInfoResponse.Content.ReadAsStringAsync();
                    Console.WriteLine($"Account Info: {accountInfo}");
                }

                var logoutResponse = await Logout(client, token);
                Console.WriteLine($"Logout Status Code: {logoutResponse.StatusCode}");
            }
        }

        static async Task<HttpResponseMessage> CreateAccount(HttpClient client, string username, string password, string region, int mmr)
        {
            var requestBody = $"{{\"username\": \"{username}\", \"password\": \"{password}\", \"region\": \"{region}\", \"mmr\": \"{mmr}\"}}";
            return await client.PostAsync("create-account", new StringContent(requestBody, Encoding.UTF8, "application/json"));
        }

        static async Task<HttpResponseMessage> Login(HttpClient client, string username, string password)
        {
            var requestBody = $"{username},{password}";
            return await client.PostAsync("login", new StringContent(requestBody, Encoding.UTF8, "text/plain"));
        }

        static async Task<HttpResponseMessage> GetAccountInfo(HttpClient client, string token)
        {
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Add("Authorization", token);
            return await client.GetAsync("get-account-info");
        }

        static async Task<HttpResponseMessage> Logout(HttpClient client, string token)
        {
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Add("Authorization", token);
            return await client.PostAsync("logout", new StringContent("", Encoding.UTF8, "text/plain"));
        }
    }
}