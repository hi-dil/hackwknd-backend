namespace hackwknd_api.Models.ExtData;

public class NoteExtdata
{
    public string title { get; set; }
    public List<string> tags { get; set; }
    public bool isPublic { get; set; }
    public bool requestForPublic { get; set; }
}