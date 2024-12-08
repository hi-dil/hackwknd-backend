using System.Runtime.InteropServices.JavaScript;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using hackwknd_api.DB;
using hackwknd_api.Models;
using hackwknd_api.Models.DB;
using hackwknd_api.Models.DTO;
using hackwknd_api.Models.Enums;
using hackwknd_api.Models.ExtData;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using OpenAI.Chat;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Content;
using Note = hackwknd_api.Models.DB.Note;

namespace hackwknd_api.Services;

public static class HackwkndService
{
    private readonly static string _mediaPath = "/app/media"; // Path where the volume is mounted

    public static async Task<string> getUserToken(string email, ApplicationDbContext dbContext)
    {
        // check if user exist or not
        var user = dbContext.Users.FirstOrDefault(x => x.Name == email);

        if (user == null)
        {
            user = new User()
            {
                Name = email, Createdateutc = DateTime.UtcNow, Lastupdateutc = DateTime.UtcNow, Recid = Guid.NewGuid()
            };
            dbContext.Add(user);

            await dbContext.SaveChangesAsync();
        }

        // generate the token
        var generatedToken = GenerateToken();
        var hashedToken = HashToken(generatedToken);

        var dbtoken = new Models.DB.Session()
        {
            Generatedsessionkey = hashedToken,
            Createdateutc = DateTime.UtcNow,
            Lastupdateutc = DateTime.UtcNow,
            Userrecid = user.Recid
        };

        dbContext.Add(dbtoken);
        await dbContext.SaveChangesAsync();

        return generatedToken;
    }

    public static async Task<User?> getUserInfo(string token, ApplicationDbContext dbContext)
    {
        // hash the token
        var hashedToken = HashToken(token);

        // compare the value
        var session = dbContext.Sessions.FirstOrDefault(x => x.Generatedsessionkey == hashedToken);
        if (session == null)
        {
            return null;
        }

        // get user detail
        var user = dbContext.Users.FirstOrDefault(x => x.Recid == session.Userrecid);

        if (user == null)
        {
            return null;
        }

        return user;
    }

    public static async Task<string> InsertNote(InsertNoteRequest req, ApplicationDbContext dbContext, User user,
        bool isDev)
    {
        if (!string.IsNullOrEmpty(req.fileName))
        {
            req.content = ExtractTextFromPdf(req.fileName, isDev);
        }

        var extdata = new NoteExtdata()
        {
            title = req.title,
            tags = req.tags
        };

        // Serialize the object to a JSON string
        var extdataString = JsonSerializer.Serialize(extdata);

        var content = Regex.Replace(req.content, @"\s+", " ").Trim();

        var note = new Note()
        {
            Recid = Guid.NewGuid(),
            Datacontent = content,
            Userrecid = user.Recid,
            Createdateutc = DateTime.UtcNow,
            Lastupdateutc = DateTime.UtcNow,
            Extdata = JsonDocument.Parse(extdataString)
        };

        dbContext.Add(note);
        await dbContext.SaveChangesAsync();

        return note.Recid.ToString();
    }

    public static async Task<List<NoteDto>> NoteList(User user, ApplicationDbContext dbContext)
    {
        var notes = dbContext.Notes.Where(x => x.Userrecid == user.Recid)
            .ToList();

        var notelist = new List<NoteDto>();

        foreach (var note in notes)
        {
            var pastQuiz = dbContext.Pastquizes
                .Where(x => x.noterecid == note.Recid.ToString() && x.userrecid == user.Recid).ToList();
            List<AnalysisDto> analysislist = new();

            foreach (var quiz in pastQuiz)
            {
                var analysis = JsonSerializer.Deserialize<AnalysisDto>(quiz.analysis);
                analysis.completedDate = quiz.completeddateutc.AddHours(8);
                analysislist.Add(analysis);
            }

            var extdata = JsonSerializer.Deserialize<NoteExtdata>(note.Extdata);
            var notedto = new NoteDto()
            {
                content = note.Datacontent, tags = extdata.tags, title = extdata.title, noteid = note.Recid.ToString(),
                pastQuiz = analysislist
            };
            notelist.Add(notedto);
        }

        return notelist;
    }

