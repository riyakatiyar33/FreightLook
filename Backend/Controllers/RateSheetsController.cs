using Microsoft.AspNetCore.Mvc;
using Models.Responses;
using Service.Implementation;
using Service.Interface;

namespace Backend.Controllers;

[ApiController]
[Route("api/ratesheets")]
[Produces("application/json")]
public class RateSheetsController : ControllerBase
{
    // Small headroom over the file limit for multipart boundaries/headers.
    private const long RequestLimit = UploadService.MaxFileBytes + 1024 * 1024;

    private readonly IUploadService _uploadService;

    public RateSheetsController(IUploadService uploadService)
    {
        _uploadService = uploadService;
    }

    /// <summary>Uploads a rate sheet (PDF or image). Rates are extracted, validated and saved.</summary>
    [HttpPost("upload")]
    [Consumes("multipart/form-data")]
    [RequestSizeLimit(RequestLimit)]
    [RequestFormLimits(MultipartBodyLengthLimit = RequestLimit)]
    [ProducesResponseType<UploadResultResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<UploadResultResponse>> Upload(IFormFile file, CancellationToken cancellationToken)
    {
        await using var stream = file.OpenReadStream();
        var result = await _uploadService.UploadAsync(stream, file.FileName, file.ContentType, file.Length, cancellationToken);
        return Ok(result);
    }
}
