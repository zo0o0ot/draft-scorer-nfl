using CsvHelper;
using HtmlAgilityPack;
using Spectre.Console;
using System.Text.RegularExpressions;
using SharpConfig;

// See https://aka.ms/new-console-template for more information
//Load config
var scraperConfig = Configuration.LoadFromFile("scorer.conf");
var draftSection = scraperConfig["Draft"];
var generalSection = scraperConfig["General"];

int draftYear = draftSection["DraftYear"].IntValue;
string urlPattern = draftSection["UrlPattern"].StringValue;

string apikey ="";
try
{
    var localScraperConfig = Configuration.LoadFromFile("scorer.local.conf");
    var localSection = scraperConfig["General"];
    apikey = localSection["apikey"].StringValue;
}
catch
{
    apikey = "";
}

AnsiConsole.MarkupLine(":abacus: :abacus::abacus::abacus::abacus::abacus:");