    public static async Task<List<string>> TagList(User user, ApplicationDbContext dbContext)
    {
        var notes = dbContext.Notes.Where(x => x.Userrecid == user.Recid)
            .ToList();

        var notelist = new List<NoteDto>();

        foreach (var note in notes)
        {
            var extdata = JsonSerializer.Deserialize<NoteExtdata>(note.Extdata);
            var notedto = new NoteDto() { content = note.Datacontent, tags = extdata.tags, title = extdata.title };
            notelist.Add(notedto);
        }

        var tags = notelist.SelectMany(x => x.tags).Distinct().ToList();

        return tags;
    }

    public static async Task<GenerateQuestionResponse> GenerateQuestion(GenerateQuestionRequest request,
        ApplicationDbContext dbContext, ChatClient chatClient, User user)
    {
        // grab knowledge base
        // var noterecids = dbContext.Notesperusertags.Where(x => x.Userrecid == user.Recid && request.tags.Contains(x.Tag))
        //     .Select(x => x.Recid)
        //     .Distinct()
        //     .ToList();
        //
        // var notes = dbContext.Notes.Where(x => noterecids.Contains(x.Recid)).ToList();
        //
        // // append the knowledge base
        // var notepreparation = string.Join("\n", notes.Select(x => x.Datacontent).ToList());

        var notepreparation = dbContext.Notes.Where(x => request.noteid == x.Recid.ToString())
            .Select(x => x.Datacontent).FirstOrDefault();

        // prepare the chat model
        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(
                $"You are an expert in this topics {string.Join(", ", request.tags)} . Answer questions based only on the provided knowledge base."),
            new AssistantChatMessage($"Knowledge Base: {notepreparation}"),
            new UserChatMessage($"Can you provide {request.questionAmount} questions based from the knowledge base?")
        };

