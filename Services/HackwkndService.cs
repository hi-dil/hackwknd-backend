using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using hackwknd_api.DB;
using hackwknd_api.Models;
using hackwknd_api.Models.DB;
using hackwknd_api.Models.DTO;
using hackwknd_api.Models.Enums;
using hackwknd_api.Models.ExtData;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using OpenAI.Chat;
using Note = hackwknd_api.Models.DB.Note;

namespace hackwknd_api.Services;

public static class HackwkndService
{
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

    public static async Task<bool> InsertNote(InsertNoteRequest req, ApplicationDbContext dbContext, User user)
    {
        var extdata = new NoteExtdata()
        {
            title = req.title,
            tags = req.tags
        };
        
        // Serialize the object to a JSON string
        var extdataString = JsonSerializer.Serialize(extdata);

        var note = new Note()
        {
            Recid = Guid.NewGuid(),
            Datacontent = req.content,
            Userrecid = user.Recid,
            Createdateutc = DateTime.UtcNow,
            Lastupdateutc = DateTime.UtcNow,
            Extdata = JsonDocument.Parse(extdataString)
        };

        dbContext.Add(note);
        await dbContext.SaveChangesAsync();

        return true;
    }

    public static async Task<List<NoteDto>> NoteList(User user, ApplicationDbContext dbContext)
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
        var noterecids = dbContext.Notesperusertags.Where(x => x.Userrecid == user.Recid && request.tags.Contains(x.Tag))
            .Select(x => x.Recid)
            .Distinct()
            .ToList();
        
        var notes = dbContext.Notes.Where(x => noterecids.Contains(x.Recid)).ToList();
        
        // append the knowledge base
        var notepreparation = string.Join("\n", notes.Select(x => x.Datacontent).ToList());
        
        // prepare the chat model
        var messages = new List<ChatMessage>
        {
            new SystemChatMessage($"You are an expert in this topics {string.Join(", ", request.tags)} . Answer questions based only on the provided knowledge base."),
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
            logs = logs
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
        
        messages.Add(new UserChatMessage($"This is the user's answer based on the generated question, can you give explain the correct answer for all question, check if the answer provided for each question is correct or not? " +
                                         $" And how many they score based on the total question generated? \\n User Answer: {JsonSerializer.Serialize(request.answers)}"));
        
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
                                      }
                                    },
                                    "required": ["question", "explanation", "isCorrect"],
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
        
        // save to db
        // structuredLogs.Add(new ChatLog(){});
        var output = JObject.Parse(completion.Content[0].Text);
        var expectedOutput = JsonSerializer.Deserialize<GenerateQuestionResponse>(output.ToString());

        var response = new GenerateQuestionResponse()
        {
            type = GenQuestionResponseType.analysis.ToString(),
            chatId = request.chatid,
            analysis = expectedOutput.analysis
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
}