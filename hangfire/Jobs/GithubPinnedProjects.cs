using Hangfire.Server;
using Hangfire.Console;
using RestSharp;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json;
using System.Text.RegularExpressions;

namespace hangfire.Jobs
{
    public class GithubPinnedProjects
    {
        private static string strapiApiToken = Environment.GetEnvironmentVariable("STRAPI_TOKEN");
        private static RestClient strapiClient = new RestClient("https://tarasa24.dev/cms/api").AddDefaultHeader(KnownHeaders.Authorization, "Bearer " + strapiApiToken);
        private static string githubApiToken = Environment.GetEnvironmentVariable("GITHUB_TOKEN");
        private static RestClient githubClient = new RestClient("https://api.github.com").AddDefaultHeader(KnownHeaders.Authorization, "Bearer " + githubApiToken);
        private static RestClient rawGithubClient = new RestClient("https://raw.githubusercontent.com/Tarasa24/");

        private class Lang
        {
            public string language { get; set; }
            public string color { get; set; }
            public float ratio { get; set; }

            public Lang(string language, string color, float ratio)
            {
                this.language = language;
                this.color = color;
                this.ratio = ratio;
            }
        }
        private class Project
        {
            public string? title { get; set; }
            public string? description { get; set; }
            public int? stars { get; set; }
            public string? homepageURL { get; set; }
            public string? repoURL { get; set; }
            public string? license { get; set; }
            public int? downloads { get; set; }
            public string? imgURL { get; set; }
            public Lang[]? lang { get; set; }
            public string? readme { get; set; }
            public string? locale { get; set; }

        }

        public static async Task Run(PerformContext context)
        {
            context.WriteLine("Sending graphql request to Github API");
            var githubRequest = new RestRequest("/graphql", Method.Post).AddJsonBody(new { query = File.ReadAllText(@"Assets/GithubPinnedProjects.graphql") });
            var githubResponse = await githubClient.ExecuteAsync(githubRequest);

            var parsedJSON = JObject.Parse(githubResponse.Content);

            var repositories = parsedJSON["data"]["user"]["pinnedItems"]["nodes"];

            // Delete projects in CMS that are no longer pinned
            var titles = repositories.Select(r => (string)r["name"]);

            var toDeleteList = JObject.Parse((await strapiClient.ExecuteAsync(new RestRequest($"/projects?locale=en"))).Content)["data"]
                .Concat(JObject.Parse((await strapiClient.ExecuteAsync(new RestRequest($"/projects?locale=cs"))).Content)["data"])
                .Select(p => new { id = (int)p["id"], title = (string)p["attributes"]["title"] })
                .Where(p => !titles.Contains(p.title));

            if (toDeleteList.Count() > 0)
                context.WriteLine($"Deleting {toDeleteList.Count()} entries from CMS that are no longer pinned");

            foreach (var repo in toDeleteList)
            {
                context.SetTextColor(ConsoleTextColor.DarkRed);
                context.WriteLine($"    Deleting {repo.title} with id {repo.id}");
                var strapiResponse = await strapiClient.ExecuteAsync(new RestRequest($"/projects/{repo.id}", Method.Delete));
                if (strapiResponse.StatusCode != System.Net.HttpStatusCode.OK)
                {
                    context.SetTextColor(ConsoleTextColor.Red);
                    context.WriteLine("    Request failed");
                }
                context.ResetTextColor();
            }

            // Process pinned projects
            var numberOfRepos = (int)parsedJSON["data"]["user"]["pinnedItems"]["totalCount"];
            context.WriteLine($"Processing {numberOfRepos} repositories in total");
            var progressBar = context.WriteProgressBar();
            int repoCounter = 1;
            
            foreach (var r in repositories)
            {
                context.WriteLine("Processing " + (string)r["name"]);

                var p = new Project();

                p.title = (string)r["name"];
                p.stars = (int)r["stargazerCount"];
                p.homepageURL = (string)r["homepageUrl"];
                p.repoURL = (string)r["url"];
                p.license = r["licenseInfo"].GetType().Equals(typeof(JObject)) ? (string)r["licenseInfo"]["name"] : null;
                p.downloads = r["releases"]["nodes"].Aggregate(0, (ir, release) => ir + release["releaseAssets"]["nodes"].Aggregate(0, (ir, asset) => ir + (int)asset["downloadCount"]));
                if (p.downloads == 0) p.downloads = null;
                p.lang = r["languages"]["edges"].Select(l => new Lang((string)l["node"]["name"], (string)l["node"]["color"], (float)l["size"] / (float)r["languages"]["totalSize"])).ToArray();

                foreach (var lang in new string[] { "en", "cz" })
                {
                    p.locale = lang == "cz" ? "cs" : lang ;

                    context.SetTextColor(ConsoleTextColor.Cyan);
                    var readmeRequest = await rawGithubClient.ExecuteAsync(new RestRequest($"/{p.title}/master/README{(lang == "en" ? "" : "." + lang)}.md"));
                    var readme = readmeRequest.Content;
                    p.readme = readme;
                    if (readmeRequest.StatusCode == System.Net.HttpStatusCode.NotFound)
                    {
                        readmeRequest = await rawGithubClient.ExecuteAsync(new RestRequest($"/{p.title}/master/README.md"));
                        readme = readmeRequest.Content;
                        p.readme = null;
                    }

                    var re = new Regex(@".*src=""(.*)"">[\S\s]*\r?\n(.*)\r?\n<\/center>", RegexOptions.Multiline);
                    var g = re.Match(readme).Groups;

                    p.imgURL = g[1].Value;
                    p.description = g[2].Value;

                    var projects = JObject.Parse((await strapiClient.ExecuteAsync(new RestRequest($"/projects?locale={p.locale}&filters[title][$eq]={p.title}"))).Content)["data"].Select(p => new { id = (int)p["id"], title = (string)p["attributes"]["title"] });

                    RestRequest strapiRequest;
                   
                    var matchingProjects = projects.Where(project => project.title == p.title);
                    if (matchingProjects.Count() == 0)
                        strapiRequest = new RestRequest($"/projects", Method.Post);
                    else
                        strapiRequest = new RestRequest($"/projects/{matchingProjects.First().id}", Method.Put);
                    strapiRequest.AddJsonBody(new { data = p });
                   
                    context.WriteLine($"    {(strapiRequest.Method == Method.Post ? "Inserting" : "Updating")} CMS entry with {lang} locale");
                    
                    var strapiResponse = await strapiClient.ExecuteAsync(strapiRequest);
                    if (strapiResponse.StatusCode != System.Net.HttpStatusCode.OK) {
                        context.SetTextColor(ConsoleTextColor.Red);
                        context.WriteLine("    Request failed");
                        context.ResetTextColor();
                    }
                }
                context.ResetTextColor();
                progressBar.SetValue((100 / numberOfRepos) * repoCounter);
                repoCounter++;
            }
            progressBar.SetValue(100);
        }

    }
}
