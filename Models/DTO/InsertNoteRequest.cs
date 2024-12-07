namespace hackwknd_api.Models.DTO;

public class InsertNoteRequest
{
    public string content { get; set; }
    public string title { get; set; }
    public bool isPublic { get; set; } = false;
    public List<string> tags { get; set; }
}