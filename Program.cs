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
bool updateProspects = draftSection["UpdateProspectList"].BoolValue;

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

bool prospectsExist = File.Exists($"actual-draft{Path.DirectorySeparatorChar}{draftYear}{Path.DirectorySeparatorChar}{draftYear}Prospects.json");
string jsonProspects = "";
//dynamic stuff = JsonConvert.DeserializeObject(jsonDraft);
JObject Jprospects = null;
if(prospectsExist)
{
    AnsiConsole.MarkupLine(":abacus: We have a prospect list! Checking for updates...... :abacus:");
    //stuff = JsonConvert.DeserializeObject(File.ReadAllText($"actual-draft{Path.DirectorySeparatorChar}{draftYear}{Path.DirectorySeparatorChar}{draftYear}Draft.json"));
    jsonProspects = File.ReadAllText($"actual-draft{Path.DirectorySeparatorChar}{draftYear}{Path.DirectorySeparatorChar}{draftYear}Prospects.json");
}
else
{
    AnsiConsole.MarkupLine(":abacus: We don't have a prospect list. Performing first time load...... :abacus:");
}

if (!prospectsExist || updateProspects)
{
    //Load the prospects
    using(var client = new HttpClient())
    {
        //Send HTTP request from here.
        client.DefaultRequestHeaders.Accept.Clear();
        var prospectsStringTask = client.GetStringAsync($"https://api.sportradar.us/draft/nfl/trial/v1/en/{draftYear}/prospects.json?api_key=" + apikey);

        jsonProspects = await prospectsStringTask;
        string fileName = $"actual-draft{Path.DirectorySeparatorChar}{draftYear}{Path.DirectorySeparatorChar}{draftYear}Prospects.json"; 
        File.WriteAllText(fileName, jsonProspects);
    }

    AnsiConsole.MarkupLine(":abacus: Prospect list updated. Waiting before making next API call. :abacus:");
    // If updating prospects, wait 1.5 seconds before making the next API call. API limit is one call per second.
    Thread.Sleep(1500);
    AnsiConsole.MarkupLine(":abacus: OK, let's do this! :abacus:");
}


Jprospects = JObject.Parse(jsonProspects);

JArray prospects = (JArray)Jprospects["prospects"];

List<Prospect> prospectList = new List<Prospect>();
foreach (var prospect in prospects)
{
    var newProspect = new Prospect();
    newProspect.PlayerId = (Guid)prospect["id"];
    newProspect.PlayerName = (string)prospect["name"];
    newProspect.School = (string)prospect["team"]?["market"] ?? "";
    newProspect.Position = (string)prospect["position"];
    newProspect.Conference = (string)prospect["conference"]?["name"] ?? (string)prospect["team_name"];
    newProspect.Experience = (string)prospect["experience"];
    prospectList.Add(newProspect);
}

// Create list of prospects from JArray


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
    bool draftLoaded = false;
    int tries = 0;
    while (!draftLoaded)
    {
        tries++;
        string draftTry = ":abacus: Loading draft, try " + tries + ":abacus:";
        AnsiConsole.MarkupLine(draftTry);
        try
        {
            //Load the draft
            using(var client = new HttpClient())
            {
                //Send HTTP request from here.
                client.DefaultRequestHeaders.Accept.Clear();

                var draftStringTask = await client.GetStringAsync($"https://api.sportradar.us/draft/nfl/trial/v1/en/{draftYear}/draft.json?api_key=" + apikey);

                jsonDraft = draftStringTask;
                string fileName = $"actual-draft{Path.DirectorySeparatorChar}{draftYear}{Path.DirectorySeparatorChar}{draftYear}Draft.json"; 
                //var Draft = JsonConvert.DeserializeObject<JToken>(jsonDraft);
                File.WriteAllText(fileName, jsonDraft);
                draftLoaded = true;
            }

        }
        catch
        {
            if (tries<4)
            {
                AnsiConsole.MarkupLine(":abacus: The draft is not loading. Waiting a while......:abacus:");
                Thread.Sleep(5000);
            }
            else
            {
                AnsiConsole.MarkupLine(":abacus: Too many tries. Wait a bit and try later.:abacus:");
            }
        }

    }
}
draft = JObject.Parse(jsonDraft);


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
        where p["prospect"] != null
        select new {
            Pick = (int?)p["overall"],
            PickInRound = (int)p["number"],
            Round = i,
            PlayerName = (string?)p["prospect"]["name"],
            School = (string?)p["prospect"]["team_name"],
            Traded = (bool?)p["traded"],
            PlayerId = (Guid?)Guid.Parse(p["prospect"]["id"].ToString()),
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
            Conference = stateAndConference.Conference,
            PlayerId = pick.PlayerId
        });
    }
}

