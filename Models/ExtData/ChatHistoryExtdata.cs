using System.Text.Json;
using hackwknd_api.Models.DTO;

namespace hackwknd_api.Models.ExtData;

public class ChatHistoryExtdata
{
    public string type { get; set; }
    public List<ChatLog> logs { get; set; }
    public string noteRecid { get; set; }
    public AnalysisDto analysis { get; set; }
    
}