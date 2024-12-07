namespace hackwknd_api.Models.DTO;

public class GenerateQuestionRequest
{
    public string questionAmount { get; set; }
    public string type { get; set; }
    public string noteid { get; set; }
    public List<QuestionDto>? answers{ get; set; }
    public List<string> tags { get; set; }
    public string chatid { get; set; }
}