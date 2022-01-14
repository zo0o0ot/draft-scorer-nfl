using CsvHelper;

public class FantasyDraftPick
{
    //Owner,Player,Position,Bid,ProjectedPoints
    public string Owner { get; set; }
    public string Player { get; set; }
    public string Position { get; set; }
    public int Bid { get; set; }
    public int ProjectedPoints { get; set; }
}
