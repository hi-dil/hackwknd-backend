namespace hackwknd_api.Models.DTO;

public class AskNotesRefRequest
{
    public string message { get; set; }
    public List<string> tags { get; set; }
    public string chatId { get; set; }
}