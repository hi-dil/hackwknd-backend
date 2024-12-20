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
public class HackwkndController : ControllerBase
{
    private readonly ApplicationDbContext _dbContext;
    private readonly IMapper _mapper;
    private readonly ChatClient _chatClient;
    private readonly string _mediaPath = "/app/media"; // Path where the volume is mounted
    private readonly IWebHostEnvironment _env;

    // Inject the DbContext using the constructor
    public HackwkndController(ApplicationDbContext dbContext, IMapper mapper, ChatClient chatClient, IWebHostEnvironment env)
    {
        _dbContext = dbContext;
        _mapper = mapper;
        _chatClient = chatClient;
        _env = env;
    }

    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login([FromBody] LoginRequest req)
    {
        var token = await HackwkndService.getUserToken(req.email, _dbContext);
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

        var insertNote = await HackwkndService.InsertNote(request, _dbContext, user, _env.IsDevelopment());
        return new ActionResult<InsertNoteResponse>(new InsertNoteResponse() { noteid = insertNote });
    }

    [HttpGet("notelist")]
    public async Task<ActionResult<NoteListResponse>> ListNote()
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
        var noteList = await HackwkndService.NoteList(user, _dbContext);
        return new ActionResult<NoteListResponse>(new NoteListResponse() { notes = noteList });
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
        return new ActionResult<TagListResponse>(new TagListResponse() { tags = tags });
    }

    [HttpPost("generatequestion")]
    public async Task<ActionResult<GenerateQuestionResponse>> GenerateQuestion(
        [FromBody] GenerateQuestionRequest request)
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

    [HttpPost("topictracking")]
    public async Task<ActionResult<AskNotesRefResponse>> AskNotesRef([FromBody] AskNotesRefRequest request)
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

        var trackNotes = await HackwkndService.TrackNotes(request, user, _dbContext, _chatClient);

        return Ok(trackNotes);
    }

    [HttpPost("upload")]
    public async Task<IActionResult> UploadFile(IFormFile file)
    {
        var filePath = "";
        if (_env.IsDevelopment())
        {
            var directory = Path.Combine(AppContext.BaseDirectory, "media");
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);

            }
            
            filePath = Path.Combine(AppContext.BaseDirectory, "media", file.FileName);
        }
        else
        {
            filePath = Path.Combine(_mediaPath, file.FileName);
        }
        
        if (file == null || file.Length == 0)
        {
            return BadRequest("No file uploaded.");
        }
        
        using (var stream = new FileStream(filePath, FileMode.Create))
        {
            await file.CopyToAsync(stream);
        }

        return Ok(new { FilePath = filePath, FileName = file.FileName });
    }

    // File download endpoint
    [HttpGet("downloadpdf/{fileName}")]
    public IActionResult DownloadFile(string fileName)
    {
        var filePath = "";
        
        if (_env.IsDevelopment())
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

        if (!System.IO.File.Exists(filePath))
        {
            return NotFound("File not found.");
        }

        var fileBytes = System.IO.File.ReadAllBytes(filePath);
        return File(fileBytes, "application/pdf", fileName);
    }
}