        ChatCompletionOptions options = new()
        {
            ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                jsonSchemaFormatName: "TopicsQuestions",
                jsonSchema: BinaryData.FromBytes("""
                                                     {
                                                       "type": "object",
                                                       "properties": {
                                                         "questions": {
                                                           "type": "array",
                                                           "items": {
                                                             "type": "object",
                                                             "properties": {
                                                               "question": {
                                                                 "type": "string"
                                                               },
                                                               "questionID": {
                                                                 "type": "string"
                                                               }
                                                             },
                                                             "required": ["question", "questionID"],
                                                             "additionalProperties": false
                                                           }
                                                         }
                                                       },
                                                       "required": ["questions"],
                                                       "additionalProperties": false
                                                     }
                                                 """u8.ToArray()),
                jsonSchemaIsStrict: true)
        };

        ChatCompletion completion = await chatClient.CompleteChatAsync(messages, options);

        // save the result into db
        var logs = new List<ChatLog>()
        {
            new ChatLog()
            {
                actor = "system",
                message =
                    $"You are an expert in this topics {string.Join(", ", request.tags)} . Answer questions based only on the provided knowledge base.",
                isHidden = true
            },
            new ChatLog()
            {
                actor = "assistant",
                message = $"Knowledge Base: {notepreparation}",
                isHidden = true
            },
            new ChatLog()
            {
                actor = "user",
                message = $"Can you provide {request.questionAmount} questions based from the knowledge base?",
                isHidden = true
            },
            new ChatLog()
            {
                actor = "system",
                message = completion.Content[0].Text,
                isHidden = false
            }
        };

        var chatHistoryExt = new ChatHistoryExtdata()
        {
            type = ChatType.question.ToString(),
            logs = logs,
            noteRecid = request.noteid
        };

        var logsString = JsonSerializer.Serialize(chatHistoryExt);

        var chatLog = new Chathistory()
        {
            Recid = Guid.NewGuid(),
            Userrecid = user.Recid,
            Chathistory1 = logsString,
            Createdateutc = DateTime.UtcNow,
            Lastupdateutc = DateTime.UtcNow
        };

        dbContext.Add(chatLog);
        await dbContext.SaveChangesAsync();

        var output = JObject.Parse(completion.Content[0].Text);
        var expectedOutputString = output["questions"];
        var expectedOutput = JsonSerializer.Deserialize<List<QuestionDto>>(expectedOutputString.ToString());

        var response = new GenerateQuestionResponse()
        {
            type = GenQuestionResponseType.question.ToString(),
            chatId = chatLog.Recid.ToString(),
            question = expectedOutput
        };

        return response;
    }

    public static async Task<GenerateQuestionResponse?> AnswerQuestion(GenerateQuestionRequest request,
        ApplicationDbContext dbContext, User user, ChatClient chatClient)
    {
        // grab chat history
        var chatHistory = dbContext.Chathistories.Where(x => x.Recid.ToString() == request.chatid).FirstOrDefault();
        if (chatHistory == null)
        {
            return null;
        }

        // prepare the answer
        var structuredLogs = JsonSerializer.Deserialize<ChatHistoryExtdata>(chatHistory.Chathistory1);

        if (structuredLogs == null)
        {
            return null;
        }

        var messages = new List<ChatMessage>();
        foreach (var item in structuredLogs.logs)
        {
            switch (item.actor)
            {
                case "system":
                    messages.Add(new SystemChatMessage(item.message));
                    break;

                case "assistant":
                    messages.Add(new AssistantChatMessage(item.message));
                    break;

                case "user":
                    messages.Add(new UserChatMessage(item.message));
                    break;
            }
        }

        var content =
            $"This is the user's answer based on the generated question, can you give explain the correct answer for all question, check if the answer provided for each question is correct or not? " +
            $" And how many they score based on the total question generated? \\n User Answer: {JsonSerializer.Serialize(request.answers)}";

        messages.Add(new UserChatMessage(content));
        structuredLogs.logs.Add(new ChatLog() { actor = "user", isHidden = false, message = content });

        // ask gpt
        ChatCompletionOptions options = new()
        {
            ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                jsonSchemaFormatName: "UserAnswer",
                jsonSchema: BinaryData.FromBytes("""
                                                     {
                                                       "type": "object",
                                                       "properties": {
                                                         "analysis": {
                                                           "type": "object",
                                                           "properties": {
                                                             "questions": {
                                                               "type": "array",
                                                               "items": {
                                                                 "type": "object",
                                                                 "properties": {
                                                                   "question": {
                                                                     "type": "string"
                                                                   },
                                                                   "explanation": {
                                                                     "type": "string"
                                                                   },
                                                                   "isCorrect": {
                                                                     "type": "boolean"
                                                                   },
                                                                   "userAnswer": {
                                                                     "type": "string"
                                                                   }
                                                                 },
                                                                 "required": ["question", "explanation", "isCorrect", "userAnswer"],
                                                                 "additionalProperties": false
                                                               }
                                                             },
                                                             "score": {
                                                               "type": "object",
                                                               "properties": {
                                                                 "userScore": {
                                                                   "type": "string"
                                                                 },
                                                                 "totalQuestion": {
                                                                   "type": "string"
                                                                 }
                                                               },
                                                               "required": ["userScore", "totalQuestion"],
                                                               "additionalProperties": false
                                                             }
                                                           },
                                                           "required": ["questions", "score"],
                                                           "additionalProperties": false
                                                         }
                                                       },
                                                       "required": ["analysis"],
                                                       "additionalProperties": false
                                                     }
                                                 """u8.ToArray()),
                jsonSchemaIsStrict: true)
        };

        ChatCompletion completion = await chatClient.CompleteChatAsync(messages, options);
        var output = JObject.Parse(completion.Content[0].Text);
        var expectedOutput = JsonSerializer.Deserialize<GenerateQuestionResponse>(output.ToString());

        // save to db
        structuredLogs.logs.Add(new ChatLog()
            { actor = "system", isHidden = false, message = completion.Content[0].Text });
        structuredLogs.analysis = expectedOutput.analysis;

        chatHistory.Chathistory1 = JsonSerializer.Serialize(structuredLogs);
        chatHistory.Lastupdateutc = DateTime.UtcNow;
        dbContext.Update(chatHistory);
        await dbContext.SaveChangesAsync();

        var response = new GenerateQuestionResponse()
        {
            type = GenQuestionResponseType.analysis.ToString(),
            chatId = request.chatid,
            analysis = expectedOutput.analysis
        };

        return response;
    }

    public static async Task<AskNotesRefResponse> TrackNotes(AskNotesRefRequest request, User user,
        ApplicationDbContext dbContext, ChatClient chatClient)
    {
        // grab knowledge base
        var noterecids = dbContext.Notesperusertags.Where(x =>
                (x.Userrecid == user.Recid && request.subject.Contains(x.Tag)) ||
                (x.Ispublic == "true" && request.subject.Contains(x.Tag)))
            .Select(x => x.Recid)
            .Distinct()
            .ToList();

        var notes = dbContext.Notes.Where(x => noterecids.Contains(x.Recid)).ToList();

        // append the knowledge base
        var notepreparation = string.Join("\n", notes.Select(x => $"{x.Recid}: {x.Datacontent}").ToList());

        // prepare the chat model
        var messages = new List<ChatMessage>
        {
            new SystemChatMessage(
                $"You are an expert in this topics {string.Join(", ", request.subject)} . Answer questions based only on the provided knowledge base."),
            new AssistantChatMessage($"Knowledge Base: {notepreparation}"),
            new UserChatMessage(
                $"Based on the provided knowledge base, which notes have mention on this? And can you answer what is the related topic for the question based on the given knowledge base? Question: {request.question}")
        };

        ChatCompletionOptions options = new()
        {
            ResponseFormat = ChatResponseFormat.CreateJsonSchemaFormat(
                jsonSchemaFormatName: "TrackNotes",
                jsonSchema: BinaryData.FromBytes("""
                                                     {
                                                       "type": "object",
                                                       "properties": {
                                                         "trackedNotes": {
                                                           "type": "object",
                                                           "properties": {
                                                             "notesid": {
                                                               "type": "array",
                                                               "items": {
                                                                 "type": "string"
                                                               }
                                                             },
                                                             "relatedTopic": {
                                                                 "type": "string"
                                                             }
                                                           },
                                                           "required": ["notesid", "relatedTopic"],
                                                           "additionalProperties": false
                                                         }
                                                       },
                                                       "required": ["trackedNotes"],
                                                       "additionalProperties": false
                                                     }
                                                 """u8.ToArray()),
                jsonSchemaIsStrict: true)
        };

        ChatCompletion completion = await chatClient.CompleteChatAsync(messages, options);

        var output = JObject.Parse(completion.Content[0].Text);
        var expectedOutputString = output["trackedNotes"];
        var expectedOutput = JsonSerializer.Deserialize<TrackedNotesDto>(expectedOutputString.ToString());

        var trackedNotes = dbContext.Notes.Where(x => expectedOutput.notesid.Contains(x.Recid.ToString())).ToList()
            .Join(dbContext.Users,
                note => note.Userrecid,
                user => user.Recid,
                (note, user) => new TrackedNotes()
                {
                    content = note.Datacontent,
                    publishedBy = user.Name,
                    title = note.Extdata.RootElement.GetProperty("title").ToString(),
                    noteid = note.Recid.ToString()
                }).ToList();

        var response = new AskNotesRefResponse()
        {
            subject = request.subject,
            trackedNotes = trackedNotes,
            topicResult = expectedOutput.relatedTopic
        };

        return response;
    }


    private static string HashToken(string token)
    {
        using (var sha256 = SHA256.Create())
        {
            var hashBytes = sha256.ComputeHash(Encoding.UTF8.GetBytes(token));
            return Convert.ToBase64String(hashBytes);
        }
    }

    private static string GenerateToken(int size = 32)
    {
        using (var rng = RandomNumberGenerator.Create())
        {
            var tokenBytes = new byte[size];
            rng.GetBytes(tokenBytes);
            return Convert.ToBase64String(tokenBytes);
        }
    }

    private static string ExtractTextFromPdf(string fileName, bool isDev)
    {
        var filePath = "";

        if (isDev)
        {
            var directory = Path.Combine(AppContext.BaseDirectory, "media");
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            filePath = Path.Combine(AppContext.BaseDirectory, "media", fileName);
        }
        else
        {
            filePath = Path.Combine(_mediaPath, fileName);
        }

        StringBuilder text = new StringBuilder();
        using (PdfDocument document = PdfDocument.Open(filePath))
        {
            foreach (Page page in document.GetPages())
            {
                text.AppendLine(page.Text);
            }
        }

        // Clean up the text:
        string cleanedText = CleanText(text.ToString());
        return cleanedText;
    }

    private static string CleanText(string inputText)
    {
        // Replace newlines with a space to avoid unnecessary breaks.
        inputText = Regex.Replace(inputText, @"(\r\n|\r|\n)+", " ");

        // Replace multiple spaces with a single space
        inputText = Regex.Replace(inputText, @"\s+", " ");

        // Trim leading/trailing spaces
        inputText = inputText.Trim();

        return inputText;
    }
}