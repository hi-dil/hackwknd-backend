namespace hackwknd_api.Models.DTO;

public class GenerateQuestionResponse
{
    public string type { get; set; }
    public List<QuestionDto> question { get; set; }
    public AnalysisDto analysis { get; set; }
    public string chatId { get; set; }
}