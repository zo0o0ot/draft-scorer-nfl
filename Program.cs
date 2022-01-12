using CsvHelper;
using HtmlAgilityPack;
using Spectre.Console;
using System.Text.RegularExpressions;
using SharpConfig;

// See https://aka.ms/new-console-template for more information
Console.WriteLine("Hello, World!");

var scraperConfig = Configuration.LoadFromFile("scraper.conf");
var pageSection = scraperConfig["Pages"];
var generalSection = scraperConfig["General"];