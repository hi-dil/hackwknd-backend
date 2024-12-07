namespace hackwknd_api.Models.DTO;

public class InsertNoteRequest
{
    public string content { get; set; }
    public string title { get; set; }
    public List<string> tags { get; set; }
}