var actualDraftPicksVerified = from adp in actualDraftPicks
                                join p in prospectList on adp.PlayerId equals p.PlayerId

                                select new ActualDraftPick{
                                    Pick = adp.Pick,
                                    Round =adp.Round,
                                    Player = adp.Player,
                                    School = p.School,
                                    Traded = adp.Traded,
                                    LeagifyPoints = adp.LeagifyPoints,
                                    State = adp.State,
                                    Conference = adp.Conference,
                                    Position = p.Position,
                                    Experience = p.Experience,
                                    PlayerId = p.PlayerId
                                };

//Go through the picks again and make sure if the school was updated, we get an updated State.
foreach (var pick in actualDraftPicksVerified)
{
    (string State, string Conference) stateAndConference = GetStateAndConference(pick.School);
    pick.State = stateAndConference.State;
    pick.Conference = stateAndConference.Conference;
}

// sort actualDraftPicksVerified by LeagifyPoints, grouped by School.
var draftPicksBySchool = from adp in actualDraftPicksVerified
                             orderby adp.LeagifyPoints descending
                             group adp by adp.School into adpGroup
                             select new {
                                 School = adpGroup.Key,
                                 LeagifyPoints = adpGroup.Sum(x => x.LeagifyPoints),
                                 Picks = adpGroup.ToList()
                             };

// Join draftPicksBySschool with fantasyDraftPicks by School. Ordered by LeafifyPoints.
var draftPicksBySchoolAndOwner = from dpbs in draftPicksBySchool
                                              join fdp in fantasyDraftPicks on dpbs.School equals fdp.Player
                                              select new {
                                                  School = dpbs.School,
                                                  LeagifyPoints = dpbs.LeagifyPoints,
                                                  Picks = dpbs.Picks,
                                                  Bid = fdp.Bid,
                                                  ProjectedPoints = fdp.ProjectedPoints,
                                                  Difference = dpbs.LeagifyPoints - fdp.ProjectedPoints,
                                                  Owner = fdp.Owner,
                                              };

// sort draftPicksBySchoolAndOwner by LeagifyPoints
var draftPicksBySchoolAndOwnerSortedByLeagifyPoints = draftPicksBySchoolAndOwner.OrderByDescending(o => o.LeagifyPoints).ToList();

// Create Bar Chart of draft picks by School and Owner

var schoolChart = new BarChart();
schoolChart.Label = "[red bold underline]Draft Points By School[/]";

foreach (var school in draftPicksBySchoolAndOwnerSortedByLeagifyPoints)
{
    string schoolLabel = $"{school.School} - {school.Owner}";
    schoolChart.AddItem(schoolLabel, school.LeagifyPoints, Color.Red);
}
if (draftPicksBySchoolAndOwnerSortedByLeagifyPoints.Count > 0)
{
    AnsiConsole.Write(schoolChart);
}
else
{
    AnsiConsole.Write("No school results yet. Has the draft begun?");
}
// Write results to CSV
string schoolResultsFileName  = $"leagify-result-stats{Path.DirectorySeparatorChar}{draftYear}{Path.DirectorySeparatorChar}{draftYear}DraftPointsBySchool.csv";
using (var stream = new StreamWriter(schoolResultsFileName))
using (var csv = new CsvWriter(stream, CultureInfo.InvariantCulture))
{
    csv.WriteRecords(draftPicksBySchoolAndOwnerSortedByLeagifyPoints);
}

AnsiConsole.WriteLine();

// Find results for any player in fantasyDraftPicks that is not in draftPicksBySchoolAndOwner.
var flops = from fdp in fantasyDraftPicks
              join dpbs in draftPicksBySchoolAndOwner on fdp.Player equals dpbs.School into dpbsGroup
              from dpbs in dpbsGroup.DefaultIfEmpty()
              where dpbs == null
              select new {
                  Player = fdp.Player,
                  Bid = fdp.Bid,
                  ProjectedPoints = fdp.ProjectedPoints,
                  Difference = fdp.ProjectedPoints - 0,
                  Owner = fdp.Owner,
              };

