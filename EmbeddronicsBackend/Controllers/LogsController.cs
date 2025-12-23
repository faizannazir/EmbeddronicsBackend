using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.IO;
using System.Linq;

namespace EmbeddronicsBackend.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "admin")]
    public class LogsController : ControllerBase
    {
        private readonly string _logDirectory = "logs";

        [HttpGet("files")]
        public IActionResult GetLogFiles()
        {
            Serilog.Log.Information("Admin accessing log files list by user: {User}", User?.Identity?.Name);
            
            if (!Directory.Exists(_logDirectory))
            {
                return Ok(new { files = new string[0] });
            }

            var files = Directory.GetFiles(_logDirectory)
                .Select(f => new
                {
                    name = Path.GetFileName(f),
                    size = new FileInfo(f).Length,
                    created = new FileInfo(f).CreationTime,
                    modified = new FileInfo(f).LastWriteTime
                })
                .OrderByDescending(f => f.modified)
                .ToList();

            return Ok(new { files });
        }

        [HttpGet("view/{fileName}")]
        public IActionResult ViewLog(string fileName)
        {
            Serilog.Log.Information("Admin viewing log file: {FileName} by user: {User}", fileName, User?.Identity?.Name);
            
            var filePath = Path.Combine(_logDirectory, fileName);
            
            if (!System.IO.File.Exists(filePath))
            {
                return NotFound(new { message = "Log file not found" });
            }

            // Read last 1000 lines for performance
            var lines = System.IO.File.ReadLines(filePath).Reverse().Take(1000).Reverse().ToList();
            
            return Ok(new { fileName, lines, totalLines = lines.Count });
        }

        [HttpGet("search")]
        public IActionResult SearchLogs([FromQuery] string query, [FromQuery] string? fileName = null)
        {
            Serilog.Log.Information("Admin searching logs for: {Query} by user: {User}", query, User?.Identity?.Name);
            
            if (string.IsNullOrWhiteSpace(query))
            {
                return BadRequest(new { message = "Search query is required" });
            }

            var results = new List<object>();
            var files = string.IsNullOrEmpty(fileName) 
                ? Directory.GetFiles(_logDirectory) 
                : new[] { Path.Combine(_logDirectory, fileName) };

            foreach (var file in files)
            {
                if (!System.IO.File.Exists(file)) continue;

                var matchingLines = System.IO.File.ReadLines(file)
                    .Select((line, index) => new { line, lineNumber = index + 1 })
                    .Where(x => x.line.Contains(query, StringComparison.OrdinalIgnoreCase))
                    .Take(100)
                    .Select(x => new
                    {
                        file = Path.GetFileName(file),
                        lineNumber = x.lineNumber,
                        content = x.line
                    })
                    .ToList();

                results.AddRange(matchingLines);
            }

            return Ok(new { query, totalMatches = results.Count, results = results.Take(100) });
        }

        [HttpGet("stats")]
        public IActionResult GetLogStats()
        {
            Serilog.Log.Information("Admin accessing log statistics by user: {User}", User?.Identity?.Name);
            
            if (!Directory.Exists(_logDirectory))
            {
                return Ok(new { totalFiles = 0, totalSize = 0 });
            }

            var files = Directory.GetFiles(_logDirectory);
            var totalSize = files.Sum(f => new FileInfo(f).Length);
            
            // Count log levels in today's log
            var todayLog = files.FirstOrDefault(f => f.Contains(DateTime.Now.ToString("yyyyMMdd")));
            var stats = new
            {
                totalFiles = files.Length,
                totalSize = totalSize,
                informationCount = 0,
                warningCount = 0,
                errorCount = 0
            };

            if (todayLog != null && System.IO.File.Exists(todayLog))
            {
                var lines = System.IO.File.ReadAllLines(todayLog);
                stats = new
                {
                    totalFiles = files.Length,
                    totalSize = totalSize,
                    informationCount = lines.Count(l => l.Contains("[Information]") || l.Contains("INF")),
                    warningCount = lines.Count(l => l.Contains("[Warning]") || l.Contains("WRN")),
                    errorCount = lines.Count(l => l.Contains("[Error]") || l.Contains("ERR"))
                };
            }

            return Ok(stats);
        }

        [HttpDelete("{fileName}")]
        public IActionResult DeleteLog(string fileName)
        {
            Serilog.Log.Information("Admin deleting log file: {FileName} by user: {User}", fileName, User?.Identity?.Name);
            
            var filePath = Path.Combine(_logDirectory, fileName);
            
            if (!System.IO.File.Exists(filePath))
            {
                return NotFound(new { message = "Log file not found" });
            }

            try
            {
                System.IO.File.Delete(filePath);
                return Ok(new { message = "Log file deleted successfully" });
            }
            catch (Exception ex)
            {
                Serilog.Log.Error(ex, "Error deleting log file: {FileName}", fileName);
                return StatusCode(500, new { message = "Error deleting log file" });
            }
        }
    }
}
