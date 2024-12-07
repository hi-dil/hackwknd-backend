namespace hackwknd_api.Models.DTO;

public class NoteListResponse
{
    public List<NoteDto> notes { get; set; }
}

public class NoteDto
{
    public string title { get; set; }
    public List<string> tags { get; set; }
    public string content { get; set; }
    public List<AnalysisDto> pastQuiz { get; set; }
    public string noteid { get; set; }
}