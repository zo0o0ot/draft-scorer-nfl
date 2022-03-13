using CsvHelper;
using HtmlAgilityPack;
using Spectre.Console;
using System.Text.RegularExpressions;
using SharpConfig;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Globalization;

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
    return 0;
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
}

bool draftExists =  File.Exists($"actual-draft{Path.DirectorySeparatorChar}{draftYear}{Path.DirectorySeparatorChar}{draftYear}Draft.json");
// Read in player and school data from CSV file.
string jsonDraft = "";
//dynamic stuff = JsonConvert.DeserializeObject(jsonDraft);
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

//Get picks from from Round1Picks and get values needed to create ActualDraftPick objects
List<ActualDraftPick> actualDraftPicks = new List<ActualDraftPick>();
for (int i = 1; i <= 7; i++)
{
    var picksTest =
        from p in draftRounds[i - 1]
        select new {
            Pick = (int?)p["overall"],
            PickInRound = (int)p["number"],
            Round = i,
            PlayerName = (string?)p["prospect"]["name"],
            School = (string?)p["prospect"]["team_name"],
            Traded = (bool?)p["traded"]
        };
    foreach (var pick in picksTest)
    {
        (string State, string Conference) stateAndConference = GetStateAndConference(pick.School);

        actualDraftPicks.Add(new ActualDraftPick()
        {
            Pick = pick.Pick,
            Round = pick.Round,
            Player = pick.PlayerName,
            School = pick.School,
            Traded = pick.Traded ?? false,
            LeagifyPoints = GetLeagifyPoints(pick.Round, pick.PickInRound, pick.Traded ?? false),
            State = stateAndConference.State,
            Conference = stateAndConference.Conference

        });
    }
}

// join actualDraftPicks to fantasyDraftPicks and sum up points for each owner.
var ownerPicks = from d in actualDraftPicks
                 join f in fantasyDraftPicks on d.School equals f.Player
                 select new {
                     d.Pick,
                     d.Round,
                     d.Player,
                     d.School,
                     d.Traded,
                     d.LeagifyPoints,
                     d.State,
                     d.Conference,
                     f.Owner
                 };


// Along with points, get some stats about the draft.
var ownerPoints = ownerPicks.GroupBy(o => o.Owner).Select(o => new {
    Owner = o.Key,
    Points = o.Sum(p => p.LeagifyPoints)
});

// for ownerPicks, get the owner's name and the number of picks they made in each round
var ownerPicksByRoundAndOwner = ownerPicks.GroupBy(o => new {
    o.Round,
    o.Owner
}).Select(o => new {
    Round = o.Key.Round,
    Owner = o.Key.Owner,
    NumberOfPicks = o.Count()
});

// Given ActualDraftPick objects, we can score them.



var asdf = "";

int GetLeagifyPoints(int round, int pickInRound, bool traded)
{
    int score = 0;
    score = round switch
    {
        1 => ((Func<int>)(() => {
            score = traded ? 10 : 0;
            if (pickInRound == 1)
            {
                score += 40;
            }
            else if (pickInRound >= 2 && pickInRound <= 10)
            {
                score += 35;
            }
            else if (pickInRound >= 11 && pickInRound <= 20)
            {
                score += 30;
            }
            else if (pickInRound >= 21 && pickInRound <= 32)
            {
                score += 25;
            }
            return score;
        }))(),
        2 => ((Func<int>)(() => {
            score = traded ? 10 : 0;
            if (pickInRound >= 1 && pickInRound <= 16)
            {
                score += 20;
            }
            else if (pickInRound >= 17 && pickInRound <= 50)
            {
                score += 15;
            }
            return score;
        }))(),
        3 => score = traded ? 20 : 10,
        4 => score = traded ? 18 : 8,
        5 => score = traded ? 17 : 7,
        6 => score = traded ? 16 : 6,
        7 => score = traded ? 15 : 5,
        _ => score = 0
    };
    return score;
}

(string, string) GetStateAndConference(string school)
{
    List<SCS> schoolsAndConferences;

    using (var reader = new StreamReader($"info{Path.DirectorySeparatorChar}SchoolStatesAndConferences.csv"))
    using (var csv = new CsvReader(reader, CultureInfo.InvariantCulture))
    {
        schoolsAndConferences = csv.GetRecords<SCS>().ToList();
    }
    
    var scs = from s in schoolsAndConferences
                            where s.School == school
                            select s;

    SCS? currentSchoolConferenceState = scs.FirstOrDefault();

    // var srfd = stateResult.FirstOrDefault();
    // string sr = "";

    // if (srfd != null)
    // {
    //     sr = srfd.ToString();
    // }
    // else
    // {
    //     Console.WriteLine("Error matching school!");
    // }
    

    // if(sr.Length > 0)
    // {
    //     return sr;
    // }
    // else
    // {
    //     return "";
    // }
    if (currentSchoolConferenceState != null)
    {
        return (currentSchoolConferenceState.State, currentSchoolConferenceState.Conference);
    }
    else
    {
        return ("", "");
    }
}

return 0;
public class SCS
{
    public string School { get; set; }
    public string State { get; set; }
    public string Conference { get; set; }
}