if (flops.Count() > 0)
{
    var flopSchools = new BarChart();
    flopSchools.Label = "[aqua bold underline]Drafted Schools with NO POINTS[/]";

    foreach (var school in flops)
    {
        string flopLabel = $"{school.Player} - {school.Owner}";
        flopSchools.AddItem(flopLabel, school.ProjectedPoints, Color.Aqua);
    }
    // Write results to CSV
    string flopsFileName  = $"leagify-result-stats{Path.DirectorySeparatorChar}{draftYear}{Path.DirectorySeparatorChar}{draftYear}Flops.csv";
    using (var stream = new StreamWriter(flopsFileName))
    using (var csv = new CsvWriter(stream, CultureInfo.InvariantCulture))
    {
        csv.WriteRecords(flops);
    }
    AnsiConsole.Write(flopSchools);
}
else
{
    AnsiConsole.Write(new Markup("[green]No flop schools! Every school we drafted had players drafted![/]\n"));
}
AnsiConsole.WriteLine();

var draftPicksBySchoolAndOwnerSortedByDifference = draftPicksBySchoolAndOwner.OrderByDescending(o => o.Difference).ToList();

var differenceTable = new Spectre.Console.Table();
differenceTable.Border(TableBorder.Double).BorderColor(ConsoleColor.DarkMagenta);
differenceTable.AddColumn("School");
differenceTable.AddColumn("Owner");
differenceTable.AddColumn("Bid");
differenceTable.AddColumn("Projected");
differenceTable.AddColumn("Actual");
differenceTable.AddColumn("Difference");
differenceTable.AddColumn("Performance Ratio");
differenceTable.AddColumn("Points per Dollar");

var schoolStatsList = new List<SchoolStats>();
foreach (var school in draftPicksBySchoolAndOwnerSortedByDifference)
{
    string[] ownerResult = {
        school.School,
        school.Owner,  
        school.Bid.ToString(),
        school.ProjectedPoints.ToString(),
        school.LeagifyPoints.ToString(),
        school.Difference.ToString(),
        ((float)school.LeagifyPoints / school.ProjectedPoints).ToString(),
        ((float)school.LeagifyPoints / school.Bid).ToString(),
        };

    //Write these results to a class so that they can be output to CSV.
    SchoolStats schoolStat = new SchoolStats{
    // public string School { get; set; }
    // public string Owner { get; set; }
    // public int Bid { get; set; }
    // public int Projected { get; set; }
    // public int Actual { get; set; }
    // public int Difference { get; set; }
    // public float PerformanceRatio { get; set; }
    // public float PointsPerDollar { get; set; }
        School = school.School,
        Owner = school.Owner,
        Bid = school.Bid,
        Projected = school.ProjectedPoints,
        Actual = school.LeagifyPoints,
        Difference = school.Difference,
        PerformanceRatio = (float)school.LeagifyPoints / school.ProjectedPoints,
        PointsPerDollar = (float)school.LeagifyPoints / school.Bid
    };

    schoolStatsList.Add(schoolStat);
    differenceTable.AddRow(ownerResult);
}

AnsiConsole.Write(differenceTable);
// Write results to CSV
string differenceTableFileName  = $"leagify-result-stats{Path.DirectorySeparatorChar}{draftYear}{Path.DirectorySeparatorChar}{draftYear}LeagifySchoolStats.csv";
using (var stream = new StreamWriter(differenceTableFileName))
using (var csv = new CsvWriter(stream, CultureInfo.InvariantCulture))
{
    csv.WriteRecords(schoolStatsList);
}
AnsiConsole.WriteLine();


// join actualDraftPicks to fantasyDraftPicks and sum up points for each owner.
var ownerPicks = from d in actualDraftPicksVerified
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
// Show players not matching an owner by seeing which of the actualDraftPicksVerified are not in ownerPicks.
var playersNotMatching = from d in actualDraftPicksVerified
                          where !ownerPicks.Any(o => o.School == d.School)
                          select d;

AnsiConsole.MarkupLine(":abacus: Players not matching up to owners... :abacus:");

// Create list of "Nobody schools" for CSV.
IDictionary<string, int> nobodySchoolDictionary = new Dictionary<string, int>();

foreach (var player in playersNotMatching)
{
    AnsiConsole.Write(new Markup($"Pick [bold yellow]{player.Pick}[/]: :american_football:[red]{player.Player}[/]:american_football: from [bold yellow]{player.School}[/] gives [lime]{player.LeagifyPoints}[/] points to :thumbs_down:[fuchsia]No One[/]:thumbs_down:\n"));
    
    //update dictionary of nobody schools
    if(!nobodySchoolDictionary.ContainsKey(player.School))
    {
        nobodySchoolDictionary[player.School] = player.LeagifyPoints;
    }
    else
    {   
        nobodySchoolDictionary[player.School] += player.LeagifyPoints;
    }

}
AnsiConsole.WriteLine();

