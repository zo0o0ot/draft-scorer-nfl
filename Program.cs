using CsvHelper;
using HtmlAgilityPack;
using Spectre.Console;
using System.Text.RegularExpressions;
using SharpConfig;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

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
dynamic stuff = JsonConvert.DeserializeObject(jsonDraft);
JObject draft = null;
if(draftExists && draftComplete)
{
    AnsiConsole.MarkupLine(":abacus: The draft is complete and we have the draft JSON! No need to load :abacus:");
    //stuff = JsonConvert.DeserializeObject(File.ReadAllText($"actual-draft{Path.DirectorySeparatorChar}{draftYear}{Path.DirectorySeparatorChar}{draftYear}Draft.json"));
    jsonDraft = File.ReadAllText($"actual-draft{Path.DirectorySeparatorChar}{draftYear}{Path.DirectorySeparatorChar}{draftYear}Draft.json");
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
draft = JObject.Parse(jsonDraft);
AnsiConsole.MarkupLine(":abacus: Scoring the draft... :abacus:");

//var picks = stuff.rounds.picks;
string startDate = draft["draft"]["end_date"].ToString();
List<JArray> draftRounds = new List<JArray>();
JArray Round1Picks = (JArray)draft["rounds"][0]["picks"];
JArray Round2Picks = (JArray)draft["rounds"][1]["picks"];
JArray Round3Picks = (JArray)draft["rounds"][2]["picks"];
JArray Round4Picks = (JArray)draft["rounds"][3]["picks"];
JArray Round5Picks = (JArray)draft["rounds"][4]["picks"];
JArray Round6Picks = (JArray)draft["rounds"][5]["picks"];
JArray Round7Picks = (JArray)draft["rounds"][6]["picks"];

draftRounds.Add(Round1Picks);
draftRounds.Add(Round2Picks);
draftRounds.Add(Round3Picks);
draftRounds.Add(Round4Picks);
draftRounds.Add(Round5Picks);
draftRounds.Add(Round6Picks);
draftRounds.Add(Round7Picks);

// Now that we have the draft picks, we can make them into ActualDraftPick objects.

//var r1Children = Round1Picks

//Get picks from from Round1Picks and get values needed to create ActualDraftPick objects
List<ActualDraftPick> actualDraftPicks = new List<ActualDraftPick>();

var picksTest =
    from p in Round1Picks
    select new {
        Pick = (int?)p["overall"],
        Round = 1,
        PlayerName = (string?)p["prospect"]["name"],
        School = (string?)p["prospect"]["team_name"],
        Traded = (bool?)p["traded"]
    };
foreach (var pick in picksTest)
{
    actualDraftPicks.Add(new ActualDraftPick()
    {
        Pick = pick.Pick,
        Round = pick.Round,
        Player = pick.PlayerName,
        School = pick.School,
        Traded = pick.Traded ?? false
    });
}


// Given ActualDraftPick objects, we can score them.



var asdf = "";


