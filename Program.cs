using CsvHelper;
using HtmlAgilityPack;
using Spectre.Console;
using System.Text.RegularExpressions;
using SharpConfig;
using Newtonsoft.Json;

// See https://aka.ms/new-console-template for more information


//Load config
var scraperConfig = Configuration.LoadFromFile("scorer.conf");
var draftSection = scraperConfig["Draft"];
var generalSection = scraperConfig["General"];

int draftYear = draftSection["DraftYear"].IntValue;
string urlPattern = draftSection["UrlPattern"].StringValue;
bool draftComplete = draftSection["DraftComplete"].BoolValue;

string apikey ="";
try
{
    var localScraperConfig = Configuration.LoadFromFile("scorer.local.conf");
    var localSection = localScraperConfig["API"];
    apikey = localSection["apikey"].StringValue;
}
catch
{
    apikey = "";
}

if (apikey == "")
{
    AnsiConsole.WriteLine("No API key found. Please add one to scorer.local.conf");
    return;
}

AnsiConsole.MarkupLine(":abacus: Loading Fantasy Draft :abacus:");
// Load from CSV named "FantasyDraft2021.csv"
var fantasyDraftPicks = new List<FantasyDraftPick>();
using (var reader = new StreamReader($"fantasy-draft{Path.DirectorySeparatorChar}{draftYear}{Path.DirectorySeparatorChar}{draftYear}FantasyDraft.csv"))
using (var csv = new CsvReader(reader, System.Globalization.CultureInfo.InvariantCulture))
{
    csv.Context.RegisterClassMap<FantasyDraftPickMap>();
    var fantasyDraftPickRecords = csv.GetRecords<FantasyDraftPick>();
    fantasyDraftPicks = fantasyDraftPickRecords.ToList();
    var foo = "";
}

bool draftExists =  File.Exists($"actual-draft{Path.DirectorySeparatorChar}{draftYear}{Path.DirectorySeparatorChar}{draftYear}Draft.json");
// Read in player and school data from CSV file.
string jsonDraft = "";

if(draftExists && draftComplete)
{
    AnsiConsole.MarkupLine(":abacus: The draft is complete and we have the draft JSON! No need to load :abacus:");
}
else
{
    //Load the draft
    using(var client = new HttpClient())
    {
        //Send HTTP request from here.
        client.DefaultRequestHeaders.Accept.Clear();


        var stringTask = client.GetStringAsync("https://api.sportradar.us/draft/nfl/trial/v1/en/2021/draft.json?api_key=nfadymz26xkk28a99c3fedye");

        jsonDraft = await stringTask;
        string fileName = $"actual-draft{Path.DirectorySeparatorChar}{draftYear}{Path.DirectorySeparatorChar}{draftYear}Draft.json"; 
        //var Draft = JsonConvert.DeserializeObject<JToken>(jsonDraft);
        File.WriteAllText(fileName, jsonDraft);

    }

}