var nobodySchoolsList = new List<NobodySchools>();
foreach (var nbs in nobodySchoolDictionary)
{
    NobodySchools school = new NobodySchools {School = nbs.Key, Points = nbs.Value};
    nobodySchoolsList.Add(school);
}
if (nobodySchoolsList.Count > 0)
{
    // Sort values by points
    List<NobodySchools> SortedListOfNobodySchools = nobodySchoolsList.OrderByDescending(s=>s.Points).ToList();
    // Write results to CSV
    string schoolsNotMatchingFileName  = $"leagify-result-stats{Path.DirectorySeparatorChar}{draftYear}{Path.DirectorySeparatorChar}{draftYear}NobodySchools.csv";
    using (var stream = new StreamWriter(schoolsNotMatchingFileName))
    using (var csv = new CsvWriter(stream, CultureInfo.InvariantCulture))
    {
        csv.WriteRecords(SortedListOfNobodySchools);
    }
}

                          


AnsiConsole.MarkupLine(":abacus: Scoring the draft... :abacus:");
AnsiConsole.WriteLine();
foreach (var pick in ownerPicks)
{
    bool traded = pick.Traded.HasValue ? pick.Traded.Value : false;
    if (traded)
        AnsiConsole.Write(new Markup($"Pick [bold yellow]{pick.Pick}[/]: :american_football:[red]{pick.Player}[/]:american_football: from [bold yellow]{pick.School}[/] gives [lime]{pick.LeagifyPoints}[/] points to :backhand_index_pointing_right:[fuchsia]{pick.Owner}[/]:backhand_index_pointing_left: [aqua]via Trade[/]\n"));
    else
        AnsiConsole.Write(new Markup($"Pick [bold yellow]{pick.Pick}[/]: :american_football:[red]{pick.Player}[/]:american_football: from [bold yellow]{pick.School}[/] gives [lime]{pick.LeagifyPoints}[/] points to :backhand_index_pointing_right:[fuchsia]{pick.Owner}[/]:backhand_index_pointing_left:\n"));    

}

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

var leagifyPointsByRoundAndOwner = ownerPicks.GroupBy(o => new { o.Owner, o.Round}).Select(o => new {
    Owner = o.Key,
    Points = o.Sum(p => p.LeagifyPoints)
}).ToList();

// change ownerPicksByRoundAndOwner to an arrayList starting with the owner name followed by the number of picks in each round
var ownerPicksByRoundAndOwnerArray = ownerPicksByRoundAndOwner.Select(o => new {
    o.Owner,
    o.Round,
    o.NumberOfPicks
}).ToList();

// create list of owners from ownerPicksByRoundAndOwnerArray
var justTheOwners = ownerPicksByRoundAndOwner.Select(o => new {
    o.Owner
}).Distinct().ToList();

// change ownerPicksByRoundAndOwnerArray to an arrayList starting with the owner name followed by the number of picks in each round
// Create string array of length 8
// string[] Ross = new string[8]; 
// string[] AJ = new string[8];
// string[] Tilo = new string[8];
// string[] Jared = new string[8];
// string[] Jawad = new string[8];

Dictionary<string, int[]> picksForTable = new Dictionary<string, int[]>();
Dictionary<string, int[]> picksForTable2 = new Dictionary<string, int[]>();
foreach (var owner in justTheOwners)
{
    picksForTable.Add(owner.Owner, new int[7]);
    picksForTable2.Add(owner.Owner, new int[7]);
}
foreach (var pickResult in ownerPicksByRoundAndOwnerArray)
{
    picksForTable[pickResult.Owner][(int)pickResult.Round - 1] = pickResult.NumberOfPicks;
    //AnsiConsole.Write(new Markup ($"[fuchsia]{pickResult.Owner}[/] has [fuchsia]{pickResult.NumberOfPicks}[/] picks in round [fuchsia]{pickResult.Round}[/]\n"));
}
foreach (var pickResult in leagifyPointsByRoundAndOwner)
{
    picksForTable2[pickResult.Owner.Owner][(int)pickResult.Owner.Round - 1] = pickResult.Points;
    //AnsiConsole.Write(new Markup ($"[fuchsia]{pickResult.Owner.Owner}[/] has [fuchsia]{pickResult.Points}[/] points in round [fuchsia]{pickResult.Owner.Round}[/]\n"));
}
// Output results of ownerPicksByRoundAndOwner to a Spectre Console table
AnsiConsole.MarkupLine(":abacus: Outputting round results to a table... :abacus:");
// Create a Spectre Console table with fully qualified class
AnsiConsole.WriteLine();

