using AutoMapper;
using hackwknd_api.DB;
using hackwknd_api.Models.DTO;
using hackwknd_api.Models.Enums;
using hackwknd_api.Services;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using OpenAI.Chat;

namespace hackwknd_api.Controllers;

[ApiController]
[Route("[controller]")]
public class HackwkndController: ControllerBase
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IMapper _mapper;
    private readonly ChatClient _chatClient;

    // Inject the DbContext using the constructor
    public HackwkndController(ApplicationDbContext dbContext, IMapper mapper, ChatClient chatClient)
    {
        _dbContext = dbContext;
        _mapper = mapper;
        _chatClient = chatClient;
    }

    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest req)
    {
        var token = await  HackwkndService.getUserToken(req.email, _dbContext);
        if (string.IsNullOrEmpty(token))
        {
            return StatusCode(500, "An unexpected error occurred.");
        }

        return Ok(token);
    }

    [HttpPost("test")]
    public async Task<ActionResult<LoginResponse>> Test()
    {
        // Retrieve token from the Authorization header
        var authHeader = Request.Headers["Authorization"].ToString();

        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
        {
            return Unauthorized("Missing or invalid Authorization header.");
        }

        // Extract token (strip "Bearer ")
        var token = authHeader.Substring("Bearer ".Length);
        var user = await HackwkndService.getUserInfo(token, _dbContext);
        
        ChatCompletion completion = _chatClient.CompleteChat("Say 'this is a test.'");

        Console.WriteLine($"[ASSISTANT]: {completion.Content[0].Text}");
        return Ok(completion.Content[0].Text);
    }

    [HttpPost("noteinsert")]
    public async Task<ActionResult<InsertNoteResponse>> InsertNote([FromBody] InsertNoteRequest request)
    {
        // Retrieve token from the Authorization header
        var authHeader = Request.Headers["Authorization"].ToString();

        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
        {
            return Unauthorized("Missing or invalid Authorization header.");
        }

        // Extract token (strip "Bearer ")
        var token = authHeader.Substring("Bearer ".Length);
        var user = await HackwkndService.getUserInfo(token, _dbContext);

        var insertNote = await HackwkndService.InsertNote(request, _dbContext, user);
        return new ActionResult<InsertNoteResponse>(new InsertNoteResponse(){success = insertNote});
    }
    
    [HttpGet("notelist")]
    public async Task<ActionResult<NoteListResponse>> ListNote() {
        // Retrieve token from the Authorization header
        var authHeader = Request.Headers["Authorization"].ToString();

        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
        {
            return Unauthorized("Missing or invalid Authorization header.");
        }

        // Extract token (strip "Bearer ")
        var token = authHeader.Substring("Bearer ".Length);
        var user = await HackwkndService.getUserInfo(token, _dbContext);
        var noteList = await HackwkndService.NoteList(user, _dbContext);
        return new ActionResult<NoteListResponse>(new NoteListResponse(){notes = noteList});
    }

    [HttpGet("usertaglist")]
    public async Task<ActionResult<TagListResponse>> ListTag()
    {
        // Retrieve token from the Authorization header
        var authHeader = Request.Headers["Authorization"].ToString();

        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
        {
            return Unauthorized("Missing or invalid Authorization header.");
        }

        // Extract token (strip "Bearer ")
        var token = authHeader.Substring("Bearer ".Length);
        var user = await HackwkndService.getUserInfo(token, _dbContext);
        
        var tags = await HackwkndService.TagList(user, _dbContext);
        return new ActionResult<TagListResponse>(new TagListResponse(){tags = tags});
    }
    
    [HttpPost("generatequestion")]
    public async Task<ActionResult<GenerateQuestionResponse>> GenerateQuestion([FromBody] GenerateQuestionRequest request) {
        // Retrieve token from the Authorization header
        var authHeader = Request.Headers["Authorization"].ToString();

        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
        {
            return Unauthorized("Missing or invalid Authorization header.");
        }

        // Extract token (strip "Bearer ")
        var token = authHeader.Substring("Bearer ".Length);
        var user = await HackwkndService.getUserInfo(token, _dbContext);

        var output = new GenerateQuestionResponse();
        if (request.type == GenQuestionRequestType.request.ToString())
        {
            output = await HackwkndService.GenerateQuestion(request, _dbContext, _chatClient, user);
        }
        else if (request.type == GenQuestionRequestType.answer.ToString())
        {
            output = await HackwkndService.AnswerQuestion(request, _dbContext, user, _chatClient);

        }
        
        return Ok(output);
    }
    
    [HttpPost("asknotesref")]
    public async Task<ActionResult<GenerateQuestionResponse>> AskNotesRef([FromBody] GenerateQuestionRequest request) {
        // Retrieve token from the Authorization header
        var authHeader = Request.Headers["Authorization"].ToString();

        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer "))
        {
            return Unauthorized("Missing or invalid Authorization header.");
        }

        // Extract token (strip "Bearer ")
        var token = authHeader.Substring("Bearer ".Length);
        var user = await HackwkndService.getUserInfo(token, _dbContext);

        var output = new GenerateQuestionResponse();
        if (request.type == GenQuestionRequestType.request.ToString())
        {
            output = await HackwkndService.GenerateQuestion(request, _dbContext, _chatClient, user);
        }
        else if (request.type == GenQuestionRequestType.answer.ToString())
        {
            output = await HackwkndService.AnswerQuestion(request, _dbContext, user, _chatClient);

        }
        
        return Ok(output);
    }
}