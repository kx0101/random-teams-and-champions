using System.Net.Http.Json;
using Microsoft.Extensions.Configuration;

class Program
{
    static readonly HttpClient client = new HttpClient();

    static IConfigurationRoot configuration = new ConfigurationBuilder()
        .SetBasePath(Directory.GetCurrentDirectory())
        .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
        .Build();

    static async Task Main(string[] args)
    {
        var teams = await GenerateTeams();

        Console.WriteLine("Enter banned champions (comma-separated) or press Enter to skip: ");
        string bannedChampionsInput = Console.ReadLine();

        if (string.IsNullOrEmpty(bannedChampionsInput))
        {
            return;
        }

        var bannedChampions = bannedChampionsInput.Split(',').Select(champion => champion.Trim()).ToList();
        var bannedChampionsString = "\nBanned Champions: " + string.Join(", ", bannedChampions);

        await SendMessageToDiscord(bannedChampionsString);

        if (bannedChampions.Any())
        {
            var updatedTeams = new Dictionary<string, List<dynamic>>();

            foreach (var team in teams)
            {
                updatedTeams[team.Key] = new List<dynamic>();

                foreach (var player in team.Value)
                {
                    if (bannedChampions.Contains(player.champion))
                    {
                        var data = await GetChampions();
                        var championsData = data["data"];
                        var champions = new List<string>();

                        foreach (var champion in championsData.EnumerateObject())
                        {
                            champions.Add(champion.Name);
                        }


                        var randomChampionIndex = new Random().Next(0, champions.Count);
                        var randomChampion = champions[randomChampionIndex];

                        updatedTeams[team.Key].Add(new { name = player.name, champion = randomChampion });
                    }
                    else
                    {
                        updatedTeams[team.Key].Add(player);
                    }
                }
            }

            var discordMessage = "\nUpdated Teams:\n";
            foreach (var team in updatedTeams)
            {
                discordMessage += $"{team.Key}: {string.Join(", ", team.Value.Select(player => $"{player.name} ({player.champion})"))}\n";
            }

            await SendMessageToDiscord(discordMessage);

            Console.WriteLine("\nUpdated Teams:\n");
            foreach (var team in updatedTeams)
            {
                Console.WriteLine($"{team.Key}: {string.Join(", ", team.Value.Select(player => $"{player.name} ({player.champion})"))}\n");
            }

        }
        else
        {
            Console.WriteLine("\nNo banned champions, teams remain unchanged.");
        }
    }

    static async Task<Dictionary<string, dynamic>> GetChampions()
    {
        try
        {
            HttpResponseMessage response = await client.GetAsync("https://ddragon.leagueoflegends.com/cdn/13.24.1/data/en_US/champion.json");
            response.EnsureSuccessStatusCode();

            var data = await response.Content.ReadFromJsonAsync<Dictionary<string, dynamic>>();

            if (data == null)
            {
                throw new Exception("api is broken");
            }

            return data;

        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
            return null;
        }
    }

    static async Task<Dictionary<string, List<dynamic>>> GenerateTeams()
    {
        var teams = new Dictionary<string, List<dynamic>>();
        var data = await GetChampions();
        var championData = data["data"];

        var names = new List<string> { "Hix", "Panos", "Liakos", "Hurricane" };
        var totalTeams = (int)Math.Ceiling((double)names.Count / 2);

        var champions = new List<string>();

        foreach (var champion in championData.EnumerateObject())
        {
            champions.Add(champion.Name);
        }

        for (int i = 0; i < totalTeams; i++)
        {
            var team = $"team{i + 1}";
            teams[team] = new List<dynamic>();

            for (int j = 0; j < 2 && names.Count > 0; j++)
            {
                var randomNameIndex = new Random().Next(0, names.Count);
                var randomChampionIndex = new Random().Next(0, champions.Count);

                var selectedName = names[randomNameIndex];
                var selectedChampion = champions[randomChampionIndex];

                names.RemoveAt(randomNameIndex);
                champions.RemoveAt(randomChampionIndex);

                teams[team].Add(new { name = selectedName, champion = selectedChampion });
            }
        }

        if (names.Count > 0)
        {
            var team = $"team{teams.Count + 1}";
            teams[team] = new List<dynamic> { new { name = names[0], champion = champions[0] } };
        }

        Console.WriteLine("Teams:\n");
        foreach (var team in teams)
        {
            Console.WriteLine($"{team.Key}: {string.Join(", ", team.Value.Select(player => $"{player.name} ({player.champion})"))}");
        }

        var discordMessage = "Teams:\n";
        foreach (var team in teams)
        {
            discordMessage += $"{team.Key}: {string.Join(", ", team.Value.Select(player => $"{player.name} ({player.champion})"))}\n";
        }

        await SendMessageToDiscord(discordMessage);

        return teams;
    }

    static async Task SendMessageToDiscord(string messageContent)
    {
        try
        {
            string discordWebhookUrl = configuration.GetSection("AppSettings")["DISCORD_WEBHOOK_URL"];

            if (string.IsNullOrWhiteSpace(discordWebhookUrl))
            {
                throw new Exception("No webhook");
            }

            var httpClient = new HttpClient();

            var payload = new
            {
                content = messageContent
            };

            var stringPayload = Newtonsoft.Json.JsonConvert.SerializeObject(payload);
            var httpContent = new StringContent(stringPayload, System.Text.Encoding.UTF8, "application/json");

            HttpResponseMessage response = await httpClient.PostAsync(discordWebhookUrl, httpContent);
            response.EnsureSuccessStatusCode();
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message);
        }
    }
}