var roundPicksTable = new Spectre.Console.Table();
roundPicksTable.Border(TableBorder.Double).BorderColor(ConsoleColor.Yellow);
roundPicksTable.AddColumn("Owner");
roundPicksTable.AddColumn("Round 1");
roundPicksTable.AddColumn("Round 2");
roundPicksTable.AddColumn("Round 3");
roundPicksTable.AddColumn("Round 4");
roundPicksTable.AddColumn("Round 5");
roundPicksTable.AddColumn("Round 6");
roundPicksTable.AddColumn("Round 7");
roundPicksTable.AddColumn("Total");
foreach (var owner in picksForTable)
{
    string[] ownerResult = {owner.Key, 
        owner.Value[0].ToString(), 
        owner.Value[1].ToString(), 
        owner.Value[2].ToString(), 
        owner.Value[3].ToString(), 
        owner.Value[4].ToString(), 
        owner.Value[5].ToString(), 
        owner.Value[6].ToString(), 
        owner.Value.Sum().ToString()};

    roundPicksTable.AddRow(ownerResult);
}

AnsiConsole.Write(roundPicksTable);
AnsiConsole.WriteLine();

// Output results of ownerPicksByRoundAndOwner to a Spectre Console table
AnsiConsole.MarkupLine(":abacus: Outputting round point results to a table... :abacus:");
// Create a Spectre Console table with fully qualified class
AnsiConsole.WriteLine();

var roundPicksTable2 = new Spectre.Console.Table();
roundPicksTable2.Border(TableBorder.Double).BorderColor(ConsoleColor.Blue);
roundPicksTable2.AddColumn("Owner");
roundPicksTable2.AddColumn("Round 1");
roundPicksTable2.AddColumn("Round 2");
roundPicksTable2.AddColumn("Round 3");
roundPicksTable2.AddColumn("Round 4");
roundPicksTable2.AddColumn("Round 5");
roundPicksTable2.AddColumn("Round 6");
roundPicksTable2.AddColumn("Round 7");
roundPicksTable2.AddColumn("Total");
foreach (var owner in picksForTable2)
{
    string[] ownerResult = {owner.Key, 
        owner.Value[0].ToString(), 
        owner.Value[1].ToString(), 
        owner.Value[2].ToString(), 
        owner.Value[3].ToString(), 
        owner.Value[4].ToString(), 
        owner.Value[5].ToString(), 
        owner.Value[6].ToString(), 
        owner.Value.Sum().ToString()};

    roundPicksTable2.AddRow(ownerResult);
}

AnsiConsole.Write(roundPicksTable2);
AnsiConsole.WriteLine();

// Given ActualDraftPick objects, we can score them with a bar chart
var pointChart = new BarChart();
pointChart.Label = "[green bold underline]Draft Points[/]";

var ownerPointsList = ownerPoints.OrderByDescending(o => o.Points).ToList();
foreach (var owner in ownerPointsList)
{
    pointChart.AddItem(owner.Owner, owner.Points, Color.Green);
}
if (ownerPointsList.Count > 0)
{
    AnsiConsole.Write(pointChart);
    // Write results to CSV
    string ownerResultsFileName  = $"leagify-result-stats{Path.DirectorySeparatorChar}{draftYear}{Path.DirectorySeparatorChar}{draftYear}OwnerResults.csv";
    using (var stream = new StreamWriter(ownerResultsFileName))
    using (var csv = new CsvWriter(stream, CultureInfo.InvariantCulture))
    {
        csv.WriteRecords(ownerPointsList);
    }
}
else
{
    AnsiConsole.Write("No points yet.  Either we suck or the draft hasn't started yet.");
}
AnsiConsole.WriteLine();

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

public class SchoolStats
{

    public string School { get; set; }
    public string Owner { get; set; }
    public int Bid { get; set; }
    public int Projected { get; set; }
    public int Actual { get; set; }
    public int Difference { get; set; }
    public float PerformanceRatio { get; set; }
    public float PointsPerDollar { get; set; }

}

public class NobodySchools
{
    public string School { get; set; }
    //public int Projected { get; set; }
    public int Points { get; set; }
}