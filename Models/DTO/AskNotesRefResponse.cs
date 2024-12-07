namespace hackwknd_api.Models.DTO;

public class AskNotesRefResponse
{
    public string subject { get; set; }
    public string topicResult { get; set; }
    public List<TrackedNotes> trackedNotes { get; set; }
}

public class TrackedNotes
{
    public string title { get; set; }
    public string publishedBy { get; set; }
    public string content { get; set; }
}