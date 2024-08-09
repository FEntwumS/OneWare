using System.Text.Json;
using System.Text.Json.Nodes;
using GitCredentialManager;
using OneWare.Essentials.Services;
using RestSharp;

namespace OneWare.SourceControl.LoginProviders;

public class GithubLoginProvider(ISettingsService settingsService, ILogger logger) : ILoginProvider
{
    public string Name => "GitHub";
    
    public string Host => "https://github.com";

    public string GenerateLink =>
        "https://github.com/settings/tokens/new?description=OneWare%20Studio%20GitHub%20integration%20plugin&scopes=repo%2Cgist%2Cread%3Aorg%2Cworkflow%2Cread%3Auser%2Cuser%3Aemail";
    
    public async Task<bool> LoginAsync(string password)
    {
        try
        {
            var client = new RestClient("https://api.github.com");
            var request = new RestRequest("/user");
            request.AddHeader("Authorization", $"Bearer {password}");
        
            var response = await client.ExecuteGetAsync(request);
            var data = JsonSerializer.Deserialize<JsonNode>(response.Content!)!;

            var username = data["login"]?.GetValue<string>();
        
            if (username != null)
            {
                settingsService.SetSettingValue(SourceControlModule.GitHubAccountNameKey, username);
            
                var store = CredentialManager.Create("oneware");
                store.AddOrUpdate("https://github.com", username, password);

                return true;
            }
        }
        catch (Exception e)
        {
            logger.Error(e.Message, e);
        }
        return false;
    }
}