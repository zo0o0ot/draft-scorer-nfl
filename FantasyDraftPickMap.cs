using CsvHelper.Configuration;
public sealed class FantasyDraftPickMap : ClassMap<FantasyDraftPick>
{
    public FantasyDraftPickMap()
    {
        Map(m => m.Owner).Name("Owner");
        Map(m => m.Player).Name("Player");
        Map(m => m.Position).Name("Position");
        Map(m => m.Bid).Name("Bid");
        Map(m => m.ProjectedPoints).Name("ProjectedPoints");
    }
}